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
    }
}