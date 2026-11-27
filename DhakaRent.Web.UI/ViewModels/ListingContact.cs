namespace DhakaRent.Web.UI.ViewModels;

public enum ContactKind { Conversations, Inquiries, Viewings }

public sealed record ContactPerson(string Role, string Name, string? Email)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Email ?? "Account unavailable" : Name;
}

public sealed class ListingContact
{
    public int Id { get; init; }
    public int PropertyId { get; init; }
    public string Title { get; init; } = "";
    public ContactPerson Tenant { get; init; } = null!;
    public ContactPerson Owner { get; init; } = null!;
    public DateTime ActivityUtc { get; init; }
}

public sealed record ContactOverview(ContactKind Kind, List<ListingContact> Rows, int Page, bool HasNext);
