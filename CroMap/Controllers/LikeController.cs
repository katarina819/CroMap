using CroMap.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CroMap.ModelsDto;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LikeController : ControllerBase
    {
        private readonly IVideoRepository _videoRepository;

        public LikeController(IVideoRepository videoRepository)
        {
            _videoRepository = videoRepository;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                throw new UnauthorizedAccessException("User not authenticated");

            return userId;
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleLike([FromBody] LikeToggleRequest request)
        {
            var userId = GetCurrentUserId();
            var isLiked = await _videoRepository.ToggleLikeAsync(request.VideoId, userId);

            var likeCount = await _videoRepository.GetLikeCountAsync(request.VideoId);

            return Ok(new
            {
                message = isLiked ? "Video liked" : "Video unliked",
                isLiked = isLiked,
                likeCount = likeCount
            });
        }
    }
}