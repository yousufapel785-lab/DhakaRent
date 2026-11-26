
using DhakaRent.Entities.Models;
using DhakaRent.Facade.Interfaces;
using DhakaRent.Web.UI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DhakaRent.Entities.Enums;
using DhakaRent.Web.UI.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DhakaRent.Web.UI.Controllers
{
    [Authorize(Roles = "PropertyOwner")]
    public class PropertyOwnerController : Controller
    {
        private readonly IPropertyFacade _propertyFacade;

        private readonly ListingAssets _assets;

        public PropertyOwnerController(
            IPropertyFacade propertyFacade, ListingAssets assets)
        {
            _propertyFacade = propertyFacade;
            _assets = assets;
        }

        // Property Owner Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(ownerId))
            {
                return Forbid();
            }

            // Get properties belonging to the logged-in owner
            var properties = (await _propertyFacade
                .GetPropertiesByOwnerIdAsync(ownerId))
                .ToList();

            // Prepare dashboard data
            var model = new PropertyOwnerDashboardViewModel
            {
                TotalProperties = properties.Count,

                PendingProperties = properties.Count(
                    p => p.ListingStatus == ListingStatus.Pending),

                ApprovedProperties = properties.Count(
                    p => p.ListingStatus == ListingStatus.Approved),

                RejectedProperties = properties.Count(
                    p => p.ListingStatus == ListingStatus.Rejected),


                Properties = properties
    .OrderByDescending(p => p.CreatedAt)
    .Select(p => new PropertyListItemViewModel
    {
        Id = p.Id,
        Title = p.Title,
        Area = p.Area,
        MonthlyRent = p.MonthlyRent,
        ListingStatus = p.ListingStatus.ToString(),
        CreatedAt = p.CreatedAt,
        AvailableFrom = p.AvailableFrom
    })
    .ToList()

            };

            return View(model);
        }

        // Display Add Property Form
        [HttpGet]
        public async Task<IActionResult> CreateProperty()
        {
            ViewData["Units"] = await _assets.OwnerUnitsAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return View(new PropertyCreateViewModel());
        }

        // Process Add Property Form
        [HttpPost, RequestSizeLimit(32_000_000)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProperty(
            PropertyCreateViewModel model)
        {
            var units = await _assets.OwnerUnitsAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            ViewData["Units"] = units;
            if (!model.UnitId.HasValue)
                ModelState.AddModelError(nameof(model.UnitId), "Choose an inventory unit. Add a building, floor and unit first if needed.");
            if (model.UnitId.HasValue && units.All(u => u.Value != model.UnitId.ToString()))
                ModelState.AddModelError(nameof(model.UnitId), "Choose an available unit in your portfolio.");
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Get authenticated owner's ID from Identity
            var ownerId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(ownerId))
            {
                return Forbid();
            }

            // Map ViewModel to database entity
            var property = new Property
            {
                OwnerId = ownerId,
                Latitude = model.Latitude, Longitude = model.Longitude, UnitId = model.UnitId,
                Title = model.Title,
                PropertyType = model.PropertyType,
                Description = model.Description,
                Area = model.Area,
                Address = model.Address,
                SizeInSqFt = model.SizeInSqFt,
                Bedrooms = model.Bedrooms,
                Bathrooms = model.Bathrooms,
                IsFurnished = model.IsFurnished,
                MonthlyRent = model.MonthlyRent,
                AvailableFrom = model.AvailableFrom
            };

            try { property.Media = await ListingAssets.ReadAsync(model.Uploads, 0); }
            catch (InvalidDataException e) { ModelState.AddModelError("", e.Message); return View(model); }
            await _propertyFacade.CreatePropertyAsync(property);

            TempData["SuccessMessage"] =
                "Your property has been submitted for admin approval.";

            return RedirectToAction(nameof(Dashboard));
        }

        [HttpGet]
        public async Task<IActionResult> EditProperty(int id)
        {
            // Step 1: Validate Property ID
            if (id <= 0)
            {
                return NotFound();
            }

            // Step 2: Get current logged-in Owner ID
            var ownerId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(ownerId))
            {
                return Challenge();
            }

            // Step 3: Get existing property from database
            var property = await _propertyFacade
                .GetPropertyByIdAsync(id);

            // Step 4: Check property ownership
            if (property == null || property.OwnerId != ownerId)
            {
                return NotFound();
            }

            // Step 5: Check whether editing is allowed
            if (property.ListingStatus != ListingStatus.Pending &&
                property.ListingStatus != ListingStatus.Approved &&
                property.ListingStatus != ListingStatus.Rejected)
            {
                return BadRequest(
                    "This property cannot be edited in its current status.");
            }

            // Step 6: Convert Entity to Edit ViewModel
            var model = new PropertyEditViewModel
            {
                Id = property.Id,
                Latitude = property.Latitude, Longitude = property.Longitude, UnitId = property.UnitId,
                Title = property.Title,
                PropertyType = property.PropertyType,
                Description = property.Description,
                Area = property.Area,
                Address = property.Address,
                SizeInSqFt = property.SizeInSqFt,
                Bedrooms = property.Bedrooms,
                Bathrooms = property.Bathrooms,
                IsFurnished = property.IsFurnished,
                MonthlyRent = property.MonthlyRent,
                AvailableFrom = property.AvailableFrom
            };

            ViewData["Units"] = await _assets.OwnerUnitsAsync(ownerId, property.UnitId);
            ViewData["ExistingMedia"] = property.Media;
            // Step 7: Send existing data to Edit View
            return View(model);
        }

        [HttpPost, RequestSizeLimit(32_000_000)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProperty(
            PropertyEditViewModel model)
        {
            // Step 1: Validate submitted form
            var existing = await _propertyFacade.GetPropertyByIdAsync(model.Id);
            if (existing == null || existing.OwnerId != User.FindFirstValue(ClaimTypes.NameIdentifier))
                return NotFound();
            ViewData["ExistingMedia"] = existing.Media;
            var units = await _assets.OwnerUnitsAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, existing.UnitId);
            ViewData["Units"] = units;
            if (existing.UnitId.HasValue && !model.UnitId.HasValue)
                ModelState.AddModelError(nameof(model.UnitId), "Choose an inventory unit.");
            if (model.UnitId.HasValue && units.All(u => u.Value != model.UnitId.ToString()))
                ModelState.AddModelError(nameof(model.UnitId), "Choose an available unit in your portfolio.");
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Step 2: Get logged-in Owner ID
            var ownerId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Challenge();
            }

            // Step 3: Map allowed form fields to Property
            var updatedData = new Property
            {
                Latitude = model.Latitude, Longitude = model.Longitude, UnitId = model.UnitId,
                Title = model.Title,
                PropertyType = model.PropertyType,
                Description = model.Description,
                Area = model.Area,
                Address = model.Address,
                SizeInSqFt = model.SizeInSqFt,
                Bedrooms = model.Bedrooms,
                Bathrooms = model.Bathrooms,
                IsFurnished = model.IsFurnished,
                MonthlyRent = model.MonthlyRent,
                AvailableFrom = model.AvailableFrom
            };

            try { updatedData.Media = await ListingAssets.ReadAsync(model.Uploads,
                existing.Media.Count(m => !model.RemoveMedia.Contains(m.Id))); }
            catch (InvalidDataException e) { ModelState.AddModelError("", e.Message); return View(model); }

            // Step 4: Call secure Facade method
            var result = await _propertyFacade
                .UpdateOwnerPropertyAsync(
                    model.Id,
                    ownerId,
                    updatedData, model.RemoveMedia);

            // Step 5: Handle unsuccessful update
            if (!result)
            {
                return NotFound();
            }

            // Step 6: Show success message
            TempData["SuccessMessage"] =
                "Property updated successfully. " +
                "It is now pending admin approval.";

            // Step 7: Redirect to Owner Dashboard
            return RedirectToAction(nameof(Dashboard));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateProperty(int id)
        {
            // Step 1: Validate Property ID
            if (id <= 0)
            {
                return NotFound();
            }

            // Step 2: Get logged-in Owner ID
            var ownerId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return Challenge();
            }

            // Step 3: Call secure Facade method
            var result = await _propertyFacade
                .DeactivateOwnerPropertyAsync(id, ownerId);

            // Step 4: Handle unsuccessful deactivation
            if (!result)
            {
                return NotFound();
            }

            // Step 5: Show success message
            TempData["SuccessMessage"] =
                "Property deactivated successfully.";

            // Step 6: Redirect to Owner Dashboard
            return RedirectToAction(nameof(Dashboard));
        }



    }
}
