using CroMap.Models;
using CroMap.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileRepository _profileRepository;

        public ProfileController(IProfileRepository profileRepository)
        {
            _profileRepository = profileRepository;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                throw new UnauthorizedAccessException("User not authenticated");

            return userId;
        }

        // GET /api/auth/my-profile
        [HttpGet("my-profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = GetCurrentUserId();

            var profile = await _profileRepository.GetProfileAsync(userId);

            if (profile == null)
                return NotFound();

            return Ok(profile);
        }

        // PUT /api/auth/settings
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
        {
            var userId = GetCurrentUserId();

            var success = await _profileRepository.UpdateSettingsAsync(
                userId,
                request.IsPublic,
                request.ShowUsername,
                request.ScreenTimeLimitMinutes
            );

            if (!success)
                return BadRequest();

            return Ok(new { message = "Settings updated successfully" });
        }

        // PUT /api/auth/profile-photo
        [HttpPut("profile-photo")]
        public async Task<IActionResult> UploadProfilePhoto([FromForm] IFormFile avatar)
        {
            if (avatar == null || avatar.Length == 0)
                return BadRequest(new { message = "Avatar file is required." });

            var userId = GetCurrentUserId();

            try
            {
                // Kreiraj folder za avatare
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // Generiraj unique ime fajla
                var extension = Path.GetExtension(avatar.FileName);
                var fileName = $"{Guid.NewGuid()}_{DateTime.Now.Ticks}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Spremi fajl
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }

                // Generiraj URL
                var avatarUrl = $"{Request.Scheme}://{Request.Host}/avatars/{fileName}";

                // Spremi u bazu
                var success = await _profileRepository.UpdateAvatarAsync(userId, avatarUrl);

                if (!success)
                    return BadRequest(new { message = "Failed to update avatar" });

                return Ok(new { avatarUrl, message = "Avatar updated successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Avatar upload error: {ex.Message}");
                return BadRequest(new { message = "Failed to upload avatar" });
            }
        }

        // PUT /api/auth/profile-photo/avatar
        [HttpPut("profile-photo/avatar")]
        public async Task<IActionResult> SetAvatarType([FromBody] SetAvatarTypeRequest request)
        {
            var userId = GetCurrentUserId();

            // Spremi kao poseban identifikator umjesto URL-a
            var avatarIdentifier = $"avatar:{request.AvatarType}"; // "avatar:male" ili "avatar:female"

            var success = await _profileRepository.UpdateAvatarAsync(userId, avatarIdentifier);

            if (!success)
                return BadRequest(new { message = "Failed to set avatar" });

            return Ok(new { avatarUrl = avatarIdentifier, message = "Avatar set successfully" });
        }

        public class SetAvatarTypeRequest
        {
            public string AvatarType { get; set; } = string.Empty; // "male" ili "female"
        }

        // DELETE /api/auth/profile-photo
        [HttpDelete("profile-photo")]
        public async Task<IActionResult> DeleteProfilePhoto()
        {
            var userId = GetCurrentUserId();

            try
            {
                var success = await _profileRepository.DeleteAvatarAsync(userId);

                if (!success)
                    return BadRequest(new { message = "Failed to delete avatar" });

                return Ok(new { message = "Avatar deleted successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Avatar delete error: {ex.Message}");
                return BadRequest(new { message = "Failed to delete avatar" });
            }
        }
    }
}