using DhakaRent.Entities.Models;

namespace DhakaRent.Web.UI.ViewModels;

public record ListingGallery(decimal? Latitude, decimal? Longitude, IReadOnlyList<ListingMedia> Media);
