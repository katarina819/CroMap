using CroMap.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/golden-friends")]
    public class GoldenFriendsController : ControllerBase
    {
        private readonly IGoldenFriendRepository _goldenFriendRepository;

        public GoldenFriendsController(IGoldenFriendRepository goldenFriendRepository)
        {
            _goldenFriendRepository = goldenFriendRepository;
        }

        // GET: api/golden-friends/{userId}
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetGoldenFriends(int userId)
        {
            var friends = await _goldenFriendRepository.GetGoldenFriendsAsync(userId);
            return Ok(friends);
        }

        // POST: api/golden-friends/add/{friendId}
        [Authorize]
        [HttpPost("add/{friendId}")]
        public async Task<IActionResult> AddGoldenFriend(int friendId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var success = await _goldenFriendRepository.AddGoldenFriendAsync(userId, friendId);

            if (!success)
                return BadRequest(new { message = "Already a Golden Friend or invalid request" });

            return Ok(new { message = "Golden Friend added successfully" });
        }

        // DELETE: api/golden-friends/remove/{friendId}
        [Authorize]
        [HttpDelete("remove/{friendId}")]
        public async Task<IActionResult> RemoveGoldenFriend(int friendId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var success = await _goldenFriendRepository.RemoveGoldenFriendAsync(userId, friendId);

            if (!success)
                return BadRequest(new { message = "Golden Friend not found" });

            return Ok(new { message = "Golden Friend removed successfully" });
        }

        // GET: api/golden-friends - dohvat svih Golden Friends za trenutnog korisnika
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMyGoldenFriends()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var friends = await _goldenFriendRepository.GetGoldenFriendsAsync(userId);
            return Ok(friends);
        }

        // GET: api/golden-friends/is-golden/{userId}
        [Authorize]
        [HttpGet("is-golden/{userId}")]
        public async Task<IActionResult> IsGoldenFriend(int userId)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var isGolden = await _goldenFriendRepository.IsGoldenFriendAsync(currentUserId, userId);

            return Ok(new { isGolden });
        }
    }
}