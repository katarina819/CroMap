using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationSearchController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public LocationSearchController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // GET: api/locationsearch/autocomplete?query=Os
        [HttpGet("autocomplete")]
        public async Task<IActionResult> Autocomplete([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                return Ok(new List<object>());

            var client = _httpClientFactory.CreateClient();
            // Nominatim traži User-Agent, inače baca 403
            client.DefaultRequestHeaders.Add("User-Agent", "VARA-App/1.0");

            var url = $"https://nominatim.openstreetmap.org/search" +
                      $"?q={Uri.EscapeDataString(query)}" +
                      $"&countrycodes=hr" +
                      $"&format=json" +
                      $"&addressdetails=1" +
                      $"&limit=6";

            try
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return Ok(new List<object>());

                var json = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<List<NominatimResult>>(json);
                if (results == null || results.Count == 0)
                    return Ok(new List<object>());

                var suggestions = results.Select(r => new
                {
                    displayName = r.DisplayName,
                    lat = r.Lat,
                    lon = r.Lon
                }).ToList();

                return Ok(suggestions);
            }
            catch
            {
                return Ok(new List<object>());
            }
        }
    }

    public class NominatimResult
    {
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("lat")]
        public string Lat { get; set; } = "";

        [JsonPropertyName("lon")]
        public string Lon { get; set; } = "";
    }
}