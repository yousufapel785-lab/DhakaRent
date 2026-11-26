using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace DhakaRent.Web.UI.Controllers;

[Authorize(Roles = "PropertyOwner"), AutoValidateAntiforgeryToken, EnableRateLimiting("geocoding")]
public class GeocodingController(IConfiguration config, IHttpClientFactory http) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Search(string address, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length > 450) return BadRequest();
        if (!Uri.TryCreate(config["Geocoding:SearchEndpoint"], UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps) return NotFound();
        var request = QueryHelpers.AddQueryString(endpoint.ToString(), new Dictionary<string,string?> {
            ["q"] = address.Trim(), ["format"] = "jsonv2", ["countrycodes"] = "bd", ["limit"] = "1" });
        try
        {
            var client = http.CreateClient("geocoding");
            using var response = await client.GetAsync(request, cancellation);
            if (!response.IsSuccessStatusCode) return StatusCode(503);
            using var data = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellation), cancellationToken: cancellation);
            if (data.RootElement.ValueKind != JsonValueKind.Array || data.RootElement.GetArrayLength() == 0) return NotFound();
            var result = data.RootElement[0];
            if (!Coordinate(result, "lat", 90, out var latitude) || !Coordinate(result, "lon", 180, out var longitude))
                return StatusCode(503);
            return Json(new { latitude, longitude });
        }
        catch (HttpRequestException) { return StatusCode(503); }
        catch (JsonException) { return StatusCode(503); }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return StatusCode(503); }
    }

    private static bool Coordinate(JsonElement result, string name, int bound, out decimal value)
    {
        value = 0;
        return result.ValueKind == JsonValueKind.Object && result.TryGetProperty(name, out var field) &&
            decimal.TryParse(field.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value) &&
            value >= -bound && value <= bound;
    }
}
