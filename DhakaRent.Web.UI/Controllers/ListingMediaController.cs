using System.Security.Claims;
using DhakaRent.DataAccess.Context;
using DhakaRent.Entities.Enums;
using DhakaRent.Entities.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Controllers;

public class ListingMediaController(ApplicationDbContext db) : Controller
{
    [HttpGet("/listing-media/{id:int}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Show(int id)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var admin = User.IsInRole("Admin");
        var media = await db.Set<ListingMedia>().AsNoTracking()
            .Where(m => m.Id == id && (admin || m.Property.OwnerId == ownerId ||
                m.Property.ListingStatus == ListingStatus.Approved && (m.Property.Unit == null ||
                    m.Property.Unit.OccupancyStatus == UnitOccupancyStatus.Vacant && !m.Property.Unit.Floor.Building.IsArchived)))
            .Select(m => new { m.ContentType, m.Content.Bytes }).SingleOrDefaultAsync();
        if (media == null) return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        if (media.ContentType == "application/pdf")
            return File(media.Bytes, media.ContentType, $"floor-plan-{id}.pdf");
        return File(media.Bytes, media.ContentType, enableRangeProcessing: true);
    }
}
