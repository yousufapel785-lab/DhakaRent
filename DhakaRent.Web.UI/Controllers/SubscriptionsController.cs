using System.Globalization;
using System.Security.Claims;
using DhakaRent.DataAccess.Context;
using DhakaRent.DataAccess.Identity;
using DhakaRent.Entities.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Authorize(Roles = "Admin,PropertyOwner"), AutoValidateAntiforgeryToken]
public class SubscriptionsController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index()
    {
        var admin = User.IsInRole("Admin");
        var plans = await db.Set<OwnerSubscription>().AsNoTracking().Include(p => p.Months)
            .Where(p => admin || p.OwnerId == Actor).OrderBy(p => p.Plan).ToListAsync();
        var ids = plans.Select(p => p.OwnerId).ToList();
        ViewData["Owners"] = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email);
        return View(plans);
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Set(string ownerEmail, string plan, int buildings, int units, decimal fee, DateOnly? trialThrough)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(plan) || plan.Length > 100 || buildings < 1 || units < 1 ||
            fee < 0 || fee > 999999999m || decimal.Round(fee, 2) != fee) return Error("Check the plan name, limits and fee.");
        var owner = await users.FindByEmailAsync(ownerEmail.Trim());
        if (owner == null || !await users.IsInRoleAsync(owner, "PropertyOwner")) return Error("Choose a registered property owner.");
        var sub = await db.Set<OwnerSubscription>().FindAsync(owner.Id);
        if (sub == null) { sub = new() { OwnerId = owner.Id }; db.Add(sub); }
        sub.Plan = plan.Trim();
        sub.BuildingLimit = buildings;
        sub.UnitLimit = units;
        sub.MonthlyFee = fee;
        sub.TrialThrough = trialThrough;
        sub.Revision = Guid.NewGuid();
        return await Save("Subscription terms saved", owner.Id);
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Issue(string ownerId, string month)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return Error("Select a billing month.");
        var sub = await db.Set<OwnerSubscription>().FindAsync(ownerId);
        if (sub == null) return NotFound();
        if (await db.Set<SubscriptionMonth>().AnyAsync(m => m.OwnerId == ownerId && m.Month == date))
            return Error("That month has already been billed.");
        db.Add(new SubscriptionMonth { OwnerId = ownerId, Month = date, Plan = sub.Plan, Fee = sub.MonthlyFee });
        return await Save("Subscription invoice issued", ownerId);
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Confirm(int id, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 100) return Error("Enter a payment reference.");
        var month = await db.Set<SubscriptionMonth>().SingleOrDefaultAsync(m => m.Id == id && m.ConfirmedUtc == null);
        if (month == null) return NotFound();
        month.ConfirmedUtc = DateTime.UtcNow;
        month.Reference = reference.Trim();
        return await Save("Subscription payment confirmed", month.OwnerId);
    }

    private IActionResult Error(string message) { TempData["PlanError"] = message; return RedirectToAction(nameof(Index)); }
    private async Task<IActionResult> Save(string action, string ownerId)
    {
        db.AuditEvents.Add(new() { ActorId = Actor, Action = action, Resource = $"Subscription/{ownerId}" });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Error("The subscription changed. Refresh and retry."); }
        return RedirectToAction(nameof(Index));
    }
}
