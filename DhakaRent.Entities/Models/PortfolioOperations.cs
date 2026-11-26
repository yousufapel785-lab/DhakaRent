using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DhakaRent.Entities.Models;

public class BuildingStaffAssignment
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;
    [MaxLength(450)] public string StaffUserId { get; set; } = "";
    public bool CanReadAccounts { get; set; }
    public bool CanManageRepairs { get; set; }
}

public class OperatingExpense
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;
    public DateOnly IncurredOn { get; set; }
    [MaxLength(160)] public string Description { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
}

public class AuditEvent
{
    public long Id { get; set; }
    [MaxLength(450)] public string ActorId { get; set; } = "";
    [MaxLength(80)] public string Action { get; set; } = "";
    [MaxLength(100)] public string Resource { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

public class SharedCharge
{
    public int Id { get; set; }
    public int OperatingExpenseId { get; set; }
    public int LeaseAgreementId { get; set; }
    public DateOnly Month { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
}

public record MeterReading(decimal Previous, decimal Current, decimal Rate);
