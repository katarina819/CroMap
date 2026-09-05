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

        // FOLLOW USER — ako je ciljani profil privatan, ovo šalje zahtjev za
        // praćenje umjesto da odmah upiše u "follows"; javni profili se i
        // dalje prate odmah kao prije.
        [Authorize]
        [HttpPost("{userId}")]
        public async Task<IActionResult> FollowUser(int userId)
        {
            var followerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (followerId == userId)
                return BadRequest(new { message = "Cannot follow yourself" });

            var alreadyFollowing = await _followRepository.IsFollowingAsync(followerId, userId);
            if (alreadyFollowing)
                return BadRequest(new { message = "Already following" });

            var targetIsPublic = await _followRepository.IsUserPublicAsync(userId);
            if (!targetIsPublic)
            {
                await _followRepository.RequestFollowAsync(followerId, userId);
                return Ok(new { pending = true, message = "Follow request sent" });
            }

            var success = await _followRepository.FollowAsync(followerId, userId);
            if (!success)
                return BadRequest(new { message = "Already following or invalid request" });

            return Ok(new { pending = false, message = "User followed successfully" });
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

        // CANCEL a pending follow request you sent
        [Authorize]
        [HttpDelete("request/{userId}")]
        public async Task<IActionResult> CancelFollowRequest(int userId)
        {
            var requesterId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var success = await _followRepository.CancelFollowRequestAsync(requesterId, userId);
            if (!success)
                return NotFound(new { message = "No pending request" });

            return Ok(new { message = "Follow request cancelled" });
        }

        // Has the current user already sent a pending request to userId?
        [Authorize]
        [HttpGet("request-status/{userId}")]
        public async Task<IActionResult> GetRequestStatus(int userId)
        {
            var requesterId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var pending = await _followRepository.HasPendingRequestAsync(requesterId, userId);
            return Ok(new { pending });
        }

        // List of people who requested to follow the current user
        [Authorize]
        [HttpGet("requests")]
        public async Task<IActionResult> GetIncomingRequests()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var requests = await _followRepository.GetPendingFollowRequestsAsync(userId);
            return Ok(requests);
        }

        // IDs of users the current user has sent a pending follow request to
        [Authorize]
        [HttpGet("requests/sent")]
        public async Task<IActionResult> GetSentRequestIds()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var ids = await _followRepository.GetOutgoingRequestTargetIdsAsync(userId);
            return Ok(ids);
        }

        // Accept an incoming follow request
        [Authorize]
        [HttpPost("requests/{requesterId}/accept")]
        public async Task<IActionResult> AcceptFollowRequest(int requesterId)
        {
            var targetId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var success = await _followRepository.AcceptFollowRequestAsync(requesterId, targetId);
            if (!success)
                return NotFound(new { message = "No pending request from this user" });

            return Ok(new { message = "Follow request accepted" });
        }

        // Decline an incoming follow request
        [Authorize]
        [HttpPost("requests/{requesterId}/decline")]
        public async Task<IActionResult> DeclineFollowRequest(int requesterId)
        {
            var targetId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var success = await _followRepository.DeclineFollowRequestAsync(requesterId, targetId);
            if (!success)
                return NotFound(new { message = "No pending request from this user" });

            return Ok(new { message = "Follow request declined" });
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