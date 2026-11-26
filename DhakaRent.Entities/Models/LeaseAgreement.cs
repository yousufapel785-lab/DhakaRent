using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DhakaRent.Entities.Models;

public class LeaseAgreement
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public RentalUnit Unit { get; set; } = null!;
    [MaxLength(450)] public string TenantId { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? AgreedUntil { get; set; }
    [ConcurrencyCheck] public int TermRevision { get; set; }
    [ConcurrencyCheck] public DateOnly? NextRentInvoiceMonth { get; set; }
    [ConcurrencyCheck, MaxLength(500)] public string? DepositObligations { get; set; }
    public int DueDay { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MonthlyRent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Deposit { get; set; }
    public List<RentInvoice> Invoices { get; set; } = [];
    public List<MaintenanceTicket> Tickets { get; set; } = [];
}

public class RentInvoice
{
    public int Id { get; set; }
    public int LeaseAgreementId { get; set; }
    public LeaseAgreement Lease { get; set; } = null!;
    public DateOnly Month { get; set; }
    public DateOnly DueDate { get; set; }
    [ConcurrencyCheck] public Guid Revision { get; set; } = Guid.NewGuid();
    public List<InvoiceCharge> Charges { get; set; } = [];
    public List<ManualPayment> Payments { get; set; } = [];
    [NotMapped] public decimal Balance => Charges.Sum(c => c.Amount) - Payments.Sum(p => p.Amount);
}

public class InvoiceCharge
{
    public int Id { get; set; }
    [MaxLength(80)] public string Description { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
}

public class ManualPayment
{
    public int Id { get; set; }
    public int RentInvoiceId { get; set; }
    public RentInvoice Invoice { get; set; } = null!;
    [MaxLength(450)] public string ConfirmedBy { get; set; } = "";
    public DateTime ConfirmedAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string Reference { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
}

public class MaintenanceTicket
{
    public int Id { get; set; }
    public int LeaseAgreementId { get; set; }
    [MaxLength(1500)] public string Description { get; set; } = "";
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    [ConcurrencyCheck] public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Expense { get; set; }
}

public class RentalNotice
{
    public int Id { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = "";
    [MaxLength(100)] public string EventKey { get; set; } = "";
    [MaxLength(250)] public string Message { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
