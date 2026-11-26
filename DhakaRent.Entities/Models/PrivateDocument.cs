using System.ComponentModel.DataAnnotations;

namespace DhakaRent.Entities.Models;

public enum DocumentPurpose { TenantProfile = 1, LeaseAgreement = 2 }
public enum DocumentReview { Submitted = 1, Accepted = 2, NeedsReplacement = 3 }

public class PrivateDocument
{
    public int Id { get; set; }
    [MaxLength(450)] public string TenantId { get; set; } = "";
    [MaxLength(450)] public string SubmittedBy { get; set; } = "";
    public int? LeaseAgreementId { get; set; }
    public LeaseAgreement? Lease { get; set; }
    public DocumentPurpose Purpose { get; set; }
    [ConcurrencyCheck] public DocumentReview Review { get; set; } = DocumentReview.Submitted;
    [MaxLength(100)] public string PolicyVersion { get; set; } = "";
    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
    public PrivateDocumentContent Content { get; set; } = null!;
}

public class PrivateDocumentContent
{
    [Key] public int PrivateDocumentId { get; set; }
    public byte[] Ciphertext { get; set; } = [];
}
