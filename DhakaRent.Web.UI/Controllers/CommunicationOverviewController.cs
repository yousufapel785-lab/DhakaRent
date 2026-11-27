using DhakaRent.Web.UI.Services;
using DhakaRent.Web.UI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Controllers;

[Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CommunicationOverviewController(CommunicationDirectory directory) : Controller
{
    public async Task<IActionResult> Index(ContactKind kind = ContactKind.Conversations, int page = 1)
    {
        const int pageSize = 25;
        if (!ModelState.IsValid || !Enum.IsDefined(kind) || page < 1 || page > int.MaxValue / pageSize)
            return BadRequest();
        var rows = await directory.Query(kind).OrderByDescending(c => c.ActivityUtc).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync();
        var hasNext = rows.Count > pageSize;
        if (hasNext) rows.RemoveAt(pageSize);
        return View(new ContactOverview(kind, rows, page, hasNext));
    }
}
