using System.Security.Claims;
using DhakaRent.DataAccess.Context;
using DhakaRent.DataAccess.Identity;
using DhakaRent.Entities.Enums;
using DhakaRent.Entities.Models;
using DhakaRent.Web.UI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Authorize(Roles = "PropertyOwner"), AutoValidateAntiforgeryToken]
public class PortfolioController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<Building> Buildings => db.Buildings.Where(b => b.OwnerId == Actor);

    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(6));
        var period = new DateOnly(today.Year, today.Month, 1);
        var fromUtc = DateTime.SpecifyKind(period.ToDateTime(TimeOnly.MinValue).AddHours(-6), DateTimeKind.Utc);
        var untilUtc = fromUtc.AddMonths(1);
        var buildings = await Buildings.AsNoTracking().OrderBy(b => b.Name).Select(b => new
        {
            b.Id,
            b.Name,
            Units = b.Floors.SelectMany(f => f.Units).Count(),
            Vacant = b.Floors.SelectMany(f => f.Units).Count(u => u.OccupancyStatus == UnitOccupancyStatus.Vacant),
            Occupied = b.Floors.SelectMany(f => f.Units).Count(u => u.OccupancyStatus == UnitOccupancyStatus.Occupied)
        }).ToListAsync();
        var leases = await db.Leases.AsNoTracking().Where(l => l.Unit.Floor.Building.OwnerId == Actor)
            .Select(l => new { l.Unit.Floor.BuildingId, l.MonthlyRent, Active = l.EndDate == null, Open = l.Tickets.Count(t => t.CompletedAtUtc == null), Repair = l.Tickets.Sum(t => (decimal?)t.Expense) ?? 0m }).ToListAsync();
        var invoices = await db.Invoices.AsNoTracking().Where(i => i.Lease.Unit.Floor.Building.OwnerId == Actor)
            .Select(i => new
            {
                i.Lease.Unit.Floor.BuildingId,
                i.DueDate,
                ReceivedThisMonth = i.Payments.Where(p => p.ConfirmedAtUtc >= fromUtc && p.ConfirmedAtUtc < untilUtc).Sum(p => (decimal?)p.Amount) ?? 0m,
                Total = i.Charges.Sum(c => c.Amount),
                Paid = i.Payments.Sum(p => (decimal?)p.Amount) ?? 0m
            }).ToListAsync();
        var expenses = await db.OperatingExpenses.Where(e => e.Building.OwnerId == Actor).GroupBy(e => e.BuildingId)
            .Select(g => new { Id = g.Key, Total = g.Sum(e => e.Amount) }).ToDictionaryAsync(g => g.Id, g => g.Total);
        var summaries = buildings.Select(b =>
        {
            var bills = invoices.Where(i => i.BuildingId == b.Id).ToList();
            var agreements = leases.Where(l => l.BuildingId == b.Id).ToList();
            var billed = bills.Sum(i => i.Total);
            var paid = bills.Sum(i => i.Paid);
            return new BuildingSummary(b.Id, b.Name, b.Units, b.Occupied, agreements.Count(l => l.Active), billed, paid, billed - paid,
                bills.Where(i => i.DueDate < today).Sum(i => i.Total - i.Paid), expenses.GetValueOrDefault(b.Id) + agreements.Sum(l => l.Repair), agreements.Sum(l => l.Open), b.Vacant, agreements.Where(l => l.Active).Sum(l => l.MonthlyRent), bills.Sum(i => i.ReceivedThisMonth));
        }).ToList();
        var staff = await (from a in db.BuildingStaff
                           where a.Building.OwnerId == Actor && (a.CanManageRepairs || a.CanReadAccounts)
                           join u in db.Users on a.StaffUserId equals u.Id
                           select new StaffAccess(a.Id, a.Building.Name, u.FullName, a.CanManageRepairs, a.CanReadAccounts)).ToListAsync();
        var costs = await db.OperatingExpenses.Where(e => e.Building.OwnerId == Actor &&
            !db.Set<SharedCharge>().Any(s => s.OperatingExpenseId == e.Id))
            .OrderByDescending(e => e.Id).Select(e => new UnallocatedExpense(e.Id, e.Description, e.Amount)).ToListAsync();
        return View(new PortfolioOverview(summaries, staff, costs));
    }

    [HttpPost]
    public async Task<IActionResult> Expense(int buildingId, DateOnly incurredOn, string description, decimal amount)
    {
        if (!ModelState.IsValid || incurredOn == default || incurredOn > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(6)) ||
            string.IsNullOrWhiteSpace(description) || description.Length > 160 || amount <= 0 || amount > 999999999 || decimal.Round(amount, 2) != amount)
            return Error("Check the expense date, description and amount.");
        if (!await Buildings.AnyAsync(b => b.Id == buildingId)) return NotFound();
        db.OperatingExpenses.Add(new() { BuildingId = buildingId, IncurredOn = incurredOn, Description = description.Trim(), Amount = amount });
        return await Save("Expense recorded", buildingId);
    }

    [HttpPost]
    public async Task<IActionResult> Assign(int buildingId, string email, bool repairs, bool accounts)
    {
        if (!repairs && !accounts) return Error("Select at least one permission.");
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(email)) return Error("Enter a registered staff email.");
        if (!await Buildings.AnyAsync(b => b.Id == buildingId && !b.IsArchived)) return NotFound();
        var staff = await users.FindByEmailAsync(email.Trim());
        if (staff == null || staff.Id == Actor) return Error("Choose another registered user.");
        var assignment = await db.BuildingStaff.SingleOrDefaultAsync(a => a.BuildingId == buildingId && a.StaffUserId == staff.Id);
        if (assignment == null)
        {
            assignment = new() { BuildingId = buildingId, StaffUserId = staff.Id };
            db.BuildingStaff.Add(assignment);
        }
        assignment.CanManageRepairs = repairs;
        assignment.CanReadAccounts = accounts;
        return await Save("Building permissions saved", buildingId);
    }

    [HttpPost]
    public async Task<IActionResult> Revoke(int id)
    {
        var assignment = await db.BuildingStaff.SingleOrDefaultAsync(a => a.Id == id && a.Building.OwnerId == Actor);
        if (assignment == null) return NotFound();
        assignment.CanManageRepairs = false;
        assignment.CanReadAccounts = false;
        return await Save("Building permissions revoked", assignment.BuildingId);
    }

    [HttpPost]
    public async Task<IActionResult> Allocate(int expenseId, string month)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var period) || period.Year >= 9999)
            return Error("Select a valid billing month.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        var expense = await db.OperatingExpenses.SingleOrDefaultAsync(e => e.Id == expenseId && e.Building.OwnerId == Actor);
        if (expense == null) return NotFound();
        var next = period.AddMonths(1);
        var leases = await db.Leases.Where(l => l.Unit.Floor.BuildingId == expense.BuildingId && l.StartDate < next &&
            (l.EndDate == null || l.EndDate >= period)).OrderBy(l => l.Id).Select(l => l.Id).ToListAsync();
        if (leases.Count == 0 || await db.Set<SharedCharge>().AnyAsync(s => s.OperatingExpenseId == expenseId) ||
            await db.Invoices.AnyAsync(i => leases.Contains(i.LeaseAgreementId) && i.Month == period))
            return Error("Choose an unallocated expense and a month with leases and no issued invoices.");
        var cents = (long)(expense.Amount * 100);
        for (int i = 0; i < leases.Count; i++)
            db.Add(new SharedCharge
            {
                OperatingExpenseId = expenseId,
                LeaseAgreementId = leases[i],
                Month = period,
                Amount = (cents / leases.Count + (i < cents % leases.Count ? 1 : 0)) / 100m
            });
        db.AuditEvents.Add(new() { ActorId = Actor, Action = "Expense allocated equally", Resource = $"Expense/{expenseId}" });
        await db.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        return RedirectToAction(nameof(Index));
    }

    private IActionResult Error(string message) { TempData["PortfolioError"] = message; return RedirectToAction(nameof(Index)); }
    private async Task<IActionResult> Save(string action, int buildingId)
    {
        db.AuditEvents.Add(new() { ActorId = Actor, Action = action, Resource = $"Building/{buildingId}" });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Error("The record changed. Refresh the portfolio and try again."); }
        TempData["SuccessMessage"] = action + ".";
        return RedirectToAction(nameof(Index));
    }
}

[Authorize, AutoValidateAntiforgeryToken]
public class AssignedRepairsController(ApplicationDbContext db) : Controller
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<LeaseAgreement> Assigned => db.Leases.Where(l => db.BuildingStaff.Any(a =>
        a.StaffUserId == Actor && a.CanManageRepairs && a.BuildingId == l.Unit.Floor.BuildingId && !a.Building.IsArchived));

    public async Task<IActionResult> Index() => View(await (from l in Assigned
                                                            join t in db.Set<MaintenanceTicket>() on l.Id equals t.LeaseAgreementId
                                                            select new AssignedRepair(t.Id, l.Unit.Floor.Building.Name, l.Unit.UnitNumber, t.Description, t.CompletedAtUtc != null, t.StartedUtc.HasValue)).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Start(int id)
    {
        var ticket = await Assigned.SelectMany(l => l.Tickets).SingleOrDefaultAsync(t => t.Id == id && t.CompletedAtUtc == null && t.StartedUtc == null);
        if (ticket == null) return NotFound();
        ticket.StartedUtc = DateTime.UtcNow;
        db.AuditEvents.Add(new() { ActorId = Actor, Action = "Repair started", Resource = $"Ticket/{id}" });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Complete(int id)
    {
        var t = await Assigned.SelectMany(l => l.Tickets).SingleOrDefaultAsync(t => t.Id == id && t.CompletedAtUtc == null);
        if (t == null) return NotFound();
        t.CompletedAtUtc = DateTime.UtcNow;
        db.AuditEvents.Add(new() { ActorId = Actor, Action = "Repair completed", Resource = $"Ticket/{id}" });
        try { await db.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { return Conflict(); }
        return RedirectToAction(nameof(Index));
    }
}
