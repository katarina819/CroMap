// Kreiraj novi fajl: Controllers/UploadController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CroMap.Data;
using System.Security.Claims;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public UploadController(IWebHostEnvironment environment)
        {
            _environment = environment;
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

                // Provjeri tip fajla
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "video/mp4", "video/quicktime" };
                if (!allowedTypes.Contains(file.ContentType))
                    return BadRequest(new { message = "Invalid file type. Only images and videos are allowed." });

                // Maksimalna veličina: 50MB za video, 10MB za sliku
                var maxSize = file.ContentType.StartsWith("video/") ? 50 * 1024 * 1024 : 10 * 1024 * 1024;
                if (file.Length > maxSize)
                    return BadRequest(new { message = $"File too large. Max {maxSize / 1024 / 1024}MB." });

                // Kreiraj upload folder
                var uploadsFolder = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "stories");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // Generiraj unique ime
                var extension = Path.GetExtension(file.FileName);
                var fileName = $"{Guid.NewGuid()}_{DateTime.Now.Ticks}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Spremi file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Generiraj URL
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var fileUrl = $"{baseUrl}/uploads/stories/{fileName}";

                return Ok(new { url = fileUrl, fileName, type = file.ContentType.StartsWith("video/") ? "video" : "image" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}