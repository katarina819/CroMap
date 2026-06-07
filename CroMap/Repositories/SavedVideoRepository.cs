// Repositories/SavedVideoRepository.cs (novi)
using CroMap.Models;
using CroMap.Data;
using Dapper;

namespace CroMap.Repositories
{
    public class SavedVideoRepository : ISavedVideoRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public SavedVideoRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<SavedVideoDto>> GetSavedVideosAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    sv.id, 
                    sv.video_id AS VideoId, 
                    sv.saved_at AS SavedAt, 
                    v.title AS Title, 
                    v.file_path AS FilePath,
                    u.username AS UserName
                FROM saved_videos sv
                JOIN videos v ON sv.video_id = v.id
                JOIN users u ON v.user_id = u.id
                WHERE sv.user_id = @UserId
                ORDER BY sv.saved_at DESC";

            var savedVideos = await connection.QueryAsync<SavedVideoDto>(sql, new { UserId = userId });
            return savedVideos;
        }

        public async Task<bool> RemoveSavedVideoAsync(int videoId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM saved_videos WHERE video_id = @VideoId AND user_id = @UserId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { VideoId = videoId, UserId = userId });
            return rowsAffected > 0;
        }

        // Dodajte ove metode u SavedVideoRepository
        public async Task<bool> IsVideoSavedAsync(int userId, int videoId)
        {
            using var connection = _dbConnection.CreateConnection();
            var sql = "SELECT COUNT(*) FROM saved_videos WHERE user_id = @UserId AND video_id = @VideoId";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, VideoId = videoId });
            return count > 0;
        }

        public async Task<SavedVideoDto> SaveVideoAsync(int userId, int videoId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        INSERT INTO saved_videos (user_id, video_id, saved_at)
        VALUES (@UserId, @VideoId, @SavedAt)
        ON CONFLICT (user_id, video_id) DO NOTHING
        RETURNING id, video_id AS VideoId, saved_at AS SavedAt";

            var result = await connection.QuerySingleOrDefaultAsync<SavedVideoDto>(sql, new
            {
                UserId = userId,
                VideoId = videoId,
                SavedAt = DateTime.UtcNow
            });

            if (result != null)
            {
                // Dohvati dodatne informacije o videu
                var videoSql = @"
            SELECT v.title, v.file_path AS FilePath, u.username AS UserName
            FROM videos v
            JOIN users u ON v.user_id = u.id
            WHERE v.id = @VideoId";

                var videoInfo = await connection.QuerySingleOrDefaultAsync(videoSql, new { VideoId = videoId });
                if (videoInfo != null)
                {
                    result.Title = videoInfo.title;
                    result.FilePath = videoInfo.file_path;
                    result.UserName = videoInfo.username;
                }
            }

            return result;
        }
    }

    public class SavedVideoDto
    {
        public int Id { get; set; }
        public int VideoId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
        public string UserName { get; set; } = string.Empty;
    }

    public interface ISavedVideoRepository
    {
        Task<IEnumerable<SavedVideoDto>> GetSavedVideosAsync(int userId);
        Task<bool> RemoveSavedVideoAsync(int videoId, int userId);
        Task<bool> IsVideoSavedAsync(int userId, int videoId);
        Task<SavedVideoDto> SaveVideoAsync(int userId, int videoId);
    }
}