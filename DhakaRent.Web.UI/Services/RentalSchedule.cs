using DhakaRent.DataAccess.Context;
using DhakaRent.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Services;

public sealed class RentalSchedule(IServiceScopeFactory scopes, ILogger<RentalSchedule> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await RunAsync(db, stoppingToken);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(error, "Scheduled rental processing will retry on the next interval.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal static async Task RunAsync(ApplicationDbContext db, CancellationToken cancellation)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(6));
        var expired = db.Set<PrivateDocument>().Where(d => d.ExpiresUtc <= now);
        if (db.Database.IsRelational())
            await expired.ExecuteDeleteAsync(cancellation);
        else
        {
            db.RemoveRange(await expired.Include(d => d.Content).ToListAsync(cancellation));
            await db.SaveChangesAsync(cancellation);
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellation) : null;
        var leases = await db.Leases.Where(l => l.NextRentInvoiceMonth <= today && l.EndDate == null)
            .OrderBy(l => l.NextRentInvoiceMonth).Take(100).ToListAsync(cancellation);
        foreach (var lease in leases)
        {
            var month = lease.NextRentInvoiceMonth!.Value;
            if (!await db.Invoices.AnyAsync(i => i.LeaseAgreementId == lease.Id && i.Month == month, cancellation))
            {
                var invoice = new RentInvoice
                {
                    LeaseAgreementId = lease.Id,
                    Month = month,
                    DueDate = month.AddDays(lease.DueDay - 1),
                    Charges = [new InvoiceCharge { Description = "Rent", Amount = lease.MonthlyRent }]
                };
                var shares = await db.Set<SharedCharge>().Where(s => s.LeaseAgreementId == lease.Id && s.Month == month).ToListAsync(cancellation);
                invoice.Charges.AddRange(shares.Select(s => new InvoiceCharge { Description = $"Shared building expense #{s.OperatingExpenseId}", Amount = s.Amount }));
                db.Invoices.Add(invoice);
            }
            lease.NextRentInvoiceMonth = month.AddMonths(1);
        }
        await db.SaveChangesAsync(cancellation);

        if (transaction != null) await transaction.CommitAsync(cancellation);

        var invoices = db.Invoices.Where(i =>
            i.Charges.Sum(c => c.Amount) > (i.Payments.Sum(p => (decimal?)p.Amount) ?? 0m));
        foreach (var overdue in new[] { false, true })
        {
            var kind = overdue ? "overdue-" : "due-";
            var bills = await invoices.Where(i => (overdue ? i.DueDate < today : i.DueDate == today) &&
                !db.Set<RentalNotice>().Any(n => n.EventKey == kind + i.Id.ToString()))
                .OrderBy(i => i.Id).Take(200).Select(i => new { i.Id, i.Lease.TenantId }).ToListAsync(cancellation);
            foreach (var bill in bills)
                db.Add(new RentalNotice
                {
                    UserId = bill.TenantId,
                    EventKey = kind + bill.Id,
                    Message = $"Invoice #{bill.Id} {(overdue ? "is overdue" : "is due today")}. Check your outstanding balance."
                });
        }

        var upcoming = today.AddDays(30);
        var agreements = await db.Leases.Where(l => l.EndDate == null && l.AgreedUntil >= today && l.AgreedUntil <= upcoming &&
            !db.Set<RentalNotice>().Any(n => n.EventKey == "expiry-" + l.Id.ToString() + "-" + l.TermRevision.ToString()))
            .OrderBy(l => l.Id).Take(200).Select(l => new { l.Id, l.TenantId, l.TermRevision, l.AgreedUntil }).ToListAsync(cancellation);
        foreach (var lease in agreements)
            db.Add(new RentalNotice
            {
                UserId = lease.TenantId,
                EventKey = $"expiry-{lease.Id}-{lease.TermRevision}",
                Message = $"Lease #{lease.Id} reaches its agreed end date on {lease.AgreedUntil:yyyy-MM-dd}."
            });
        await db.SaveChangesAsync(cancellation);
    }
}
