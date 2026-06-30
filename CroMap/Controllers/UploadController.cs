using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CroMap.Services;
using System.Security.Claims;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UploadController : ControllerBase
    {
        private readonly IR2StorageService _storageService;

        public UploadController(IR2StorageService storageService)
        {
            _storageService = storageService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? User.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                throw new UnauthorizedAccessException("User not authenticated");
            return userId;
        }

        [HttpPost("media")]
        public async Task<IActionResult> UploadMedia(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "No file uploaded" });

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "video/mp4", "video/quicktime" };
                if (!allowedTypes.Contains(file.ContentType))
                    return BadRequest(new { message = "Invalid file type. Only images and videos are allowed." });

                var maxSize = file.ContentType.StartsWith("video/") ? 50 * 1024 * 1024 : 10 * 1024 * 1024;
                if (file.Length > maxSize)
                    return BadRequest(new { message = $"File too large. Max {maxSize / 1024 / 1024}MB." });

                var extension = Path.GetExtension(file.FileName);
                var fileName = $"{Guid.NewGuid()}_{DateTime.Now.Ticks}{extension}";

                string fileUrl;
                using (var stream = file.OpenReadStream())
                {
                    fileUrl = await _storageService.UploadFileAsync(
                        stream,
                        fileName,
                        file.ContentType,
                        "stories"
                    );
                }

                return Ok(new
                {
                    url = fileUrl,
                    fileName,
                    type = file.ContentType.StartsWith("video/") ? "video" : "image"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}