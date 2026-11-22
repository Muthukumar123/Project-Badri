using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Badri.Pilot;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder=WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PilotStore>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{
    o.Cookie.Name=builder.Environment.IsDevelopment()?"BadriPilotDev":"__Host-BadriPilot";o.Cookie.HttpOnly=true;o.Cookie.Path="/";
    o.Cookie.SameSite=SameSiteMode.Strict;
    o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
    o.ExpireTimeSpan=TimeSpan.FromHours(6);o.SlidingExpiration=false;
    o.Events.OnValidatePrincipal=async context=>
    {
        var identity=context.Principal?.Identity?.Name ?? "";
        var storedRole=context.Principal?.FindFirst(ClaimTypes.Role)?.Value ?? "";
        var actualStamp=context.Principal?.FindFirst("sessionStamp")?.Value ?? "";
        var store=context.HttpContext.RequestServices.GetRequiredService<PilotStore>();
        var stamp=await store.SessionStamp(identity);
        var role=await store.RoleForUser(identity);
        if (string.IsNullOrEmpty(stamp) || role!=storedRole || !new[]{"Owner","Salesperson","Retailer"}.Contains(role)
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(actualStamp.PadRight(64)[..64]),
                System.Text.Encoding.UTF8.GetBytes(stamp)))
        {
            context.RejectPrincipal();
        }
    };
    o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
    o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options=>{
    options.RejectionStatusCode=429;
    options.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>
        new FixedWindowRateLimiterOptions{PermitLimit=7,Window=TimeSpan.FromMinutes(2),QueueLimit=0}));
});
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=12*1024*1024);
var app=builder.Build();
// Trust forwarded headers only after explicitly configuring known Azure reverse proxies.
// Do not turn on UseForwardedHeaders with unrestricted trust.
if(!app.Environment.IsDevelopment())app.UseHsts(); // Azure App Service enforces HTTPS-only at the edge.
app.UseDefaultFiles();app.UseStaticFiles();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
app.MapGet("/health",()=>Results.Ok(new{status="ok",database="XML pilot only",version="pilot-r1",data="review-only source samples"}));

app.MapPost("/api/auth/login",async(Login req,HttpContext ctx,PilotStore store)=>{
    if(string.IsNullOrWhiteSpace(req.Username)||string.IsNullOrWhiteSpace(req.Password))return Results.BadRequest(new{error="Username and password required"});
    var (role,valid)=await store.CheckPassword(req.Username.Trim().ToLowerInvariant(),req.Password);
    if(!valid)return Results.Unauthorized();
    var csrf=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var stamp=await store.SessionStamp(req.Username.Trim().ToLowerInvariant());
    var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.Name,req.Username.Trim().ToLowerInvariant()),
        new Claim(ClaimTypes.Role,role),new Claim("tenant",PilotStore.DemoTenant),new Claim("csrf",csrf),
        new Claim("sessionStamp",stamp)},
        CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(identity));
    return Results.Ok(new{username=req.Username,role,csrf});
}).RequireRateLimiting("login");
app.MapGet("/api/auth/session",(HttpContext ctx)=>ctx.User.Identity?.IsAuthenticated==true
    ? Results.Ok(new{username=Name(ctx),role=Role(ctx),csrf=ctx.User.FindFirst("csrf")?.Value})
    : Results.Unauthorized());
app.MapPost("/api/auth/logout",async(HttpContext ctx)=>{if(!Csrf(ctx))return Results.Forbid();await ctx.SignOutAsync();return Results.Ok(new{loggedOut=true});}).RequireAuthorization();

var api=app.MapGroup("/api").RequireAuthorization();
api.AddEndpointFilter(async(c,next)=>{
    // Authenticate via signed ASP.NET cookie; never read client-supplied tenant/role headers.
    if(c.HttpContext.User.FindFirst("tenant")?.Value!=PilotStore.DemoTenant)return Results.Forbid();
    if(!InRole(c.HttpContext,"Owner","Salesperson","Retailer"))return Results.Forbid();
    if(c.HttpContext.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        if(!Csrf(c.HttpContext))return Results.Problem(statusCode:403,title:"Missing or invalid anti-forgery token");
    return await next(c);
});
api.MapGet("/state",async(HttpContext c,PilotStore s)=>await s.Read<IResult>(d=>{
    var role=Role(c);var root=d.Root!;
    var docs=Rows(root,"documents");var shops=Rows(root,"retailers");var products=Rows(root,"products");
    var purchases=Rows(root,"purchases");var stock=Rows(root,"stock");var bills=Rows(root,"bills");
    var receipts=Rows(root,"collections");var deliveries=Rows(root,"deliveries");
    var cash=Rows(root,"cashcloses");var payments=Rows(root,"payments");var returns=Rows(root,"returns");
    var supplier=Rows(root,"suppliers");var orders=Rows(root,"orders");
    var due=bills.Sum(x=>Value(x,"amount"))-receipts.Where(x=>Get(x,"status")=="Cleared"&&Get(x,"billId")!="").Sum(x=>Value(x,"amount"));
    var pending=receipts.Where(x=>Get(x,"status")!="Cleared").Sum(x=>Value(x,"amount"));
    var payables=purchases.Where(x=>Get(x,"status")=="Received"||Get(x,"status")=="Part received")
        .Sum(x=>Value(x,"amount")-Value(x,"paid"));
    var totals=new{receivable=due,pendingReceipts=pending,supplierPayables=payables,documentReviews=docs.Count(x=>Get(x,"status")=="Review required"),stockUnits=products.Sum(x=>StockQty(stock,Get(x,"id")))};
    if(role=="Retailer"){
        // This R1 retailer identity is intentionally scoped to Kumar Maligai only.
        // Future multi-retailer rollout requires separate retailer accounts & memberships.
        var mine=shops.FirstOrDefault(x=>Get(x,"name")=="Kumar Maligai");var id=Get(mine,"id");
        var myBills=bills.Where(x=>Get(x,"retailerId")==id).ToList();
        var myReceipts=receipts.Where(x=>Get(x,"retailerId")==id).ToList();
        var myDue=myBills.Sum(x=>Value(x,"amount"))
            -myReceipts.Where(x=>Get(x,"status")=="Cleared").Sum(x=>Value(x,"amount"));
        return Results.Ok(new{role,totals=new{receivable=myDue,pendingReceipts=myReceipts
                .Where(x=>Get(x,"status")!="Cleared").Sum(x=>Value(x,"amount"))},
            retailers=mine is null?[]:new[]{mine},
            orders=orders.Where(x=>Get(x,"retailerId")==id),
            products=products.Select(x=>new{id=Get(x,"id"),name=Get(x,"name"),supplier=Get(x,"supplier")}),
            bills=myBills,deliveries=deliveries.Where(x=>Get(x,"retailerId")==id),
            collections=myReceipts});
    }
    if(role=="Salesperson")return Results.Ok(new{role,totals=new{
        assignedDeliveries=deliveries.Count(x=>Get(x,"status")=="Dispatched"),
        pendingCash=receipts.Where(x=>Get(x,"status")=="Cash pending handover"&&Get(x,"by")==Name(c))
            .Sum(x=>Value(x,"amount")),
        pendingOrders=orders.Count(x=>Get(x,"status")=="New")},
        retailers=shops,products=products.Select(x=>new{id=Get(x,"id"),name=Get(x,"name")}),
        orders,bills,deliveries,
        documents=docs.Where(x=>Get(x,"uploadedBy")==Name(c)&&Get(x,"type")!="Supplier invoice"),
        collections=receipts.Where(x=>Get(x,"by")==Name(c)),
        cashcloses=cash.Where(x=>Get(x,"by")==Name(c))});
    return Results.Ok(new{role,totals,retailers=shops,suppliers=supplier,products,documents=docs,purchases,stock,bills,
        orders, deliveries,collections=receipts,cashcloses=cash,payments,returns,
        audit=Rows(root,"audit").TakeLast(100).Reverse()});
}));


// Order request is a quotation/stock planning request, NOT a posted bill,
// stock dispatch, delivery acknowledgment, cash receipt or payable.
api.MapPost("/orders",async(NewOrder req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,
    "order.request",["Owner","Salesperson","Retailer"],d=>{
    var root=d.Root!;
    var shop=Role(ctx)=="Retailer"
        ? root.Element("retailers")!.Elements("retailer").FirstOrDefault(x=>PilotStore.Attr(x,"name")=="Kumar Maligai")
        : PilotStore.ById(root.Element("retailers")!,req.RetailerId??"");
    if(shop is null)throw new InvalidOperationException("Select a valid retailer.");
    var product=PilotStore.ById(root.Element("products")!,req.ProductId);
    if(product is null||req.Quantity<=0||req.Quantity>10000||req.Quantity!=decimal.Truncate(req.Quantity))
        throw new InvalidOperationException("Select a product and whole-number quantity between 1 and 10,000.");
    var note=(req.Note??"").Trim();
    if(note.Length>240)throw new InvalidOperationException("Order note is limited to 240 characters.");
    var id=PilotStore.Id();
    var row=PilotStore.Obj("order",("id",id),("retailerId",PilotStore.Attr(shop,"id")),
        ("retailer",PilotStore.Attr(shop,"name")),("productId",req.ProductId),
        ("product",PilotStore.Attr(product,"name")),("qty",req.Quantity),
        ("note",note),("placedBy",Name(ctx)),("placedAt",DateTime.UtcNow),
        ("status","New"));
    root.Element("orders")!.Add(row);return Render(row);
}));
api.MapPost("/orders/{id}/decision",async(string id,OrderDecision req,HttpContext ctx,PilotStore s)=>
    await Change(ctx,s,"order.decision",["Owner"],d=>{
    var order=PilotStore.ById(d.Root!.Element("orders")!,id)
        ??throw new InvalidOperationException("Order does not exist.");
    if(PilotStore.Attr(order,"status")!="New")throw new InvalidOperationException("Order already reviewed.");
    if(!req.Approve && string.IsNullOrWhiteSpace(req.Note))
        throw new InvalidOperationException("Enter a reason for declining the order.");
    order.SetAttributeValue("status",req.Approve?"Accepted - pending billing":"Declined");
    order.SetAttributeValue("ownerNote",req.Note?.Trim()??"");
    order.SetAttributeValue("reviewedBy",Name(ctx));
    order.SetAttributeValue("reviewedAt",DateTime.UtcNow.ToString("O"));
    return Render(order);
}));

api.MapPost("/retailers",async(NewRetailer req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"retailer.create",["Owner"],d=>{
    if(string.IsNullOrWhiteSpace(req.Name)||string.IsNullOrWhiteSpace(req.Beat))throw new InvalidOperationException("Shop and beat are required");
    var row=PilotStore.Obj("retailer",("id",PilotStore.Id()),("name",req.Name.Trim()),("beat",req.Beat.Trim()),("openingDue","0"),("status","Verified"));
    d.Root!.Element("retailers")!.Add(row);return Render(row);
}));
api.MapPost("/sales-bills",async(NewBill req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"sales-bill.create",["Owner"],d=>{
    var rs=d.Root!;var retailer=PilotStore.ById(rs.Element("retailers")!,req.RetailerId);
    if(retailer==null)throw new InvalidOperationException("Unknown retailer");
    if(req.Amount<=0||req.Amount!=decimal.Round(req.Amount,2)||string.IsNullOrWhiteSpace(req.BillNo))throw new InvalidOperationException("Valid bill amount and number required");
    if(rs.Element("bills")!.Elements().Any(x=>PilotStore.Attr(x,"billNo")==req.BillNo))throw new InvalidOperationException("Duplicate bill number");
    var linkedOrder=string.IsNullOrWhiteSpace(req.OrderId)?null:
        PilotStore.ById(rs.Element("orders")!,req.OrderId);
    if(!string.IsNullOrWhiteSpace(req.OrderId) &&
        (linkedOrder is null||PilotStore.Attr(linkedOrder,"retailerId")!=req.RetailerId||
        PilotStore.Attr(linkedOrder,"status")!="Accepted - pending billing"))
        throw new InvalidOperationException("Only an accepted order for this retailer can be linked to a bill");
    var row=PilotStore.Obj("bill",("id",PilotStore.Id()),("retailerId",req.RetailerId),("retailer",PilotStore.Attr(retailer,"name")),
        ("billNo",req.BillNo.Trim()),("amount",req.Amount),("due",req.DueDate),
        ("orderId",req.OrderId??""),("status","Created - pending dispatch"));
    rs.Element("bills")!.Add(row);
    if(linkedOrder is not null)linkedOrder.SetAttributeValue("status","Billed - pending dispatch");
    return Render(row);
}));
api.MapPost("/documents/upload",async(HttpContext ctx,PilotStore s,IWebHostEnvironment env,IConfiguration config)=>{
    if(!InRole(ctx,"Owner","Salesperson"))return Results.Forbid();
    var form=await ctx.Request.ReadFormAsync();var file=form.Files.FirstOrDefault();
    var type=form["type"].ToString();
    if(Role(ctx)=="Salesperson" && type=="Supplier invoice")
        return Results.Forbid();
    if(file is null||file.Length==0||file.Length>10*1024*1024|| !new[]{"Collection sheet","Supplier invoice","Delivery sheet"}.Contains(type))return Results.BadRequest(new{error="Select type and PDF/JPG/PNG file (max 10MB)"});
    var ext=Path.GetExtension(file.FileName).ToLowerInvariant();if(!new[]{".pdf",".png",".jpg",".jpeg"}.Contains(ext))return Results.BadRequest(new{error="Unsupported file extension"});
    using var input=file.OpenReadStream();var header=new byte[8];_ = await input.ReadAsync(header);input.Position=0;
    var valid=ext==".pdf"?System.Text.Encoding.ASCII.GetString(header).StartsWith("%PDF-"):
        ext==".png"?header.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}):
        header[0]==255&&header[1]==216&&header[2]==255;
    if(!valid)return Results.BadRequest(new{error="Uploaded file does not match its extension"});
    var folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(config["BADRI_XML_PATH"]??Path.Combine(env.ContentRootPath,"pilot-data.xml")))!,"uploads");
    Directory.CreateDirectory(folder);var id=PilotStore.Id();var stored=Path.Combine(folder,id+ext);
    try{await using(var output=File.Create(stored))await input.CopyToAsync(output);
        return await Change(ctx,s,"document.upload",["Owner","Salesperson"],d=>{
            var group=d.Root!.Element("documents")!;
            group.Add(PilotStore.Obj("document",("id",id),("title",Path.GetFileName(file.FileName)),("type",type),("claimed",""),
                ("computed",""),("status","Review required"),("source","Uploaded file; manual verification"),
                 ("uploadedBy",Name(ctx)),("path",id+ext)));
            return new{id,status="Review required",message="Stored privately. Manual review is required; OCR is not enabled in this pilot."};
        });
    }catch{try{File.Delete(stored);}catch{}throw;}
});
api.MapGet("/documents/{id}/file",async(string id,HttpContext ctx,PilotStore s,IConfiguration config,IWebHostEnvironment env)=>{
    if(!InRole(ctx,"Owner","Salesperson"))return Results.Forbid();
    var metadata=await s.Read(d=>{
        var item=PilotStore.ById(d.Root!.Element("documents")!,id);
        return new {path=PilotStore.Attr(item,"path"),by=PilotStore.Attr(item,"uploadedBy"),
            type=PilotStore.Attr(item,"type")};
    });
    if(Role(ctx)=="Salesperson" && (metadata.by!=Name(ctx)||metadata.type=="Supplier invoice"))
        return Results.Forbid();
    var path=metadata.path;
    if(string.IsNullOrEmpty(path)||Path.GetFileName(path)!=path)return Results.NotFound();
    var folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(config["BADRI_XML_PATH"]??Path.Combine(env.ContentRootPath,"pilot-data.xml")))!,"uploads");
    var full=Path.Combine(folder,path);
    if(!File.Exists(full))return Results.NotFound();
    var media=path.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase)?"application/pdf":path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)?"image/png":"image/jpeg";
    return Results.File(full,media,enableRangeProcessing:false);
});
api.MapGet("/documents/{id}/rows",async(string id,HttpContext ctx,PilotStore s)=>{
    if(!InRole(ctx,"Owner"))return Results.Forbid();
    return Results.Ok(await s.Read(d=>{
        var item=PilotStore.ById(d.Root!.Element("documents")!,id);
        return item?.Elements("row").Select(Render).ToList()??new List<Dictionary<string,string>>();
    }));
});
api.MapPost("/documents/{id}/review",async(string id,Review req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"document.review",["Owner"],d=>{
    var row=PilotStore.ById(d.Root!.Element("documents")!,id)??throw new InvalidOperationException("Unknown document");
    if(req.Approve && string.IsNullOrWhiteSpace(req.Note))throw new InvalidOperationException("Record reviewer evidence or reason");
    row.SetAttributeValue("status",req.Approve?"Verified - staging only":"Review required");
    row.SetAttributeValue("reviewNote",req.Note?.Trim()??"");row.SetAttributeValue("reviewedBy",Name(ctx));
    return Render(row);
}));
api.MapPost("/purchases/{id}/approve",async(string id,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"purchase.reviewed",["Owner"],d=>{
    var row=PilotStore.ById(d.Root!.Element("purchases")!,id)??throw new InvalidOperationException("Unknown supplier bill");
    if(PilotStore.Attr(row,"completeness")!="Complete")throw new InvalidOperationException("Missing invoice page. Cannot approve.");
    var reviewed=d.Root!.Element("documents")!.Elements("document").Any(x=>
        PilotStore.Attr(x,"title").Contains(PilotStore.Attr(row,"invoice"),StringComparison.OrdinalIgnoreCase)
        &&PilotStore.Attr(x,"status")=="Verified - staging only");
    if(!reviewed)throw new InvalidOperationException("Review supplier document and record the source check before invoice approval");
    if(PilotStore.Attr(row,"status")!="Review required")throw new InvalidOperationException("Bill already reviewed");
    row.SetAttributeValue("status","Approved - pending goods receipt");return Render(row);
}));
api.MapPost("/grn",async(NewGrn req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"stock.grn",["Owner"],d=>{
    var root=d.Root!;var inv=PilotStore.ById(root.Element("purchases")!,req.PurchaseId)??throw new InvalidOperationException("Supplier invoice not found");
    if(PilotStore.Attr(inv,"status") is not ("Approved - pending goods receipt" or "Part received"))throw new InvalidOperationException("Approve full supplier invoice before receiving");
    var product=PilotStore.ById(root.Element("products")!,req.ProductId)??throw new InvalidOperationException("Product not found");
    if(PilotStore.Attr(inv,"supplier")!=PilotStore.Attr(product,"supplier"))throw new InvalidOperationException("Product does not belong to this supplier bill");
    var expectedLine=inv.Elements("expected").FirstOrDefault(x=>PilotStore.Attr(x,"sku")==PilotStore.Attr(product,"name"))
        ??throw new InvalidOperationException("Product is not present on this supplier invoice");
    var alreadyCounted=root.Element("stock")!.Elements("movement")
        .Where(x=>PilotStore.Attr(x,"purchaseId")==req.PurchaseId&&PilotStore.Attr(x,"productId")==req.ProductId)
        .Sum(x=>PilotStore.Dec(x,"qty")+PilotStore.Dec(x,"quarantine"));
    if(alreadyCounted+req.Quantity>PilotStore.Dec(expectedLine,"qty"))throw new InvalidOperationException("Received quantity exceeds supplier invoice; record an exception");
    if(req.Quantity<=0||string.IsNullOrWhiteSpace(req.Batch)||req.Damaged<0||req.Damaged>req.Quantity)throw new InvalidOperationException("Count received, damaged units and batch");
    var stock=PilotStore.Obj("movement",("id",PilotStore.Id()),("productId",req.ProductId),("product",PilotStore.Attr(product,"name")),
        ("purchaseId",req.PurchaseId),("batch",req.Batch.Trim()),("expiry",req.Expiry??""),("qty",req.Quantity-req.Damaged),
        ("quarantine",req.Damaged),("reason","GRN"),("at",DateTime.UtcNow));
    root.Element("stock")!.Add(stock);inv.SetAttributeValue("status","Part received");
    inv.SetAttributeValue("received",PilotStore.Dec(inv,"received")+req.Quantity);
    return Render(stock);
}));
api.MapPost("/purchases/{id}/complete-grn",async(string id,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"purchase.grn-completed",["Owner"],d=>{
    var row=PilotStore.ById(d.Root!.Element("purchases")!,id)??throw new InvalidOperationException("Unknown purchase");
    if(PilotStore.Attr(row,"status")!="Part received"||PilotStore.Dec(row,"received")<=0)
        throw new InvalidOperationException("First count actual received goods, then confirm receiving completion");
    var stock=d.Root!.Element("stock")!.Elements("movement")
        .Where(x=>PilotStore.Attr(x,"purchaseId")==id).ToList();
    var missing=row.Elements("expected")
        .Where(line=>{
            var sku=PilotStore.Attr(line,"sku");
            var received=stock.Where(m=>PilotStore.Attr(m,"product")==sku)
              .Sum(m=>PilotStore.Dec(m,"qty")+PilotStore.Dec(m,"quarantine"));
            return received!=PilotStore.Dec(line,"qty");
        }).Select(x=>PilotStore.Attr(x,"sku")).ToList();
    if(missing.Count>0)throw new InvalidOperationException("Cannot complete GRN; count missing/short items: "+string.Join(", ",missing));
    row.SetAttributeValue("status","Received");row.SetAttributeValue("grnConfirmedBy",Name(ctx));
    return Render(row);
}));
api.MapPost("/deliveries",async(NewDispatch req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"delivery.dispatch",["Owner"],d=>{
    var root=d.Root!;var bill=PilotStore.ById(root.Element("bills")!,req.BillId)??throw new InvalidOperationException("Create sale invoice first");
    if(PilotStore.Attr(bill,"status")!="Created - pending dispatch")throw new InvalidOperationException("Bill already dispatched");
    var product=PilotStore.ById(root.Element("products")!,req.ProductId)??throw new InvalidOperationException("Unknown SKU");
    if(req.Quantity<=0||Available(root,req.ProductId)<req.Quantity)throw new InvalidOperationException("Insufficient stock - receive goods / count opening stock first");
    root.Element("stock")!.Add(PilotStore.Obj("movement",("id",PilotStore.Id()),("productId",req.ProductId),
        ("product",PilotStore.Attr(product,"name")),("qty",-req.Quantity),("reason","Dispatch"),("at",DateTime.UtcNow)));
    var row=PilotStore.Obj("delivery",("id",PilotStore.Id()),("billId",req.BillId),("retailerId",PilotStore.Attr(bill,"retailerId")),
        ("retailer",PilotStore.Attr(bill,"retailer")),("billNo",PilotStore.Attr(bill,"billNo")),("product",PilotStore.Attr(product,"name")),
        ("qty",req.Quantity),("status","Dispatched"),("receiver",""));
    root.Element("deliveries")!.Add(row);bill.SetAttributeValue("status","Dispatched");return Render(row);
}));
api.MapPost("/deliveries/{id}/confirm",async(string id,POD req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"delivery.pod",["Owner","Salesperson","Retailer"],d=>{
    var row=PilotStore.ById(d.Root!.Element("deliveries")!,id)??throw new InvalidOperationException("Delivery not found");
    if(Role(ctx)=="Retailer" && PilotStore.Attr(row,"retailer")!="Kumar Maligai")throw new InvalidOperationException("Not your order");
    if(PilotStore.Attr(row,"status")!="Dispatched"||string.IsNullOrWhiteSpace(req.Receiver))throw new InvalidOperationException("Receiver and dispatched delivery required");
    if(req.Accepted<0||req.Accepted>PilotStore.Dec(row,"qty"))throw new InvalidOperationException("Invalid accepted quantity");
    row.SetAttributeValue("status",req.Accepted==PilotStore.Dec(row,"qty")?"Delivered":"Partial delivery - exception");
    row.SetAttributeValue("accepted",PilotStore.Money(req.Accepted));row.SetAttributeValue("receiver",req.Receiver.Trim());
    row.SetAttributeValue("confirmedAt",DateTime.UtcNow.ToString("O"));return Render(row);
}));
api.MapPost("/collections",async(NewCollection req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"collection.record",["Owner","Salesperson"],d=>{
    var root=d.Root!;var bill=PilotStore.ById(root.Element("bills")!,req.BillId)??throw new InvalidOperationException("Only verified posted bills may receive demo payments");
    var all=root.Element("collections")!;if(string.IsNullOrWhiteSpace(req.IdempotencyKey)||req.IdempotencyKey.Length>100)throw new InvalidOperationException("Payment request ID required");
    var existing=all.Elements().FirstOrDefault(x=>PilotStore.Attr(x,"key")==req.IdempotencyKey);
    if(existing!=null){if(PilotStore.Attr(existing,"billId")!=req.BillId||PilotStore.Dec(existing,"amount")!=req.Amount)throw new InvalidOperationException("Payment key reused with different values");return Render(existing);}
    var due=PilotStore.Dec(bill,"amount")-all.Elements().Where(x=>PilotStore.Attr(x,"billId")==req.BillId&&PilotStore.Attr(x,"status")!="Reversed").Sum(x=>PilotStore.Dec(x,"amount"));
    if(req.Amount<=0||decimal.Round(req.Amount,2)!=req.Amount||req.Amount>due)throw new InvalidOperationException("Amount exceeds uncollected invoice balance");
    if(req.Mode is not ("Cash" or "UPI" or "Bank" or "Cheque"))throw new InvalidOperationException("Payment mode is mandatory");
    var row=PilotStore.Obj("receipt",("id",PilotStore.Id()),("billId",req.BillId),("billNo",PilotStore.Attr(bill,"billNo")),
        ("retailerId",PilotStore.Attr(bill,"retailerId")),("retailer",PilotStore.Attr(bill,"retailer")),
        ("amount",req.Amount),("mode",req.Mode),("status",req.Mode=="Cash"?"Cash pending handover":"Pending bank verification"),
        ("by",Name(ctx)),("key",req.IdempotencyKey),("at",DateTime.UtcNow));
    all.Add(row);return Render(row);
}));
api.MapPost("/collections/{id}/bank-verify",async(string id,VerifiedBank req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"bank.record-evidence",["Owner"],d=>{
    var row=PilotStore.ById(d.Root!.Element("collections")!,id)??throw new InvalidOperationException("Unknown receipt");
    if(PilotStore.Attr(row,"status")!="Pending bank verification"||string.IsNullOrWhiteSpace(req.Reference)||string.IsNullOrWhiteSpace(req.ReviewerNote))throw new InvalidOperationException("Bank evidence and reviewer note required - screenshots alone do not prove payment");
    row.SetAttributeValue("status","Cleared");row.SetAttributeValue("bankRef",req.Reference.Trim());row.SetAttributeValue("reviewedBy",Name(ctx));return Render(row);
}));
api.MapPost("/cashclose",async(NewCash req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"cash.submit",["Owner","Salesperson"],d=>{
    var group=d.Root!.Element("collections")!;
    var pending=group.Elements().Where(x=>PilotStore.Attr(x,"status")=="Cash pending handover"&&PilotStore.Attr(x,"by")==Name(ctx)).ToList();
    if(!pending.Any())throw new InvalidOperationException("No cash receipts waiting for this user");
    if(req.Counted<0||string.IsNullOrWhiteSpace(req.Date))throw new InvalidOperationException("Business date and counted notes required");
    if(req.Notes is null||req.Notes.Any(x=>x.Value<0||!new[]{500,200,100,50,20,10,5,2,1}.Contains(x.Key))
        ||req.Notes.Sum(x=>x.Key*(decimal)x.Value)!=req.Counted)
        throw new InvalidOperationException("Denomination totals must equal counted cash");
    var expected=pending.Sum(x=>PilotStore.Dec(x,"amount"));var difference=req.Counted-expected;
    if(difference!=0&&string.IsNullOrWhiteSpace(req.Reason))throw new InvalidOperationException("Explain cash difference before submitting");
    var row=PilotStore.Obj("close",("id",PilotStore.Id()),("by",Name(ctx)),("date",req.Date),
        ("expected",expected),("counted",req.Counted),("difference",difference),("reason",req.Reason),
        ("denominations",JsonSerializer.Serialize(req.Notes)),
        ("receiptIds",string.Join(',',pending.Select(x=>PilotStore.Attr(x,"id")))),("status","Awaiting owner"));
    d.Root.Element("cashcloses")!.Add(row);return Render(row);
}));
api.MapPost("/cashclose/{id}/approve",async(string id,ApprovalReason req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"cash.approve",["Owner"],d=>{
    var root=d.Root!;var row=PilotStore.ById(root.Element("cashcloses")!,id)??throw new InvalidOperationException("Unknown cash close");
    if(PilotStore.Attr(row,"status")!="Awaiting owner"||PilotStore.Dec(row,"difference")!=0&&string.IsNullOrWhiteSpace(req.Reason))throw new InvalidOperationException("Discrepancy reason required");
    var ids=PilotStore.Attr(row,"receiptIds").Split(',',StringSplitOptions.RemoveEmptyEntries);
    var receipts=ids.Select(x=>PilotStore.ById(root.Element("collections")!,x)).ToList();
    if(receipts.Any(x=>x is null||PilotStore.Attr(x,"status")!="Cash pending handover"))throw new InvalidOperationException("Some receipts have already changed");
    foreach(var item in receipts)item!.SetAttributeValue("status","Cleared");
    row.SetAttributeValue("status","Owner approved");row.SetAttributeValue("approvedBy",Name(ctx));return Render(row);
}));
api.MapPost("/supplierpayments",async(NewPayment req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"supplierpayment.prepare",["Owner"],d=>{
    var purchase=PilotStore.ById(d.Root!.Element("purchases")!,req.PurchaseId)??throw new InvalidOperationException("Unknown supplier invoice");
    if(PilotStore.Attr(purchase,"status")!="Received")throw new InvalidOperationException("Supplier invoice requires approved goods receipt before payable demo");
    var balance=PilotStore.Dec(purchase,"amount")-PilotStore.Dec(purchase,"paid");
    if(req.Amount<=0||req.Amount>balance||string.IsNullOrWhiteSpace(req.ExternalReference))throw new InvalidOperationException("Amount or external bank reference invalid");
    var row=PilotStore.Obj("payment",("id",PilotStore.Id()),("purchaseId",req.PurchaseId),("supplier",PilotStore.Attr(purchase,"supplier")),
        ("amount",req.Amount),("reference",req.ExternalReference.Trim()),("preparedBy",Name(ctx)),("status","Owner review required"));
    d.Root.Element("payments")!.Add(row);return Render(row);
}));
api.MapPost("/supplierpayments/{id}/approve",async(string id,ApprovalReason req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"supplierpayment.approve",["Owner"],d=>{
    var root=d.Root!;var row=PilotStore.ById(root.Element("payments")!,id)??throw new InvalidOperationException("Unknown payment");
    if(PilotStore.Attr(row,"status")!="Owner review required")throw new InvalidOperationException("Already reviewed");
    if(PilotStore.Attr(row,"preparedBy")==Name(ctx) && (req.Reason?.Trim().Length??0)<10)
        throw new InvalidOperationException("Single-owner pilot: record a 10+ character independent bank evidence review note before self-approval");
    var bill=PilotStore.ById(root.Element("purchases")!,PilotStore.Attr(row,"purchaseId"))!;
    var amount=PilotStore.Dec(row,"amount");
    if(amount<=0||amount>PilotStore.Dec(bill,"amount")-PilotStore.Dec(bill,"paid"))
        throw new InvalidOperationException("Recorded supplier payment exceeds the remaining invoice balance");
    bill.SetAttributeValue("paid",PilotStore.Dec(bill,"paid")+amount);
    row.SetAttributeValue("status","Recorded - evidence reviewed");row.SetAttributeValue("approvedBy",Name(ctx));
    row.SetAttributeValue("approvalEvidence",req.Reason?.Trim()??"");
    return Render(row);
}));
api.MapPost("/returns",async(NewReturn req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"return.request",["Owner","Salesperson"],d=>{
    if(req.Qty<=0||string.IsNullOrWhiteSpace(req.Reason))throw new InvalidOperationException("Return quantity and reason required");
    var row=PilotStore.Obj("return",("id",PilotStore.Id()),("productId",req.ProductId),("qty",req.Qty),
        ("reason",req.Reason.Trim()),("status","Quarantine review"));d.Root!.Element("returns")!.Add(row);return Render(row);
}));
api.MapPost("/products/{id}/opening",async(string id,OpenStock req,HttpContext ctx,PilotStore s)=>await Change(ctx,s,"stock.opening",["Owner"],d=>{
    var root=d.Root!;var product=PilotStore.ById(root.Element("products")!,id)??throw new InvalidOperationException("Unknown product");
    if(req.Quantity<0||string.IsNullOrWhiteSpace(req.Batch))throw new InvalidOperationException("Count and batch required");
    if(root.Element("stock")!.Elements().Any(x=>PilotStore.Attr(x,"productId")==id&&PilotStore.Attr(x,"reason")=="Opening count"))throw new InvalidOperationException("Opening count already recorded. Use approved adjustment workflow.");
    var row=PilotStore.Obj("movement",("id",PilotStore.Id()),("productId",id),("product",PilotStore.Attr(product,"name")),
        ("batch",req.Batch),("qty",req.Quantity),("reason","Opening count"),("at",DateTime.UtcNow));
    root.Element("stock")!.Add(row);return Render(row);
}));
api.MapGet("/report",async(HttpContext ctx,PilotStore s)=>InRole(ctx,"Owner")?Results.Ok(await s.Read(d=>{
    var r=d.Root!;var invoices=r.Element("bills")!.Elements().ToList();var receipts=r.Element("collections")!.Elements().ToList();
    return new{postedSales=invoices.Sum(x=>PilotStore.Dec(x,"amount")),clearedCollections=receipts.Where(x=>PilotStore.Attr(x,"status")=="Cleared").Sum(x=>PilotStore.Dec(x,"amount")),
        cashAwaitingHandover=receipts.Where(x=>PilotStore.Attr(x,"status")=="Cash pending handover").Sum(x=>PilotStore.Dec(x,"amount")),
        note="Only pilot-entered bills/receipts; historical PDF figures are not posted to these balances."};
})):Results.Forbid());
app.MapFallbackToFile("index.html");
app.Run();

static string Name(HttpContext c)=>c.User.Identity?.Name??"";
static string Role(HttpContext c)=>c.User.FindFirst(ClaimTypes.Role)?.Value??"";
static bool InRole(HttpContext c,params string[] roles)=>roles.Contains(Role(c));
static bool Csrf(HttpContext c)=>
    !string.IsNullOrWhiteSpace(c.User.FindFirst("csrf")?.Value) &&
    CryptographicOperations.FixedTimeEquals(
      System.Text.Encoding.UTF8.GetBytes(c.User.FindFirst("csrf")!.Value),
      System.Text.Encoding.UTF8.GetBytes(c.Request.Headers["X-CSRF"].ToString().PadRight(64).Substring(0,64)));
static Dictionary<string,string> Render(XElement row)=>row.Attributes().ToDictionary(x=>x.Name.LocalName,x=>x.Value);
static List<Dictionary<string,string>> Rows(XElement root,string name)=>root.Element(name)!.Elements().Select(Render).ToList();
static string Get(Dictionary<string,string>? row,string name)=>row?.GetValueOrDefault(name)??"";
static decimal Value(Dictionary<string,string> row,string name)=>decimal.TryParse(Get(row,name),NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n:0m;
static decimal StockQty(List<Dictionary<string,string>> stock,string productId)=>stock.Where(x=>Get(x,"productId")==productId).Sum(x=>Value(x,"qty"));
static decimal Available(XElement root,string productId)=>root.Element("stock")!.Elements()
    .Where(x=>PilotStore.Attr(x,"productId")==productId).Sum(x=>PilotStore.Dec(x,"qty"));
static async Task<IResult> Change(HttpContext ctx,PilotStore s,string action,string[] roles,Func<XDocument,object> mutate)
{
    if(!InRole(ctx,roles))return Results.Forbid();
    try { return Results.Ok(await s.Write(Name(ctx),action,mutate)); }
    catch(InvalidOperationException e){return Results.BadRequest(new{error=e.Message});}
}
public record Login(string Username,string Password);
public record NewRetailer(string Name,string Beat);
public record NewOrder(string? RetailerId,string ProductId,decimal Quantity,string? Note);
public record OrderDecision(bool Approve,string? Note);
public record NewBill(string RetailerId,string BillNo,decimal Amount,string DueDate,string? OrderId=null);
public record Review(bool Approve,string? Note);
public record NewGrn(string PurchaseId,string ProductId,decimal Quantity,decimal Damaged,string Batch,string? Expiry);
public record NewDispatch(string BillId,string ProductId,decimal Quantity);
public record POD(string Receiver,decimal Accepted);
public record NewCollection(string BillId,decimal Amount,string Mode,string IdempotencyKey);
public record VerifiedBank(string Reference,string ReviewerNote);
public record NewCash(string Date,decimal Counted,string? Reason, Dictionary<int,int>? Notes);
public record ApprovalReason(string? Reason);
public record NewPayment(string PurchaseId,decimal Amount,string ExternalReference);
public record NewReturn(string ProductId,decimal Qty,string Reason);
public record OpenStock(decimal Quantity,string Batch);
