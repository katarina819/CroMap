using CroMap.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FollowController : ControllerBase
    {
        private readonly IFollowRepository _followRepository;

        public FollowController(IFollowRepository followRepository)
        {
            _followRepository = followRepository;
        }

        // FOLLOW USER
        [Authorize]
        [HttpPost("{userId}")]
        public async Task<IActionResult> FollowUser(int userId)
        {
            var followerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var success = await _followRepository.FollowAsync(followerId, userId);

            if (!success)
                return BadRequest(new { message = "Already following or invalid request" });

            return Ok(new { message = "User followed successfully" });
        }

        // UNFOLLOW USER
        [Authorize]
        [HttpDelete("{userId}")]
        public async Task<IActionResult> UnfollowUser(int userId)
        {
            var followerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var success = await _followRepository.UnfollowAsync(followerId, userId);

            if (!success)
                return BadRequest(new { message = "You are not following this user" });

            return Ok(new { message = "User unfollowed successfully" });
        }

        // GET FOLLOWERS LIST
        [HttpGet("followers/{userId}")]
        public async Task<IActionResult> GetFollowers(int userId)
        {
            var followers = await _followRepository.GetFollowersAsync(userId);
            return Ok(followers);
        }

        // GET FOLLOWING LIST
        [HttpGet("following/{userId}")]
        public async Task<IActionResult> GetFollowing(int userId)
        {
            var following = await _followRepository.GetFollowingAsync(userId);
            return Ok(following);
        }

        // CHECK IF CURRENT USER FOLLOWS SOMEONE
        [Authorize]
        [HttpGet("is-following/{userId}")]
        public async Task<IActionResult> IsFollowing(int userId)
        {
            var followerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var isFollowing = await _followRepository.IsFollowingAsync(followerId, userId);

            return Ok(new { isFollowing });
        }

        // GET FOLLOW COUNTS
        [HttpGet("counts/{userId}")]
        public async Task<IActionResult> GetFollowCounts(int userId)
        {
            var followers = await _followRepository.GetFollowersCountAsync(userId);
            var following = await _followRepository.GetFollowingCountAsync(userId);

            return Ok(new
            {
                followers,
                following
            });
        }
    }
}