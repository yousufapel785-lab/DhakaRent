using System.Security.Claims;
using DhakaRent.DataAccess.Context;
using DhakaRent.Entities.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Controllers;

[Authorize, AutoValidateAntiforgeryToken, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DocumentsController(ApplicationDbContext db, IDataProtectionProvider protection, IConfiguration config) : Controller
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IDataProtector Cipher => protection.CreateProtector("DhakaRent.PrivateDocuments.v1");
    private IQueryable<PrivateDocument> Accessible => db.Set<PrivateDocument>().Where(d =>
        d.ExpiresUtc > DateTime.UtcNow && (d.TenantId == Actor ||
        d.Purpose == DocumentPurpose.TenantProfile && User.IsInRole("Admin") ||
        d.Purpose == DocumentPurpose.LeaseAgreement && d.Lease!.Unit.Floor.Building.OwnerId == Actor));
    private int Retention => config.GetValue<int>("PrivateDocuments:RetentionDays");
    private string? Policy => config["PrivateDocuments:PolicyVersion"];
    private bool UploadEnabled => Retention is >= 1 and <= 3650 && !string.IsNullOrWhiteSpace(Policy) && Policy.Length <= 100;

    public async Task<IActionResult> Index()
    {
        ViewData["UploadsEnabled"] = UploadEnabled;
        ViewData["Retention"] = Retention;
        ViewData["Policy"] = Policy;
        if (User.IsInRole("PropertyOwner"))
            ViewData["LeaseIds"] = await db.Leases.Where(l => l.Unit.Floor.Building.OwnerId == Actor).Select(l => l.Id).ToListAsync();
        return View(await Accessible.AsNoTracking().OrderByDescending(d => d.SubmittedUtc).ToListAsync());
    }

    [HttpPost, RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> Upload(IFormFile? document, bool consent, int? leaseId)
    {
        if (!UploadEnabled || !consent || document == null || document.Length is < 6 or > 5_000_000)
            return ProblemResult("Confirm the stated use and choose a PDF up to 5 MB when uploads are enabled.");
        string tenant = Actor;
        if (leaseId.HasValue)
        {
            var lease = await db.Leases.SingleOrDefaultAsync(l => l.Id == leaseId && l.Unit.Floor.Building.OwnerId == Actor);
            if (lease == null || !User.IsInRole("PropertyOwner")) return NotFound();
            tenant = lease.TenantId;
        }
        else if (!User.IsInRole("Tenant")) return Forbid();
        if (await db.Set<PrivateDocument>().CountAsync(d => d.SubmittedBy == Actor && d.ExpiresUtc > DateTime.UtcNow) >= 20)
            return ProblemResult("Remove an older upload before adding another.");
        using var buffer = new MemoryStream((int)document.Length);
        await document.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        if (!bytes.AsSpan().StartsWith("%PDF-"u8)) return ProblemResult("Choose a PDF document.");
        var record = new PrivateDocument
        {
            TenantId = tenant,
            SubmittedBy = Actor,
            LeaseAgreementId = leaseId,
            Purpose = leaseId.HasValue ? DocumentPurpose.LeaseAgreement : DocumentPurpose.TenantProfile,
            PolicyVersion = Policy!,
            ExpiresUtc = DateTime.UtcNow.AddDays(Retention),
            Content = new() { Ciphertext = Cipher.Protect(bytes) }
        };
        db.Add(record);
        Log("Document upload consent recorded", leaseId.HasValue ? $"Lease/{leaseId}" : "Tenant profile");
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Download(int id)
    {
        var record = await Accessible.Where(d => d.Id == id).Select(d => d.Content.Ciphertext).SingleOrDefaultAsync();
        if (record == null) return NotFound();
        var plaintext = Cipher.Unprotect(record);
        Log("Private document downloaded", $"Document/{id}");
        await db.SaveChangesAsync();
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(plaintext, "application/pdf", $"document-{id}.pdf");
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Review(int id, DocumentReview decision)
    {
        var record = await Accessible.SingleOrDefaultAsync(d => d.Id == id && d.Purpose == DocumentPurpose.TenantProfile);
        if (record == null) return NotFound();
        if (decision is not (DocumentReview.Accepted or DocumentReview.NeedsReplacement)) return BadRequest();
        record.Review = decision;
        Log($"Internal review {decision}", $"Document/{id}");
        try { await db.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { return Conflict(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id)
    {
        var record = await Accessible.Include(d => d.Content).SingleOrDefaultAsync(d => d.Id == id && d.SubmittedBy == Actor && d.Purpose == DocumentPurpose.TenantProfile);
        if (record == null) return NotFound();
        db.Remove(record);
        Log("Profile document withdrawn", $"Document/{id}");
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private void Log(string action, string resource) => db.AuditEvents.Add(new() { ActorId = Actor, Action = action, Resource = resource });
    private IActionResult ProblemResult(string message) { TempData["DocumentError"] = message; return RedirectToAction(nameof(Index)); }
}
