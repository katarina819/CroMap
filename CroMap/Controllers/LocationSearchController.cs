using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // Ovaj endpoint je posrednik prema Nominatimu (OpenStreetMap). Dok je bio
    // otvoren, svatko ga je mogao koristiti kao besplatan geokoder na naš
    // račun — a Nominatim ima strogu politiku korištenja i blokira IP koji je
    // preoptereti, čime bi pretraživanje lokacija prestalo raditi svim
    // korisnicima aplikacije.
    [Authorize]
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
                    lon = r.Lon,
                    osmClass = r.Class,
                    osmType = r.Type
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

        [JsonPropertyName("class")]
        public string Class { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }
}