using System.ComponentModel.DataAnnotations;

namespace DhakaRent.Entities.Models;

public class ListingMedia
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    [MaxLength(32)] public string ContentType { get; set; } = "";
    public ListingMediaContent Content { get; set; } = null!;
}

// Keep binary data out of listing searches, reviews, and metadata queries.
public class ListingMediaContent
{
    [Key] public int ListingMediaId { get; set; }
    public byte[] Bytes { get; set; } = [];
}
