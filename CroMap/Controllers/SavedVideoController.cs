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
    public class SavedVideoController : ControllerBase
    {
        private readonly IVideoRepository _videoRepository;

        public SavedVideoController(IVideoRepository videoRepository)
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
        public async Task<IActionResult> ToggleSavedVideo([FromBody] SavedVideoToggleRequest request)
        {
            var userId = GetCurrentUserId();
            var isSaved = await _videoRepository.ToggleSavedVideoAsync(request.VideoId, userId);

            return Ok(new
            {
                message = isSaved ? "Video saved" : "Video unsaved",
                isSaved = isSaved
            });
        }
    }
}