using DhakaRent.DataAccess.Context;
using DhakaRent.Web.UI.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Services;

public class CommunicationDirectory(ApplicationDbContext db)
{
    // Only project the metadata used by the directory. Messages and inquiry text stay in their participant-only views.
    public IQueryable<ListingContact> Query(ContactKind kind)
    {
        var references = kind switch
        {
            ContactKind.Conversations =>
                from c in db.Conversations
                join i in db.PropertyInquiries on c.PropertyInquiryId equals i.Id
                select new { c.Id, i.PropertyId, c.TenantId, c.OwnerId, Activity = c.LastMessageAt ?? c.CreatedAt },
            ContactKind.Inquiries =>
                from i in db.PropertyInquiries
                join p in db.Properties on i.PropertyId equals p.Id
                select new { i.Id, i.PropertyId, i.TenantId, p.OwnerId, Activity = i.UpdatedAt ?? i.CreatedAt },
            ContactKind.Viewings =>
                from v in db.ViewingRequests
                join p in db.Properties on v.PropertyId equals p.Id
                select new { v.Id, v.PropertyId, v.TenantId, p.OwnerId, Activity = v.UpdatedAt ?? v.CreatedAt },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return from r in references
               join p in db.Properties on r.PropertyId equals p.Id
               join tenant in db.Users on r.TenantId equals tenant.Id
               join owner in db.Users on r.OwnerId equals owner.Id
               select new ListingContact
               {
                   Id = r.Id, PropertyId = p.Id, Title = p.Title, ActivityUtc = r.Activity,
                   Tenant = new ContactPerson("Tenant", tenant.FullName, tenant.Email),
                   Owner = new ContactPerson("Property owner", owner.FullName, owner.Email)
               };
    }

    public Task<Dictionary<int, ListingContact>> ForRecordsAsync(ContactKind kind, IEnumerable<int> authorizedIds)
    {
        var ids = authorizedIds.Distinct().ToArray();
        return Query(kind).Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
    }
}
