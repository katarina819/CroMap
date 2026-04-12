using CroMap.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/activity")]
    [Authorize]
    public class ActivityController : ControllerBase
    {
        private readonly IActivityRepository _activityRepository;

        public ActivityController(IActivityRepository activityRepository)
        {
            _activityRepository = activityRepository;
        }

        // GET: /api/activity/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats([FromQuery] string period = "daily")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var stats = await _activityRepository.GetActivityStatsAsync(userId, period);
            return Ok(stats);
        }

        // GET: /api/activity/daily
        [HttpGet("daily")]
        public async Task<IActionResult> GetDailyStats([FromQuery] int days = 7)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var stats = await _activityRepository.GetDailyStatsAsync(userId, days);
            return Ok(stats);
        }

        // POST: /api/activity/track/session
        [HttpPost("track/session")]
        public async Task<IActionResult> TrackSession([FromBody] TrackSessionRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _activityRepository.TrackSessionTime(userId, request.Minutes);
            return Ok();
        }

        // POST: /api/activity/track/like
        [HttpPost("track/like")]
        public async Task<IActionResult> TrackLike()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _activityRepository.TrackLike(userId);
            return Ok();
        }

        // POST: /api/activity/track/comment
        [HttpPost("track/comment")]
        public async Task<IActionResult> TrackComment()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _activityRepository.TrackComment(userId);
            return Ok();
        }

        // POST: /api/activity/track/post
        [HttpPost("track/post")]
        public async Task<IActionResult> TrackPost()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _activityRepository.TrackPost(userId);
            return Ok();
        }

        // POST: /api/activity/track/followers
        [HttpPost("track/followers")]
        public async Task<IActionResult> TrackFollowers([FromBody] TrackFollowersRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _activityRepository.UpdateFollowersCount(userId, request.FollowersCount);
            return Ok();
        }
    }

    public class TrackSessionRequest
    {
        public int Minutes { get; set; }
    }

    public class TrackFollowersRequest
    {
        public int FollowersCount { get; set; }
    }
}