using System.Globalization;
using System.Security.Claims;
using System.Text;
using DhakaRent.DataAccess.Identity;
using DhakaRent.Facade.Interfaces;
using DhakaRent.Web.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Authorize(Roles = "PropertyOwner,Tenant")]
[AutoValidateAntiforgeryToken]
public class RentalDeskController(IRentalOperations operations, ListingAssets inventory, UserManager<ApplicationUser> users, DhakaRent.DataAccess.Context.ApplicationDbContext db) : Controller
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index()
    {
        var owner = User.IsInRole("PropertyOwner");
        ViewData["Notices"] = await db.Set<DhakaRent.Entities.Models.RentalNotice>().AsNoTracking()
            .Where(n => n.UserId == Actor).OrderByDescending(n => n.Id).Take(20).Select(n => n.Message).ToListAsync();
        if (owner) ViewData["Units"] = await inventory.OwnerUnitsAsync(Actor);
        return View(await operations.GetLeasesAsync(Actor, owner));
    }

    [HttpPost, Authorize(Roles = "PropertyOwner")]
    public Task<IActionResult> Start(int unitId, string tenantEmail, DateOnly start, decimal rent, decimal deposit, int dueDay) =>
        Change(async () =>
        {
            var tenant = await users.FindByEmailAsync(tenantEmail.Trim());
            if (tenant == null || !await users.IsInRoleAsync(tenant, "Tenant")) throw new ArgumentException("Enter the registered tenant's email.");
            await operations.StartAsync(Actor, unitId, tenant.Id, start, rent, deposit, dueDay);
        });

    [HttpPost, Authorize(Roles = "PropertyOwner")]
    public Task<IActionResult> End(int leaseId, DateOnly end) => Change(() => operations.EndAsync(Actor, leaseId, end));

    [HttpPost, Authorize(Roles = "PropertyOwner")]
    public Task<IActionResult> Bill(int leaseId, string month, decimal water, decimal gas, decimal electricity, decimal service, decimal? previous, decimal? current, decimal? rate) =>
        Change(() =>
        {
            if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var period))
                throw new ArgumentException("Select a valid billing month.");
            DhakaRent.Entities.Models.MeterReading? meter = null;
            if (previous.HasValue || current.HasValue || rate.HasValue)
            {
                if (!previous.HasValue || !current.HasValue || !rate.HasValue)
                    throw new ArgumentException("Enter both readings and the tariff for metered electricity.");
                meter = new(previous.Value, current.Value, rate.Value);
            }
            return operations.BillAsync(Actor, leaseId, period, water, gas, electricity, service, meter);
        });

    [HttpPost, Authorize(Roles = "PropertyOwner")]
    public Task<IActionResult> Confirm(int invoiceId, decimal amount, string reference) =>
        Change(() => operations.ConfirmAsync(Actor, invoiceId, amount, reference));

    [HttpPost, Authorize(Roles = "Tenant")]
    public Task<IActionResult> RequestRepair(int leaseId, string description) =>
        Change(() => operations.OpenTicketAsync(Actor, leaseId, description));

    [HttpPost, Authorize(Roles = "PropertyOwner")]
    public Task<IActionResult> CompleteRepair(int ticketId, decimal expense) =>
        Change(() => operations.CloseTicketAsync(Actor, ticketId, expense));

    [HttpPost, Authorize(Roles = "PropertyOwner")]
    public Task<IActionResult> Terms(int leaseId, DateOnly? agreedUntil, string? depositObligations, bool automaticRent) =>
        Change(async () =>
        {
            var lease = await db.Leases.SingleOrDefaultAsync(l => l.Id == leaseId && l.Unit.Floor.Building.OwnerId == Actor);
            if (lease == null || agreedUntil < lease.StartDate || depositObligations?.Length > 500 ||
                (lease.AgreedUntil.HasValue && (!agreedUntil.HasValue || agreedUntil < lease.AgreedUntil)))
                throw new ArgumentException("Keep the existing end date or select a later one and enter up to 500 characters.");
            if (lease.EndDate.HasValue && agreedUntil != lease.AgreedUntil)
                throw new ArgumentException("An ended lease cannot be renewed.");
            if (lease.AgreedUntil != agreedUntil)
            {
                db.AuditEvents.Add(new() { ActorId = Actor, Action = "Lease term recorded", Resource = $"Lease/{leaseId}/{agreedUntil:yyyy-MM-dd}" });
                lease.AgreedUntil = agreedUntil;
                lease.TermRevision++;
            }
            lease.DepositObligations = depositObligations?.Trim();
            var today = DateTime.UtcNow.AddHours(6);
            lease.NextRentInvoiceMonth = automaticRent && lease.EndDate == null
                ? lease.NextRentInvoiceMonth ?? new DateOnly(today.Year, today.Month, 1) : null;
            await db.SaveChangesAsync();
        });

    [HttpGet]
    public async Task<IActionResult> Receipt(int id)
    {
        var payment = await operations.ReceiptAsync(Actor, id);
        if (payment == null) return NotFound();
        var receipt = $"DhakaRent — confirmed manual payment\nReceipt #{payment.Id}\nInvoice #{payment.RentInvoiceId} ({payment.Invoice.Month:yyyy-MM})\nAmount received: BDT {payment.Amount:N2}\nReference: {payment.Reference}\nConfirmed (UTC): {payment.ConfirmedAtUtc:u}\n";
        return File(Encoding.UTF8.GetBytes(receipt), "text/plain; charset=utf-8", $"receipt-{payment.Id}.txt");
    }

    [HttpGet, Authorize(Roles = "PropertyOwner")]
    public async Task<IActionResult> Export()
    {
        var leases = await operations.GetLeasesAsync(Actor, true);
        var report = new StringBuilder("Building ID,Unit ID,Lease ID,Invoice ID,Month,Billed BDT,Received BDT,Balance BDT\r\n");
        foreach (var l in leases)
            foreach (var i in l.Invoices)
                report.Append(FormattableString.Invariant($"{l.Unit.Floor.BuildingId},{l.UnitId},{l.Id},{i.Id},{i.Month:yyyy-MM},{i.Charges.Sum(c => c.Amount):F2},{i.Payments.Sum(p => p.Amount):F2},{i.Balance:F2}\r\n"));
        return File(Encoding.UTF8.GetBytes(report.ToString()), "text/csv", "rent-report.csv");
    }

    private async Task<IActionResult> Change(Func<Task> change)
    {
        if (!ModelState.IsValid) TempData["DeskError"] = "Check the required fields, dates and amounts.";
        else
        {
            try { await change(); TempData["SuccessMessage"] = "Saved."; }
            catch (ArgumentException e) { TempData["DeskError"] = e.Message; }
            catch (DbUpdateException) { TempData["DeskError"] = "This record could not be saved. Review the latest values before trying again."; }
        }
        return RedirectToAction(nameof(Index));
    }
}
