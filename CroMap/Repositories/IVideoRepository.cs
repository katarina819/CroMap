using CroMap.Models;

namespace CroMap.Repositories
{
    public interface IVideoRepository
    {
        Task<IEnumerable<Video>> GetAllVideosAsync(int? currentUserId);
        Task<Video> GetVideoByIdAsync(int id, int? currentUserId);
        Task<IEnumerable<Video>> GetVideosByUserAsync(int userId, int? currentUserId);
        Task CreateVideoAsync(Video video);
        Task UpdateVideoAsync(Video video);
        Task DeleteVideoAsync(int id, int userId);

        // Like metode
        Task<bool> ToggleLikeAsync(int videoId, int userId);
        Task<int> GetLikeCountAsync(int videoId);
        Task<bool> IsLikedByUserAsync(int videoId, int userId);

        // Save metode
        Task<bool> SaveVideoAsync(int videoId, int userId);
        Task<bool> UnsaveVideoAsync(int videoId, int userId);
        Task<bool> IsSavedByUserAsync(int videoId, int userId);

        // Comment metode
        Task<int> AddCommentAsync(Comment comment);  // Promijeni iz Task u Task<int>
        Task<IEnumerable<Comment>> GetCommentsByVideoIdAsync(int videoId);
        Task<int> GetCommentCountAsync(int videoId);
    }
}