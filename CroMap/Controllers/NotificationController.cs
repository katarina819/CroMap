using System.Security.Claims;
using CroMap.Models;
using CroMap.ModelsDto;
using CroMap.Repositories;
using CroMap.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/notification")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private const int MaxPageSize = 100;
        private const int MaxTitleLength = 200;
        private const int MaxBodyLength = 2000;

        private readonly INotificationRepository _repo;
        private readonly INotificationService _service;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(
            INotificationRepository repo,
            INotificationService service,
            ILogger<NotificationController> logger)
        {
            _repo = repo;
            _service = service;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim == null || !int.TryParse(claim.Value, out var userId))
                throw new UnauthorizedAccessException("User not authenticated");
            return userId;
        }

        // GET: api/notification?page=1&pageSize=30&unreadOnly=false
        [HttpGet]
        public async Task<IActionResult> GetMine(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30,
            [FromQuery] bool unreadOnly = false)
        {
            try
            {
                var userId = GetCurrentUserId();
                var size = Math.Clamp(pageSize, 1, MaxPageSize);
                var items = await _repo.GetForUserAsync(userId, page, size, unreadOnly);
                return Ok(items);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri dohvaćanju obavijesti");
                return StatusCode(500, new { message = "Greška pri dohvaćanju obavijesti" });
            }
        }

        // GET: api/notification/unread-count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var count = await _repo.GetUnreadCountAsync(GetCurrentUserId());
                return Ok(new { count });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri brojanju nepročitanih obavijesti");
                return StatusCode(500, new { message = "Greška pri dohvaćanju obavijesti" });
            }
        }

        // POST: api/notification — korisnik sam kreira obavijest/podsjetnik
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNotificationRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Title))
                    return BadRequest(new { message = "Naslov obavijesti je obavezan" });

                if (request.Title.Length > MaxTitleLength)
                    return BadRequest(new { message = $"Naslov smije imati najviše {MaxTitleLength} znakova" });

                if (request.Body?.Length > MaxBodyLength)
                    return BadRequest(new { message = $"Tekst smije imati najviše {MaxBodyLength} znakova" });

                var userId = GetCurrentUserId();

                var notification = new Notification
                {
                    UserId = userId,
                    Category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim(),
                    Title = request.Title.Trim(),
                    Body = request.Body?.Trim(),
                    PlaceName = request.PlaceName?.Trim(),
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    ScheduledAt = request.ScheduledAt,
                    Source = "user",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                var created = await _service.CreateAsync(notification, request.SendEmail);

                // CreateAsync preskoči samo obavijesti koje je stvorio sustav
                // za kategoriju koju korisnik ne prati; vlastiti podsjetnik se
                // uvijek sprema, pa je null ovdje neočekivan.
                if (created == null)
                    return StatusCode(500, new { message = "Greška pri stvaranju obavijesti" });

                return Ok(created);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri stvaranju obavijesti");
                return StatusCode(500, new { message = "Greška pri stvaranju obavijesti" });
            }
        }

        // PUT: api/notification/{id}/read
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkRead(int id, [FromQuery] bool read = true)
        {
            try
            {
                var ok = await _repo.MarkReadAsync(id, GetCurrentUserId(), read);
                if (!ok)
                    return NotFound(new { message = "Obavijest nije pronađena" });
                return Ok(new { message = "Obavijest ažurirana" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri označavanju obavijesti {Id}", id);
                return StatusCode(500, new { message = "Greška pri ažuriranju obavijesti" });
            }
        }

        // PUT: api/notification/read-all
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            try
            {
                var updated = await _repo.MarkAllReadAsync(GetCurrentUserId());
                return Ok(new { updated });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri označavanju svih obavijesti");
                return StatusCode(500, new { message = "Greška pri ažuriranju obavijesti" });
            }
        }

        // DELETE: api/notification/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var ok = await _repo.DeleteAsync(id, GetCurrentUserId());
                if (!ok)
                    return NotFound(new { message = "Obavijest nije pronađena" });
                return Ok(new { message = "Obavijest obrisana" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri brisanju obavijesti {Id}", id);
                return StatusCode(500, new { message = "Greška pri brisanju obavijesti" });
            }
        }

        // DELETE: api/notification — obriši sve svoje obavijesti
        [HttpDelete]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                var deleted = await _repo.DeleteAllAsync(GetCurrentUserId());
                return Ok(new { deleted });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri brisanju obavijesti");
                return StatusCode(500, new { message = "Greška pri brisanju obavijesti" });
            }
        }

        // ─── Postavke ────────────────────────────────────────────────────────

        // GET: api/notification/preferences
        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences()
        {
            try
            {
                var prefs = await _repo.GetPreferencesAsync(GetCurrentUserId());
                return Ok(new NotificationPreferencesDto
                {
                    AppEnabled = prefs.AppEnabled,
                    EmailEnabled = prefs.EmailEnabled,
                    Email = prefs.Email,
                    Categories = prefs.Categories,
                    AgeGroups = prefs.AgeGroups
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri dohvaćanju postavki obavijesti");
                return StatusCode(500, new { message = "Greška pri dohvaćanju postavki" });
            }
        }

        // PUT: api/notification/preferences
        [HttpPut("preferences")]
        public async Task<IActionResult> SavePreferences([FromBody] NotificationPreferencesDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest(new { message = "Postavke nedostaju" });

                // Email je obavezan samo ako korisnik traži email obavijesti;
                // inače se sprema prazan i koristi se adresa s računa.
                var email = dto.Email?.Trim();
                if (dto.EmailEnabled && !string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
                    return BadRequest(new { message = "Email adresa nije ispravna" });

                var userId = GetCurrentUserId();
                await _repo.SavePreferencesAsync(new NotificationPreferences
                {
                    UserId = userId,
                    AppEnabled = dto.AppEnabled,
                    EmailEnabled = dto.EmailEnabled,
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    Categories = dto.Categories ?? new List<string>(),
                    AgeGroups = dto.AgeGroups ?? new List<string>()
                });

                return Ok(new { message = "Postavke spremljene" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri spremanju postavki obavijesti");
                return StatusCode(500, new { message = "Greška pri spremanju postavki" });
            }
        }

        // ─── Slanje svima u kategoriji (samo admin) ──────────────────────────

        // POST: api/notification/broadcast
        [HttpPost("broadcast")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Broadcast([FromBody] BroadcastNotificationRequest request)
        {
            try
            {
                if (request == null
                    || string.IsNullOrWhiteSpace(request.Category)
                    || string.IsNullOrWhiteSpace(request.Title))
                    return BadRequest(new { message = "Kategorija i naslov su obavezni" });

                if (request.Title.Length > MaxTitleLength)
                    return BadRequest(new { message = $"Naslov smije imati najviše {MaxTitleLength} znakova" });

                var created = await _service.BroadcastToCategoryAsync(
                    request.Category.Trim(),
                    request.Title.Trim(),
                    request.Body?.Trim(),
                    request.PlaceName?.Trim(),
                    request.Latitude,
                    request.Longitude,
                    GetCurrentUserId());

                return Ok(new { created });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri slanju obavijesti kategoriji");
                return StatusCode(500, new { message = "Greška pri slanju obavijesti" });
            }
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new System.Net.Mail.MailAddress(email);
                return address.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
