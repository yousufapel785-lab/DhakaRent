using DhakaRent.DataAccess.Context;
using DhakaRent.Entities.Enums;
using DhakaRent.Entities.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Services;

public class ListingAssets(ApplicationDbContext db)
{
    public Task<List<SelectListItem>> OwnerUnitsAsync(string ownerId, int? currentUnit = null) =>
        db.RentalUnits.Where(u => u.Floor.Building.OwnerId == ownerId && (u.Id == currentUnit || !u.Floor.Building.IsArchived &&
                u.OccupancyStatus == UnitOccupancyStatus.Vacant))
            .OrderBy(u => u.Floor.Building.Name).ThenBy(u => u.Floor.FloorNumber).ThenBy(u => u.UnitNumber)
            .Select(u => new SelectListItem(u.Floor.Building.Name + " / Floor " + u.Floor.FloorNumber + " / " + u.UnitNumber, u.Id.ToString()))
            .ToListAsync();

    public static async Task<List<ListingMedia>> ReadAsync(List<IFormFile>? files, int retainedCount)
    {
        files ??= [];
        if (files.Count + retainedCount > 8 || files.Sum(f => f.Length) > 30_000_000)
            throw new InvalidDataException("Use up to eight attachments per listing and 30 MB per submission.");

        List<ListingMedia> media = [];
        foreach (var file in files)
        {
            if (file.Length < 12 || file.Length > 24_000_000)
                throw new InvalidDataException("Each file must be a supported image, video or floor plan up to 24 MB.");
            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer);
            var bytes = buffer.ToArray();
            var type = DetectType(bytes);
            media.Add(new ListingMedia { ContentType = type, Content = new() { Bytes = bytes } });
        }
        return media;
    }

    private static string DetectType(ReadOnlySpan<byte> b)
    {
        if (b.StartsWith("%PDF-"u8)) return "application/pdf";
        if (b.StartsWith(new byte[] { 0xff, 0xd8, 0xff })) return "image/jpeg";
        if (b.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (b.StartsWith("RIFF"u8) && b.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        if (b.Slice(4, 4).SequenceEqual("ftyp"u8) &&
            (b.Slice(8, 4).SequenceEqual("isom"u8) || b.Slice(8, 3).SequenceEqual("mp4"u8) || b.Slice(8, 3).SequenceEqual("iso"u8)))
            return "video/mp4";
        if (b.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }) && b[..Math.Min(256, b.Length)].IndexOf("webm"u8) >= 0) return "video/webm";
        throw new InvalidDataException("Choose a JPEG, PNG, WebP, MP4, WebM or PDF file.");
    }
}
