using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CroMap.Repositories;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AdminRepository _adminRepository;
        private readonly IActivityRepository _activityRepository;

        public AdminController(AdminRepository adminRepository, IActivityRepository activityRepository)
        {
            _adminRepository = adminRepository;
            _activityRepository = activityRepository;
        }

        // GET: api/admin/users
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _adminRepository.GetAllUsersWithStatsAsync();
            return Ok(users);
        }

        // GET: api/admin/users/{userId}/activity
        [HttpGet("users/{userId}/activity")]
        public async Task<IActionResult> GetUserActivity(int userId, [FromQuery] string period = "daily")
        {
            var stats = await _activityRepository.GetActivityStatsAsync(userId, period);
            return Ok(stats);
        }

        // GET: api/admin/users/{userId}/daily-activity
        [HttpGet("users/{userId}/daily-activity")]
        public async Task<IActionResult> GetUserDailyActivity(int userId, [FromQuery] int days = 30)
        {
            var stats = await _activityRepository.GetDailyStatsAsync(userId, days);
            return Ok(stats);
        }

        // GET: api/admin/stats/summary
        [HttpGet("stats/summary")]
        public async Task<IActionResult> GetSummaryStats()
        {
            var summary = await _adminRepository.GetAdminSummaryAsync();
            return Ok(summary);
        }

        // POST: api/plan-ratings  (korisnik šalje ocjenu — bez admin role)
        [HttpPost("/api/plan-ratings")]
        [AllowAnonymous]
        public async Task<IActionResult> SavePlanRating([FromBody] SavePlanRatingRequest request)
        {
            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest("Ocjena mora biti između 1 i 5.");
            if (string.IsNullOrWhiteSpace(request.Destination))
                return BadRequest("Destinacija je obavezna.");

            var id = await _adminRepository.SavePlanRatingAsync(
                request.UserName ?? "Anonimni korisnik",
                request.Destination,
                request.Rating
            );
            return StatusCode(201, new { id });
        }

        // GET: api/plan-ratings  (admin dohvaća sve ocjene)
        [HttpGet("/api/plan-ratings")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPlanRatings()
        {
            var ratings = await _adminRepository.GetPlanRatingsAsync();
            return Ok(ratings);
        }
    }
}