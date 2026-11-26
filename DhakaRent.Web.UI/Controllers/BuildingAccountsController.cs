using System.Globalization;
using System.Security.Claims;
using System.Text;
using DhakaRent.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Controllers;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class BuildingAccountsController(ApplicationDbContext db) : Controller
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<DhakaRent.Entities.Models.Building> Allowed => db.Buildings.Where(b =>
        b.OwnerId == Actor || !b.IsArchived && db.BuildingStaff.Any(a => a.BuildingId == b.Id && a.StaffUserId == Actor && a.CanReadAccounts));

    public async Task<IActionResult> Index() => View(await Allowed.AsNoTracking().OrderBy(b => b.Name).ToListAsync());

    public async Task<IActionResult> Export(int id)
    {
        if (!await Allowed.AnyAsync(b => b.Id == id)) return NotFound();
        var leases = await (from l in db.Leases
                            where l.Unit.Floor.BuildingId == id
                            join user in db.Users on l.TenantId equals user.Id
                            select new { l.Id, l.Unit.UnitNumber, user.FullName }).ToListAsync();
        var invoices = await db.Invoices.Where(i => i.Lease.Unit.Floor.BuildingId == id).Select(i => new
        {
            i.Id,
            i.LeaseAgreementId,
            i.Month,
            Billed = i.Charges.Sum(c => c.Amount),
            Paid = i.Payments.Sum(p => (decimal?)p.Amount) ?? 0m
        }).ToListAsync();
        var expenses = await db.OperatingExpenses.Where(e => e.BuildingId == id)
            .Select(e => new { e.IncurredOn, e.Description, e.Amount }).ToListAsync();
        var repairs = await db.Leases.Where(l => l.Unit.Floor.BuildingId == id).SelectMany(l => l.Tickets)
            .Where(t => t.CompletedAtUtc != null && t.Expense > 0)
            .Select(t => new { t.CompletedAtUtc, t.Description, t.Expense, t.LeaseAgreementId }).ToListAsync();

        var csv = new StringBuilder("Type,Lease,Unit,Tenant,Date,Description,Billed BDT,Received BDT,Expense BDT\r\n");
        void Row(params object?[] cells) => csv.AppendLine(string.Join(",", cells.Select(cell =>
        {
            var value = Convert.ToString(cell, CultureInfo.InvariantCulture) ?? "";
            if (cell is string && value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        })));
        foreach (var l in leases) Row("Lease", l.Id, l.UnitNumber, l.FullName, "", "", "", "", "");
        foreach (var i in invoices) Row("Invoice", i.LeaseAgreementId, "", "", i.Month.ToString("yyyy-MM"), $"Invoice #{i.Id}", i.Billed, i.Paid, "");
        foreach (var e in expenses) Row("Expense", "", "", "", e.IncurredOn.ToString("yyyy-MM-dd"), e.Description, "", "", e.Amount);
        foreach (var t in repairs) Row("Repair", t.LeaseAgreementId, "", "", t.CompletedAtUtc?.ToString("yyyy-MM-dd"), t.Description, "", "", t.Expense);
        db.AuditEvents.Add(new() { ActorId = Actor, Action = "Building accounts exported", Resource = $"Building/{id}" });
        await db.SaveChangesAsync();
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"building-{id}-accounts.csv");
    }
}
