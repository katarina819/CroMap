using CroMap.Models;
using CroMap.ModelsDto;
using CroMap.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CommentController : ControllerBase
    {
        private readonly IVideoRepository _videoRepository;

        public CommentController(IVideoRepository videoRepository)
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

        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] CommentCreateRequest request)
        {
            var userId = GetCurrentUserId();

            var comment = new Comment
            {
                UserId = userId,
                VideoId = request.VideoId,
                Content = request.Content,
                CreatedAt = DateTime.UtcNow
            };

            var commentId = await _videoRepository.AddCommentAsync(comment);

            var commentCount = await _videoRepository.GetCommentCountAsync(request.VideoId);

            return Ok(new
            {
                message = "Comment added successfully",
                commentId = commentId,
                commentCount = commentCount
            });
        }

        [HttpGet("video/{videoId}")]
        public async Task<ActionResult<IEnumerable<Comment>>> GetCommentsByVideoId(int videoId)
        {
            var comments = await _videoRepository.GetCommentsByVideoIdAsync(videoId);
            return Ok(comments);
        }
    }
}