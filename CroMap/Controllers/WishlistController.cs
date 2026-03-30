using CroMap.Models;
using CroMap.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WishlistController : ControllerBase
    {
        private readonly IWishlistRepository _wishlistRepository;

        public WishlistController(IWishlistRepository wishlistRepository)
        {
            _wishlistRepository = wishlistRepository;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                throw new UnauthorizedAccessException("User not authenticated");

            return userId;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddToWishlist([FromBody] AddToWishlistRequest request)
        {
            var userId = GetCurrentUserId();

            var wishlistItem = new WishlistVideo
            {
                UserId = userId,
                VideoId = request.VideoId,
                Notes = request.Notes ?? "",
                AddedAt = DateTime.UtcNow
            };

            await _wishlistRepository.AddToWishlistAsync(wishlistItem);

            return Ok(new { message = "Video added to wishlist" });
        }

        [HttpDelete("remove/{videoId}")]
        public async Task<IActionResult> RemoveFromWishlist(int videoId)
        {
            var userId = GetCurrentUserId();
            var success = await _wishlistRepository.RemoveFromWishlistAsync(userId, videoId);

            if (!success)
                return NotFound(new { message = "Video not found in wishlist" });

            return Ok(new { message = "Video removed from wishlist" });
        }

        [HttpGet("my-wishlist")]
        public async Task<IActionResult> GetMyWishlist()
        {
            var userId = GetCurrentUserId();
            var wishlist = await _wishlistRepository.GetUserWishlistAsync(userId);
            return Ok(wishlist);
        }

        [HttpGet("check/{videoId}")]
        public async Task<IActionResult> IsInWishlist(int videoId)
        {
            var userId = GetCurrentUserId();
            var isInWishlist = await _wishlistRepository.IsInWishlistAsync(userId, videoId);
            return Ok(new { isInWishlist });
        }

        [HttpPut("update-notes/{videoId}")]
        public async Task<IActionResult> UpdateNotes(int videoId, [FromBody] string notes)
        {
            var userId = GetCurrentUserId();
            var success = await _wishlistRepository.UpdateWishlistNotesAsync(userId, videoId, notes);

            if (!success)
                return NotFound(new { message = "Video not found in wishlist" });

            return Ok(new { message = "Notes updated successfully" });
        }
    }
}