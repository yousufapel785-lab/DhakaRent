using DhakaRent.DataAccess.Context;
using DhakaRent.Entities.Enums;
using DhakaRent.Entities.Models;
using DhakaRent.Facade.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.DataAccess.Repositories;

public class RentalOperations(ApplicationDbContext db) : IRentalOperations
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(6));
    private IQueryable<LeaseAgreement> Owned(string id) => db.Leases.Where(l => l.Unit.Floor.Building.OwnerId == id);

    public Task<List<LeaseAgreement>> GetLeasesAsync(string userId, bool owner) =>
        (owner ? Owned(userId) : db.Leases.Where(l => l.TenantId == userId)).AsNoTracking().AsSplitQuery()
        .Include(l => l.Unit.Floor.Building).Include(l => l.Invoices).ThenInclude(i => i.Charges)
        .Include(l => l.Invoices).ThenInclude(i => i.Payments).Include(l => l.Tickets)
        .OrderByDescending(l => l.StartDate).ThenByDescending(l => l.Id).ToListAsync();

    public async Task StartAsync(string ownerId, int unitId, string tenantId, DateOnly start, decimal rent, decimal deposit, int dueDay)
    {
        Money(rent, true);
        Money(deposit);
        var unit = await db.RentalUnits.SingleOrDefaultAsync(u => u.Id == unitId && u.Floor.Building.OwnerId == ownerId &&
            !u.Floor.Building.IsArchived && u.OccupancyStatus == UnitOccupancyStatus.Vacant);
        if (unit == null || start == default || start > Today || dueDay is < 1 or > 28)
            throw new ArgumentException("Choose a vacant unit, a start date up to today and a due day from 1 to 28.");
        if (await db.Leases.AnyAsync(l => l.UnitId == unitId && (l.EndDate == null || l.EndDate >= start)))
            throw new ArgumentException("These dates overlap an existing lease.");
        db.Leases.Add(new LeaseAgreement
        {
            UnitId = unitId,
            TenantId = tenantId,
            StartDate = start,
            MonthlyRent = rent,
            Deposit = deposit,
            DueDay = dueDay
        });
        unit.OccupancyStatus = UnitOccupancyStatus.Occupied;
        await db.SaveChangesAsync();
    }

    public async Task EndAsync(string ownerId, int leaseId, DateOnly end)
    {
        var lease = await Owned(ownerId).Include(l => l.Unit).SingleOrDefaultAsync(l => l.Id == leaseId && l.EndDate == null);
        if (lease == null || end < lease.StartDate || end > Today ||
            await db.Invoices.AnyAsync(i => i.LeaseAgreementId == leaseId && i.Month > end))
            throw new ArgumentException("Choose an active lease and a valid move-out date covering all billed months.");
        lease.EndDate = end;
        lease.NextRentInvoiceMonth = null;
        lease.TermRevision++;
        lease.Unit.OccupancyStatus = UnitOccupancyStatus.Vacant;
        await db.SaveChangesAsync();
    }

    public async Task BillAsync(string ownerId, int leaseId, DateOnly month, decimal water, decimal gas, decimal power, decimal service, MeterReading? meter = null)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        var lease = await Owned(ownerId).SingleOrDefaultAsync(l => l.Id == leaseId);
        month = new DateOnly(month.Year, month.Month, 1);
        if (lease == null || month < new DateOnly(lease.StartDate.Year, lease.StartDate.Month, 1) || month > lease.EndDate || month > Today)
            throw new ArgumentException("Choose a billing month within the lease, up to the current month.");
        if (await db.Invoices.AnyAsync(i => i.LeaseAgreementId == leaseId && i.Month == month))
            throw new ArgumentException("An invoice already exists for this month.");
        var invoice = new RentInvoice { LeaseAgreementId = leaseId, Month = month, DueDate = month.AddDays(lease.DueDay - 1) };
        foreach (var (label, amount) in new[] { ("Rent", lease.MonthlyRent), ("Water", water), ("Gas", gas), ("Electricity", power), ("Service fee", service) })
        {
            Money(amount);
            if (amount > 0) invoice.Charges.Add(new InvoiceCharge { Description = label, Amount = amount });
        }
        if (meter != null)
        {
            Money(meter.Previous);
            Money(meter.Current);
            Money(meter.Rate, true);
            if (power != 0 || meter.Current < meter.Previous)
                throw new ArgumentException("Use either a fixed electricity charge or a non-decreasing meter reading.");
            var charge = decimal.Round((meter.Current - meter.Previous) * meter.Rate, 2, MidpointRounding.AwayFromZero);
            Money(charge);
            invoice.Charges.Add(new InvoiceCharge { Description = FormattableString.Invariant($"Meter {meter.Previous:0.##} to {meter.Current:0.##} at {meter.Rate:0.##}"), Amount = charge });
        }
        var shares = await db.Set<SharedCharge>().Where(s => s.LeaseAgreementId == leaseId && s.Month == month).ToListAsync();
        foreach (var share in shares)
            invoice.Charges.Add(new InvoiceCharge { Description = $"Shared building expense #{share.OperatingExpenseId}", Amount = share.Amount });
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
    }

    public async Task ConfirmAsync(string ownerId, int invoiceId, decimal amount, string reference)
    {
        Money(amount, true);
        reference = reference?.Trim() ?? "";
        if (reference.Length is < 1 or > 100) throw new ArgumentException("Enter a payment reference of up to 100 characters.");
        var invoice = await db.Invoices.Include(i => i.Charges).Include(i => i.Payments)
            .SingleOrDefaultAsync(i => i.Id == invoiceId && i.Lease.Unit.Floor.Building.OwnerId == ownerId);
        if (invoice == null || amount > invoice.Balance || invoice.Payments.Any(p => p.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Check the balance and use a new reference for each confirmed payment.");
        invoice.Payments.Add(new ManualPayment { ConfirmedBy = ownerId, Amount = amount, Reference = reference });
        invoice.Revision = Guid.NewGuid();
        var tenantId = await db.Leases.Where(l => l.Id == invoice.LeaseAgreementId).Select(l => l.TenantId).SingleAsync();
        db.Add(new RentalNotice
        {
            UserId = tenantId,
            EventKey = $"payment-{invoice.Revision:N}",
            Message = $"Payment of ৳{amount:N2} confirmed for invoice #{invoice.Id}. Your receipt is available."
        });
        await db.SaveChangesAsync();
    }

    public Task<ManualPayment?> ReceiptAsync(string actorId, int paymentId) =>
        db.Payments.AsNoTracking().Include(p => p.Invoice).SingleOrDefaultAsync(p => p.Id == paymentId &&
            (p.Invoice.Lease.TenantId == actorId || p.Invoice.Lease.Unit.Floor.Building.OwnerId == actorId));

    public async Task OpenTicketAsync(string tenantId, int leaseId, string description)
    {
        description = description?.Trim() ?? "";
        var lease = await db.Leases.SingleOrDefaultAsync(l => l.Id == leaseId && l.TenantId == tenantId && l.EndDate == null);
        if (lease == null || description.Length is < 1 or > 1500) throw new ArgumentException("Select your active lease and describe the issue (up to 1500 characters).");
        lease.Tickets.Add(new MaintenanceTicket { Description = description });
        await db.SaveChangesAsync();
    }

    public async Task CloseTicketAsync(string ownerId, int ticketId, decimal expense)
    {
        Money(expense);
        var ticket = await Owned(ownerId).SelectMany(l => l.Tickets).SingleOrDefaultAsync(t => t.Id == ticketId && t.CompletedAtUtc == null);
        if (ticket == null) throw new ArgumentException("This ticket is unavailable or already completed.");
        ticket.CompletedAtUtc = DateTime.UtcNow;
        ticket.Expense = expense;
        await db.SaveChangesAsync();
    }

    private static void Money(decimal value, bool positive = false)
    {
        if (value < 0 || positive && value == 0 || value > 999999999m || decimal.Round(value, 2) != value)
            throw new ArgumentException("Enter a valid BDT amount with up to two decimal places.");
    }
}
