using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Badri.TradeFlow;
public enum TraderRole { Owner, Accounts, Salesman }
public enum PaymentMode { Cash, Upi, Bank, Cheque }
public enum PaymentState { CashAwaitingHandover, BankAwaitingVerification, Cleared, Reversed }
public enum PaperDocumentType { CollectionSheet, SupplierInvoice, DeliverySheet }
public enum DocumentState { Draft, ReviewRequired, Verified, Rejected }
public record Actor(Guid TenantId, string UserId, TraderRole Role);

public static class Rules
{
    public static bool ValidMoney(decimal amount) => amount > 0m && amount == Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    public static PaymentState InitialPaymentState(PaymentMode mode) => mode == PaymentMode.Cash ? PaymentState.CashAwaitingHandover : PaymentState.BankAwaitingVerification;
    public static bool ReducesDue(PaymentState state) => state == PaymentState.Cleared;
    public static decimal CashCount(IReadOnlyDictionary<int, int> denominations)
    {
        if (denominations.Any(x => !new[]{1,2,5,10,20,50,100,200,500}.Contains(x.Key) || x.Value < 0))
            throw new ArgumentException("Unsupported denomination or negative note count");
        return denominations.Sum(x => (decimal)x.Key * x.Value);
    }
    public static string Fingerprint(Guid retailerId, Guid? billId, string? oldBillNo, decimal amount, PaymentMode mode)
    {
        var s = string.Join('|', retailerId, billId?.ToString() ?? "", oldBillNo ?? "", amount.ToString("0.00", CultureInfo.InvariantCulture), mode);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    }
}
public class Retailer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Beat { get; set; } = "";
    public decimal UnspecifiedOpeningDue { get; set; }
}
public class RetailerBill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RetailerId { get; set; }
    public string BillNo { get; set; } = "";
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
}
public class Collection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RetailerId { get; set; }
    public Guid? RetailerBillId { get; set; }
    public string? LegacyBillNo { get; set; }
    public string CollectedBy { get; set; } = "";
    public decimal Amount { get; set; }
    public PaymentMode Mode { get; set; }
    public PaymentState State { get; set; }
    public string? BankReference { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}
public class CashHandover
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SalesmanId { get; set; } = "";
    public DateOnly Date { get; set; }
    public decimal Expected { get; set; }
    public decimal Counted { get; set; }
    public decimal Difference { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Submitted";
    public string? ApprovedBy { get; set; }
    public string ReceiptIdsJson { get; set; } = "[]";
}
public class EarlierCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RetailerId { get; set; }
    public Guid? BillId { get; set; }
    public string? LegacyBillNo { get; set; }
    public decimal Amount { get; set; }
    public string SourceMarker { get; set; } = "HC";
    public string Status { get; set; } = "Unproven";
    public string? DecisionReason { get; set; }
}
public class DocumentDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public PaperDocumentType Type { get; set; }
    public string Filename { get; set; } = "";
    public DocumentState State { get; set; } = DocumentState.Draft;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = "";
    public string Action { get; set; } = "";
    public Guid RecordId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
