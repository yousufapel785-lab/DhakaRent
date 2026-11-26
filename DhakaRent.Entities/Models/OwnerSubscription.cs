using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DhakaRent.Entities.Models;

public class OwnerSubscription
{
    [Key, MaxLength(450)] public string OwnerId { get; set; } = "";
    [MaxLength(100)] public string Plan { get; set; } = "";
    public int BuildingLimit { get; set; }
    public int UnitLimit { get; set; }
    public DateOnly? TrialThrough { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MonthlyFee { get; set; }
    [ConcurrencyCheck] public Guid Revision { get; set; } = Guid.NewGuid();
    public List<SubscriptionMonth> Months { get; set; } = [];
}

public class SubscriptionMonth
{
    public int Id { get; set; }
    [MaxLength(450)] public string OwnerId { get; set; } = "";
    [MaxLength(100)] public string Plan { get; set; } = "";
    public DateOnly Month { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Fee { get; set; }
    public DateTime? ConfirmedUtc { get; set; }
    [MaxLength(100)] public string? Reference { get; set; }
}
