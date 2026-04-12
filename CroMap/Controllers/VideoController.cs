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
                return BadRequest(new { message = "Invalid media data." });

            var currentUserId = GetCurrentUserId();
            if (currentUserId != request.UserId)
                return Unauthorized(new { message = "User ID mismatch." });

            // Odredi upload folder na temelju tipa medija
            var mediaType = request.MediaType ?? "video";
            var subFolder = mediaType == "image" ? "images" : "videos";
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", subFolder);

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // Generiraj jedinstveno ime fajla
            var fileExtension = Path.GetExtension(request.Video.FileName);
            if (string.IsNullOrEmpty(fileExtension))
            {
                // Ako nema ekstenzije, dodaj na temelju tipa
                fileExtension = mediaType == "image" ? ".jpg" : ".mp4";
            }

            var fileName = $"{Guid.NewGuid()}_{DateTime.Now.Ticks}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.Video.CopyToAsync(stream);
            }

            // Generiraj URL za medij
            var mediaUrl = $"{Request.Scheme}://{Request.Host}/{subFolder}/{fileName}";

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

        [HttpPost("fix-video-urls")]
        public async Task<IActionResult> FixVideoUrls()
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "videos");
            if (!Directory.Exists(uploadsFolder))
                return NotFound("Videos folder not found");

            var files = Directory.GetFiles(uploadsFolder);
            var fixedCount = 0;

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var correctUrl = $"{Request.Scheme}://{Request.Host}/videos/{fileName}";

                // Ovdje bi trebao ažurirati bazu, ali za sada samo logiraj
                Console.WriteLine($"File: {fileName} -> URL: {correctUrl}");
                fixedCount++;
            }

            return Ok(new { message = $"Found {fixedCount} videos", baseUrl = $"{Request.Scheme}://{Request.Host}" });
        }
    }
}