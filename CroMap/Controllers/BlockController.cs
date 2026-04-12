using CroMap.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/block")]
    [Authorize]
    public class BlockController : ControllerBase
    {
        private readonly IBlockRepository _blockRepository;
        private readonly IFollowRepository _followRepository; // Dodaj ovo

        // Ažuriraj konstruktor
        public BlockController(IBlockRepository blockRepository, IFollowRepository followRepository)
        {
            _blockRepository = blockRepository;
            _followRepository = followRepository; // Inicijaliziraj
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                throw new UnauthorizedAccessException("User not authenticated");

            return userId;
        }

        // POST: api/block/block/{userId}
        [HttpPost("block/{userId}")]
        public async Task<IActionResult> BlockUser(int userId)
        {
            var currentUserId = GetCurrentUserId();

            // Prvo ukloni follow ako postoji
            try
            {
                var isFollowing = await _followRepository.IsFollowingAsync(currentUserId, userId);
                if (isFollowing)
                {
                    await _followRepository.UnfollowAsync(currentUserId, userId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unfollow error: {ex.Message}");
                // Nastavi dalje čak i ako unfollow ne uspije
            }

            var success = await _blockRepository.BlockUserAsync(currentUserId, userId);

            if (!success)
                return BadRequest(new { message = "User already blocked or invalid request." });

            return Ok(new { message = "User blocked successfully." });
        }

        // DELETE: api/block/unblock/{userId}
        [HttpDelete("unblock/{userId}")]
        public async Task<IActionResult> UnblockUser(int userId)
        {
            var currentUserId = GetCurrentUserId();

            var success = await _blockRepository.UnblockUserAsync(currentUserId, userId);

            if (!success)
                return NotFound(new { message = "Blocked user not found." });

            return Ok(new { message = "User unblocked successfully." });
        }

        // GET: api/block/blocked-users
        [HttpGet("blocked-users")]
        public async Task<IActionResult> GetBlockedUsers()
        {
            var currentUserId = GetCurrentUserId();

            var users = await _blockRepository.GetBlockedUsersAsync(currentUserId);

            return Ok(users);
        }

        // GET: api/block/is-blocked/{userId}
        [HttpGet("is-blocked/{userId}")]
        public async Task<IActionResult> IsBlocked(int userId)
        {
            var currentUserId = GetCurrentUserId();

            var isBlocked = await _blockRepository.IsBlockedAsync(currentUserId, userId);

            return Ok(new { isBlocked });
        }
    }
}