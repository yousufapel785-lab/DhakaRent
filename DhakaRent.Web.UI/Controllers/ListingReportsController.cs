using System.Security.Claims;
using DhakaRent.DataAccess.Context;
using DhakaRent.Entities.Enums;
using DhakaRent.Entities.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Authorize, AutoValidateAntiforgeryToken]
public class ListingReportsController(ApplicationDbContext db) : Controller
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index() =>
        View(await db.PropertyReports.AsNoTracking().Where(r => r.Status == ReportStatus.Pending)
            .OrderBy(r => r.CreatedAt).Take(100).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Submit(int propertyId, string reason, string description)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(reason) || reason.Length > 100 ||
            string.IsNullOrWhiteSpace(description) || description.Length > 2000)
            return BadRequest("Enter a reason and a description of the issue.");
        var listing = await db.Properties.Include(p => p.Unit!).ThenInclude(u => u.Floor.Building)
            .SingleOrDefaultAsync(p => p.Id == propertyId);
        if (listing == null || !listing.IsPubliclyAvailable) return NotFound();
        if (!await db.PropertyReports.AnyAsync(r => r.ReporterId == Actor && r.PropertyId == propertyId && r.Status == ReportStatus.Pending))
        {
            db.PropertyReports.Add(new PropertyReport { ReporterId = Actor, PropertyId = propertyId, Reason = reason.Trim(), Description = description.Trim() });
            await db.SaveChangesAsync();
        }
        TempData["SuccessMessage"] = "Your report is in the moderation queue.";
        return RedirectToAction("Details", "Properties", new { id = propertyId });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Review(int id, ReportStatus decision, string remarks)
    {
        if (!ModelState.IsValid || decision is not (ReportStatus.Resolved or ReportStatus.Dismissed) ||
            string.IsNullOrWhiteSpace(remarks) || remarks.Length > 1000) return BadRequest();
        var report = await db.PropertyReports.SingleOrDefaultAsync(r => r.Id == id && r.Status == ReportStatus.Pending);
        if (report == null) return NotFound();
        report.Status = decision;
        report.AdminRemarks = remarks.Trim();
        report.ReviewedAt = DateTime.UtcNow;
        report.ReviewedByAdminId = Actor;
        db.AuditEvents.Add(new() { ActorId = Actor, Resource = $"Report/{id}", Action = $"Report {decision}" });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
