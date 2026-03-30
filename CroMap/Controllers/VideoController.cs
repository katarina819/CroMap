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
    public class VideoController : ControllerBase
    {
        private readonly IVideoRepository _videoRepository;

        public VideoController(IVideoRepository videoRepository)
        {
            _videoRepository = videoRepository;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? User.FindFirst("sub");

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                return userId;

            return null;
        }

        // GET: api/video
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Video>>> GetAllVideos()
        {
            var currentUserId = GetCurrentUserId();
            var videos = await _videoRepository.GetAllVideosAsync(currentUserId);
            return Ok(videos);
        }

        // GET: api/video/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Video>> GetVideoById(int id)
        {
            var currentUserId = GetCurrentUserId();
            var video = await _videoRepository.GetVideoByIdAsync(id, currentUserId);

            if (video == null)
                return NotFound(new { message = "Video not found." });

            return Ok(video);
        }

        // GET: api/video/user/2
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<Video>>> GetVideosByUser(int userId)
        {
            var currentUserId = GetCurrentUserId();
            var videos = await _videoRepository.GetVideosByUserAsync(userId, currentUserId);
            return Ok(videos);
        }

        // POST: api/video
        [HttpPost]
        public async Task<IActionResult> CreateVideo([FromBody] Video video)
        {
            if (video == null || string.IsNullOrWhiteSpace(video.Title) || string.IsNullOrWhiteSpace(video.FilePath))
                return BadRequest("Invalid video data.");

            video.CreatedAt = DateTime.UtcNow;
            await _videoRepository.CreateVideoAsync(video);
            return Ok(new { message = "Video created successfully.", videoId = video.Id });
        }

        // PUT: api/video
        [HttpPut]
        public async Task<IActionResult> UpdateVideo([FromBody] Video video)
        {
            if (video == null || video.Id <= 0)
                return BadRequest("Invalid video data.");

            var currentUserId = GetCurrentUserId();
            if (currentUserId != video.UserId)
                return Unauthorized(new { message = "You can only update your own videos." });

            await _videoRepository.UpdateVideoAsync(video);
            return Ok(new { message = "Video updated successfully." });
        }

        // DELETE: api/video/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVideo(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
                return Unauthorized(new { message = "User not authenticated." });

            await _videoRepository.DeleteVideoAsync(id, currentUserId.Value);
            return Ok(new { message = "Video deleted successfully." });
        }

        // POST: api/video/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadVideo([FromForm] VideoUploadRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Title) || request.Video == null)
                return BadRequest(new { message = "Invalid video data." });

            var currentUserId = GetCurrentUserId();
            if (currentUserId != request.UserId)
                return Unauthorized(new { message = "User ID mismatch." });

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "videos");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}_{request.Video.FileName}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.Video.CopyToAsync(stream);
            }

            var videoUrl = $"{Request.Scheme}://{Request.Host}/videos/{fileName}";

            var video = new Video
            {
                Title = request.Title,
                AdditionalDescription = request.Description ?? "",
                Location = request.Location ?? "",
                FilePath = videoUrl,
                UserId = request.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _videoRepository.CreateVideoAsync(video);
            return Ok(new { message = "Video uploaded successfully.", videoUrl, videoId = video.Id });
        }
    }
}