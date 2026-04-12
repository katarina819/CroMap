// Controllers/WishlistVideoController.cs
using CroMap.Models;
using CroMap.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/[controller]")]  // Ovo će biti "api/wishlistvideo"
    [Authorize]
    public class WishlistVideoController : ControllerBase
    {
        private readonly IWishlistRepository _wishlistRepository;

        public WishlistVideoController(IWishlistRepository wishlistRepository)
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

        [HttpGet("my-wishlist")]
        public async Task<IActionResult> GetMyWishlist()
        {
            var userId = GetCurrentUserId();
            var wishlist = await _wishlistRepository.GetUserWishlistAsync(userId);

            // Mapiraj na format koji frontend očekuje
            var result = wishlist.Select(w => new
            {
                id = w.Id,
                videoId = w.VideoId,
                title = w.Title,
                filePath = w.FilePath,
                addedAt = w.AddedAt,
                isGoing = w.IsGoing,
                notes = w.Notes
            });

            return Ok(result);
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddToWishlist([FromBody] AddToWishlistRequest request)
        {
            var userId = GetCurrentUserId();

            // Provjeri da li već postoji
            var exists = await _wishlistRepository.IsInWishlistAsync(userId, request.VideoId);
            if (exists)
                return BadRequest(new { message = "Video already in wishlist" });

            var wishlistItem = new WishlistVideo
            {
                UserId = userId,
                VideoId = request.VideoId,
                Notes = request.Notes ?? "",
                AddedAt = DateTime.UtcNow,
                IsGoing = null
            };

            var result = await _wishlistRepository.AddToWishlistAsync(wishlistItem);
            return Ok(new { success = true, message = "Video added to wishlist" });
        }

        [HttpDelete("remove")]
        public async Task<IActionResult> RemoveFromWishlist([FromQuery] int userId, [FromQuery] int videoId)
        {
            var currentUserId = GetCurrentUserId();

            // Sigurnosna provjera - korisnik može brisati samo svoje stavke
            if (currentUserId != userId)
                return Unauthorized(new { message = "Cannot remove other user's wishlist items" });

            var success = await _wishlistRepository.RemoveFromWishlistAsync(userId, videoId);

            if (!success)
                return NotFound(new { message = "Video not found in wishlist" });

            return Ok(new { success = true, message = "Video removed from wishlist" });
        }

        [HttpGet("check/{videoId}")]
        public async Task<IActionResult> IsInWishlist(int videoId)
        {
            var userId = GetCurrentUserId();
            var isInWishlist = await _wishlistRepository.IsInWishlistAsync(userId, videoId);
            return Ok(new { isInWishlist });
        }

        [HttpPut("update/{videoId}")]
        public async Task<IActionResult> UpdateWishlistItem(int videoId, [FromBody] UpdateWishlistRequest request)
        {
            var userId = GetCurrentUserId();

            if (request.IsGoing.HasValue)
            {
                await _wishlistRepository.UpdateWishlistGoingStatusAsync(userId, videoId, request.IsGoing);
            }

            if (!string.IsNullOrEmpty(request.Notes))
            {
                await _wishlistRepository.UpdateWishlistNotesAsync(userId, videoId, request.Notes);
            }

            return Ok(new { success = true });
        }
    }

    public class AddToWishlistRequest
    {
        public int VideoId { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateWishlistRequest
    {
        public bool? IsGoing { get; set; }
        public string? Notes { get; set; }
    }
}