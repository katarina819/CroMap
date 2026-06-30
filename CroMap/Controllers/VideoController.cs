using CroMap.Models;
using CroMap.ModelsDto;
using CroMap.Repositories;
using CroMap.Services;
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
        private readonly IR2StorageService _storageService;

        public VideoController(IVideoRepository videoRepository, IR2StorageService storageService)
        {
            _videoRepository = videoRepository;
            _storageService = storageService;
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

            // Dohvati video prije brisanja da znamo file path za R2 cleanup
            var video = await _videoRepository.GetVideoByIdAsync(id, currentUserId);

            await _videoRepository.DeleteVideoAsync(id, currentUserId.Value);

            // Pokušaj obrisati i fajl s R2 (best effort, ne blokira response)
            if (video != null && !string.IsNullOrWhiteSpace(video.FilePath))
            {
                _ = _storageService.DeleteFileAsync(video.FilePath);
            }

            return Ok(new { message = "Video deleted successfully." });
        }

        // POST: api/video/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadVideo([FromForm] VideoUploadRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Title) || request.Video == null)
                return BadRequest(new { message = "Invalid media data." });

            var currentUserId = GetCurrentUserId();
            if (currentUserId != request.UserId)
                return Unauthorized(new { message = "User ID mismatch." });

            var mediaType = request.MediaType ?? "video";
            var subFolder = mediaType == "image" ? "images" : "videos";

            var fileExtension = Path.GetExtension(request.Video.FileName);
            if (string.IsNullOrEmpty(fileExtension))
            {
                fileExtension = mediaType == "image" ? ".jpg" : ".mp4";
            }

            var fileName = $"{Guid.NewGuid()}_{DateTime.Now.Ticks}{fileExtension}";

            string mediaUrl;
            using (var stream = request.Video.OpenReadStream())
            {
                mediaUrl = await _storageService.UploadFileAsync(
                    stream,
                    fileName,
                    request.Video.ContentType,
                    subFolder
                );
            }

            var video = new Video
            {
                Title = request.Title,
                AdditionalDescription = request.Description ?? "",
                Location = request.Location ?? "",
                FilePath = mediaUrl,
                UserId = request.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _videoRepository.CreateVideoAsync(video);

            return Ok(new
            {
                message = $"{mediaType} uploaded successfully.",
                mediaUrl,
                videoId = video.Id,
                mediaType = mediaType
            });
        }
    }
}