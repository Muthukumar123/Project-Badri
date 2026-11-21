using System.Text.Json;
using System.Text.Json.Serialization;
using Badri.TradeFlow;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var development = builder.Environment.IsDevelopment();
TradeIdentity.AddIdentity(builder);
builder.Services.AddDbContext<TradeDb>(options =>
{
    if (development)
        options.UseSqlite(builder.Configuration.GetConnectionString("TradeDb") ?? "Data Source=tradeflow.dev.db");
    else
    {
        var connection = builder.Configuration.GetConnectionString("AzureSql");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Production requires ConnectionStrings:AzureSql, with a provisioned SQL schema.");
        options.UseSqlServer(connection);
    }
});
builder.Services.AddCors(o=>o.AddPolicy("local", p=>p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
// Production never trusts development headers. Authenticated access is required
// and the business tenant is resolved from a membership row on the server.
if (!development) app.UseAuthentication();
app.UseAuthorization();
app.UseCors("local");
app.UseDefaultFiles();
app.UseStaticFiles();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TradeDb>();
    if (development)
        db.Database.EnsureCreated();
    else
    {
        // Fail closed until the production SQL schema has been provisioned.
        // Do NOT call EnsureCreated() on a production financial database.
        if (!db.Database.CanConnect())
            throw new InvalidOperationException("Azure SQL is not accessible. Check identity, firewall and connection string.");
        _ = db.TraderMemberships.AsNoTracking().Select(x => x.Id).Take(1).ToList();
    }
}
app.MapGet("/auth/config", () => development
    ? Results.Ok(new { mode = "development" })
    : Results.Ok(new {
        mode = "entra",
        tenantId = builder.Configuration["AzureAd:TenantId"],
        clientId = builder.Configuration["AzureAd:SpaClientId"],
        scope = $"api://{builder.Configuration["AzureAd:ClientId"]}/access_as_user"
    }));
app.MapGet("/health",()=> Results.Ok(new{status="ok", mode=development ? "DEVELOPMENT_ONLY" : "SECURED", product="Badri TradeFlow R1"}));

var api = app.MapGroup("/api/v1");
if (!development) api.RequireAuthorization();
api.AddEndpointFilter(async (ctx, next) =>
{
    var db = ctx.HttpContext.RequestServices.GetRequiredService<TradeDb>();
    var actor = await TradeIdentity.ResolveActorAsync(ctx.HttpContext, db, builder.Configuration, development);
    if (actor is null) return Results.Unauthorized();
    ctx.HttpContext.Items["actor"] = actor;
    return await next(ctx);
});

api.MapGet("/me",(HttpContext h)=>{var a=Who(h);return Results.Ok(new{a.UserId,a.Role});});
api.MapGet("/retailers",async(HttpContext h,TradeDb db)=>{
    var a=Who(h);return Results.Ok(await db.Retailers.Where(x=>x.TenantId==a.TenantId).OrderBy(x=>x.Name).ToListAsync());
});
api.MapPost("/retailers",async(HttpContext h,TradeDb db,CreateRetailer req)=>{
    var a=Who(h);if(!Can(a,TraderRole.Owner,TraderRole.Accounts)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(req.Name)||string.IsNullOrWhiteSpace(req.Beat)||req.UnspecifiedOpeningDue<0)
        return Results.BadRequest(new{error="Shop name, beat and non-negative opening due required"});
    var shop=new Retailer{TenantId=a.TenantId,Name=req.Name.Trim(),Beat=req.Beat.Trim(),UnspecifiedOpeningDue=req.UnspecifiedOpeningDue};
    db.Retailers.Add(shop);Log(db,a,"retailer.created",shop.Id);await db.SaveChangesAsync();
    return Results.Created($"/api/v1/retailers/{shop.Id}",shop);
});
api.MapPost("/opening-bills",async(HttpContext h,TradeDb db,CreateOpeningBill req)=>{
    var a=Who(h);if(!Can(a,TraderRole.Owner,TraderRole.Accounts))return Results.Forbid();
    if(!Rules.ValidMoney(req.Amount)||string.IsNullOrWhiteSpace(req.BillNo))return Results.BadRequest(new{error="Invalid amount or bill"});
    if(!await db.Retailers.AnyAsync(x=>x.Id==req.RetailerId&&x.TenantId==a.TenantId))return Results.NotFound();
    if(await db.Bills.AnyAsync(x=>x.TenantId==a.TenantId&&x.BillNo==req.BillNo))return Results.Conflict(new{error="Duplicate bill"});
    var bill=new RetailerBill{TenantId=a.TenantId,RetailerId=req.RetailerId,BillNo=req.BillNo.Trim(),Amount=req.Amount,DueDate=req.DueDate};
    db.Bills.Add(bill);Log(db,a,"opening-bill.added",bill.Id);await db.SaveChangesAsync();return Results.Created($"/api/v1/opening-bills/{bill.Id}",bill);
});
api.MapGet("/bills",async(HttpContext h,TradeDb db)=>{
    var a=Who(h);return Results.Ok(await db.Bills.Where(x=>x.TenantId==a.TenantId).OrderBy(x=>x.DueDate).ToListAsync());
});
api.MapPost("/collections",async(HttpContext h,TradeDb db,CapturePayment req)=>{
    var a=Who(h);
    var key=h.Request.Headers["Idempotency-Key"].ToString();
    if(string.IsNullOrWhiteSpace(key)||key.Length>120)return Results.BadRequest(new{error="Idempotency-Key header required"});
    if(!Rules.ValidMoney(req.Amount)|| (req.BillId is null&&string.IsNullOrWhiteSpace(req.LegacyBillNo)))return Results.BadRequest(new{error="Valid amount and bill reference required"});
    if(!await db.Retailers.AnyAsync(x=>x.TenantId==a.TenantId&&x.Id==req.RetailerId))return Results.NotFound();
    var fingerprint=Rules.Fingerprint(req.RetailerId,req.BillId,req.LegacyBillNo,req.Amount,req.Mode);
    var previous=await db.Collections.FirstOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.IdempotencyKey==key);
    if(previous!=null)return previous.Fingerprint==fingerprint?Results.Ok(previous):Results.Conflict(new{error="Key reused with changed payment"});
    if(req.BillId is Guid b){
       var bill=await db.Bills.FirstOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.Id==b&&x.RetailerId==req.RetailerId);
       if(bill==null)return Results.NotFound();
       var receipts=await db.Collections.Where(x=>x.TenantId==a.TenantId&&x.RetailerBillId==b&&x.State!=PaymentState.Reversed).ToListAsync();
       var hc=await db.EarlierCollections.Where(x=>x.TenantId==a.TenantId&&x.BillId==b&&x.Status=="Accepted").ToListAsync();
       decimal remaining=bill.Amount-receipts.Sum(x=>x.Amount)-hc.Sum(x=>x.Amount);
       if(req.Amount>remaining)return Results.Conflict(new{error="Amount exceeds remaining bill",remaining});
    }
    var ist=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
    var record=new Collection{TenantId=a.TenantId,RetailerId=req.RetailerId,RetailerBillId=req.BillId,
        LegacyBillNo=req.LegacyBillNo,Amount=req.Amount,Mode=req.Mode,State=Rules.InitialPaymentState(req.Mode),
        CollectedBy=a.UserId,BusinessDate=DateOnly.FromDateTime(ist),BankReference=req.BankReference,
        IdempotencyKey=key,Fingerprint=fingerprint};
    db.Collections.Add(record);Log(db,a,"payment.captured",record.Id);
    try{await db.SaveChangesAsync();}catch(DbUpdateException){return Results.Conflict(new{error="Concurrent duplicate; retry same key"});}
    return Results.Created($"/api/v1/collections/{record.Id}",new{record.Id,record.Amount,record.Mode,record.State,record.LegacyBillNo});
});
api.MapGet("/collections",async(HttpContext h,TradeDb db)=>{
    var a=Who(h);return Results.Ok(await db.Collections.Where(x=>x.TenantId==a.TenantId).OrderByDescending(x=>x.CapturedAtUtc).Take(200).ToListAsync());
});
// SIMULATION ONLY. In production verify payment with bank/provider evidence; screenshot is not proof.
api.MapPost("/collections/{id:guid}/verify-demo",async(HttpContext h,TradeDb db,Guid id,VerifyDemo req)=>{
    var a=Who(h);if(!Can(a,TraderRole.Owner,TraderRole.Accounts))return Results.Forbid();
    var payment=await db.Collections.FirstOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.Id==id);
    if(payment==null)return Results.NotFound();
    if(payment.State!=PaymentState.BankAwaitingVerification||string.IsNullOrWhiteSpace(req.Reference))
       return Results.Conflict(new{error="Receipt not pending verification or missing evidence reference"});
    payment.State=PaymentState.Cleared;payment.BankReference=req.Reference.Trim();
    Log(db,a,"DEMO.bank-verification",payment.Id);await db.SaveChangesAsync();return Results.Ok(new{payment.Id,payment.State,warning="DEMO verification only"});
});
api.MapPost("/cash-handovers",async(HttpContext h,TradeDb db,SubmitCash req)=>{
    var a=Who(h);decimal counted;
    try{counted=Rules.CashCount(req.Denominations);}catch(ArgumentException e){return Results.BadRequest(new{error=e.Message});}
    var pending=await db.Collections.Where(x=>x.TenantId==a.TenantId&&x.CollectedBy==a.UserId&&
        x.BusinessDate==req.Date&&x.State==PaymentState.CashAwaitingHandover).ToListAsync();
    if(pending.Count==0)return Results.BadRequest(new{error="No pending cash receipts"});
    if(await db.CashHandovers.AnyAsync(x=>x.TenantId==a.TenantId&&x.SalesmanId==a.UserId&&x.Date==req.Date))
       return Results.Conflict(new{error="Handover already submitted for date"});
    var expected=pending.Sum(x=>x.Amount);var difference=counted-expected;
    if(difference!=0&&string.IsNullOrWhiteSpace(req.Reason))return Results.BadRequest(new{error="Cash difference requires reason",expected,counted,difference});
    var cash=new CashHandover{TenantId=a.TenantId,SalesmanId=a.UserId,Date=req.Date,Expected=expected,
        Counted=counted,Difference=difference,Reason=req.Reason,ReceiptIdsJson=JsonSerializer.Serialize(pending.Select(x=>x.Id))};
    db.CashHandovers.Add(cash);Log(db,a,"cash.submitted",cash.Id);await db.SaveChangesAsync();
    return Results.Created($"/api/v1/cash-handovers/{cash.Id}",cash);
});
api.MapGet("/cash-handovers",async(HttpContext h,TradeDb db)=>{
    var a=Who(h);if(!Can(a,TraderRole.Owner,TraderRole.Accounts))return Results.Forbid();
    return Results.Ok(await db.CashHandovers.Where(x=>x.TenantId==a.TenantId).ToListAsync());
});
api.MapPost("/cash-handovers/{id:guid}/approve",async(HttpContext h,TradeDb db,Guid id,ApproveCash req)=>{
    var a=Who(h);if(a.Role!=TraderRole.Owner)return Results.Forbid();
    var cash=await db.CashHandovers.FirstOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.Id==id);
    if(cash==null)return Results.NotFound();
    if(cash.Status!="Submitted"||(cash.Difference!=0&&string.IsNullOrWhiteSpace(req.ReviewReason)))
       return Results.Conflict(new{error="Cash close requires discrepancy review"});
    var ids=JsonSerializer.Deserialize<Guid[]>(cash.ReceiptIdsJson)??Array.Empty<Guid>();
    await using var tx=await db.Database.BeginTransactionAsync();
    var receipts=await db.Collections.Where(x=>x.TenantId==a.TenantId&&ids.Contains(x.Id)).ToListAsync();
    if(receipts.Count!=ids.Length||receipts.Any(x=>x.State!=PaymentState.CashAwaitingHandover))
       return Results.Conflict(new{error="Underlying receipts changed; investigate"});
    foreach(var receipt in receipts) receipt.State=PaymentState.Cleared;
    cash.Status="Approved";cash.ApprovedBy=a.UserId;
    if(!string.IsNullOrWhiteSpace(req.ReviewReason))cash.Reason=req.ReviewReason;
    Log(db,a,"cash.approved",cash.Id);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(cash);
});
api.MapPost("/earlier-collections",async(HttpContext h,TradeDb db,CreateHc req)=>{
    var a=Who(h);if(!Can(a,TraderRole.Owner,TraderRole.Accounts))return Results.Forbid();
    if(!Rules.ValidMoney(req.Amount)||!await db.Retailers.AnyAsync(x=>x.TenantId==a.TenantId&&x.Id==req.RetailerId))
       return Results.BadRequest(new{error="Invalid retailer/amount"});
    if(req.BillId is Guid b&&!await db.Bills.AnyAsync(x=>x.TenantId==a.TenantId&&x.Id==b&&x.RetailerId==req.RetailerId))return Results.NotFound();
    var hc=new EarlierCollection{TenantId=a.TenantId,RetailerId=req.RetailerId,BillId=req.BillId,
        LegacyBillNo=req.LegacyBillNo,Amount=req.Amount,SourceMarker=req.Marker??"HC"};
    db.EarlierCollections.Add(hc);Log(db,a,"hc.unproven",hc.Id);await db.SaveChangesAsync();return Results.Created($"/api/v1/earlier-collections/{hc.Id}",hc);
});
api.MapPost("/earlier-collections/{id:guid}/decision",async(HttpContext h,TradeDb db,Guid id,HcDecision req)=>{
    var a=Who(h);if(a.Role!=TraderRole.Owner)return Results.Forbid();
    var hc=await db.EarlierCollections.FirstOrDefaultAsync(x=>x.TenantId==a.TenantId&&x.Id==id);
    if(hc==null)return Results.NotFound();
    if(hc.Status!="Unproven"||string.IsNullOrWhiteSpace(req.Reason))return Results.Conflict(new{error="Review reason required"});
    hc.Status=req.Accept?"Accepted":"Rejected";hc.DecisionReason=req.Reason;
    Log(db,a,"hc."+hc.Status,hc.Id);await db.SaveChangesAsync();return Results.Ok(hc);
});
api.MapGet("/receivables",async(HttpContext h,TradeDb db)=>{
    var a=Who(h);
    var retailers=await db.Retailers.Where(x=>x.TenantId==a.TenantId).ToListAsync();
    var bills=await db.Bills.Where(x=>x.TenantId==a.TenantId).ToListAsync();
    var receipts=await db.Collections.Where(x=>x.TenantId==a.TenantId).ToListAsync();
    var hc=await db.EarlierCollections.Where(x=>x.TenantId==a.TenantId&&x.Status=="Accepted").ToListAsync();
    return Results.Ok(retailers.Select(r=>new{
       r.Name,r.Beat,r.Id,
       Due=r.UnspecifiedOpeningDue+bills.Where(b=>b.RetailerId==r.Id).Sum(b=>b.Amount)
            - receipts.Where(p=>p.RetailerId==r.Id&&p.RetailerBillId!=null&&Rules.ReducesDue(p.State)).Sum(p=>p.Amount)
            - hc.Where(x=>x.RetailerId==r.Id&&x.BillId!=null).Sum(x=>x.Amount),
       Pending=receipts.Where(p=>p.RetailerId==r.Id&&p.State!=PaymentState.Cleared&&p.State!=PaymentState.Reversed).Sum(p=>p.Amount),
       Unallocated=receipts.Where(p=>p.RetailerId==r.Id&&p.RetailerBillId==null).Sum(p=>p.Amount)
    }));
});
api.MapPost("/document-drafts",async(HttpContext h,TradeDb db,NewDocument req)=>{
    var a=Who(h);if(!Can(a,TraderRole.Owner,TraderRole.Accounts))return Results.Forbid();
    if(string.IsNullOrWhiteSpace(req.Filename))return Results.BadRequest(new{error="Filename required"});
    var draft=new DocumentDraft{TenantId=a.TenantId,Type=req.Type,Filename=Path.GetFileName(req.Filename),State=DocumentState.Draft};
    db.DocumentDrafts.Add(draft);Log(db,a,"document.metadata-created",draft.Id);await db.SaveChangesAsync();
    return Results.Created($"/api/v1/document-drafts/{draft.Id}",new{draft.Id,draft.Type,draft.State,warning="Metadata only; no PDF was uploaded or extracted"});
});
api.MapGet("/document-drafts",async(HttpContext h,TradeDb db)=>{
    var a=Who(h);return Results.Ok(await db.DocumentDrafts.Where(x=>x.TenantId==a.TenantId).ToListAsync());
});
api.MapGet("/audit",async(HttpContext h,TradeDb db)=>{
    var a=Who(h);if(!Can(a,TraderRole.Owner,TraderRole.Accounts))return Results.Forbid();
    return Results.Ok(await db.Audits.Where(x=>x.TenantId==a.TenantId).OrderByDescending(x=>x.AtUtc).Take(100).ToListAsync());
});
app.MapFallbackToFile("index.html");
app.Run();
static Actor Who(HttpContext ctx)=>(Actor)ctx.Items["actor"]!;
static bool Can(Actor a,params TraderRole[] roles)=>roles.Contains(a.Role);
static void Log(TradeDb db,Actor a,string action,Guid id)=>db.Audits.Add(new AuditEvent{TenantId=a.TenantId,UserId=a.UserId,Action=action,RecordId=id});
public record CreateRetailer(string Name,string Beat,decimal UnspecifiedOpeningDue);
public record CreateOpeningBill(Guid RetailerId,string BillNo,decimal Amount,DateOnly DueDate);
public record CapturePayment(Guid RetailerId,Guid? BillId,string? LegacyBillNo,decimal Amount,PaymentMode Mode,string? BankReference);
public record VerifyDemo(string Reference);
public record SubmitCash(DateOnly Date,Dictionary<int,int> Denominations,string? Reason);
public record ApproveCash(string? ReviewReason);
public record CreateHc(Guid RetailerId,Guid? BillId,string? LegacyBillNo,decimal Amount,string? Marker);
public record HcDecision(bool Accept,string Reason);
public record NewDocument(PaperDocumentType Type,string Filename);
public partial class Program{}
