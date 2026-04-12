// Controllers/SavedVideoController.cs
using System.Security.Claims;
using CroMap.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SavedVideoController : ControllerBase
{
    private readonly ISavedVideoRepository _savedVideoRepository;

    public SavedVideoController(ISavedVideoRepository savedVideoRepository)
    {
        _savedVideoRepository = savedVideoRepository;
    }

    [HttpGet("my-saved")]
    public async Task<IActionResult> GetMySavedVideos()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var savedVideos = await _savedVideoRepository.GetSavedVideosAsync(userId);
        return Ok(savedVideos);
    }

    [HttpPost("save")]
    public async Task<IActionResult> SaveVideo([FromBody] SaveVideoRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        // Provjeri da li već postoji
        var exists = await _savedVideoRepository.IsVideoSavedAsync(userId, request.VideoId);
        if (exists)
            return BadRequest("Video je već spremljen u Box");

        var saved = await _savedVideoRepository.SaveVideoAsync(userId, request.VideoId);
        return Ok(saved);
    }

    [HttpDelete("unsave")]
    public async Task<IActionResult> UnsaveVideo([FromQuery] int videoId, [FromQuery] int userId)
    {
        // Verificiraj da je userId isti kao token
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        if (currentUserId != userId)
            return Unauthorized();

        var result = await _savedVideoRepository.RemoveSavedVideoAsync(videoId, userId);
        return result ? Ok() : NotFound();
    }
}

public class SaveVideoRequest
{
    public int VideoId { get; set; }
}