// Repositories/MediaRepository.cs
using CroMap.Models;
using CroMap.Data;
using Dapper;

namespace CroMap.Repositories
{
    public class MediaRepository : IMediaRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public MediaRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<MediaItemDto>> GetUserMediaAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    id, 
                    file_path AS Url, 
                    'video' AS Type,
                    created_at AS CreatedAt,
                    title AS Title
                FROM videos
                WHERE user_id = @UserId
                ORDER BY created_at DESC";

            var media = await connection.QueryAsync<MediaItemDto>(sql, new { UserId = userId });
            return media;
        }

        public async Task<int> AddMediaAsync(int userId, string filePath, string? title = null)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO videos (user_id, file_path, title, created_at)
                VALUES (@UserId, @FilePath, @Title, @CreatedAt)
                RETURNING id";

            var mediaId = await connection.ExecuteScalarAsync<int>(sql, new
            {
                UserId = userId,
                FilePath = filePath,
                Title = title ?? "Untitled",
                CreatedAt = DateTime.UtcNow
            });
            return mediaId;
        }

        public async Task<bool> DeleteMediaAsync(int mediaId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM videos WHERE id = @MediaId AND user_id = @UserId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { MediaId = mediaId, UserId = userId });
            return rowsAffected > 0;
        }
    }

    public class MediaItemDto
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Title { get; set; }
    }

    public interface IMediaRepository
    {
        Task<IEnumerable<MediaItemDto>> GetUserMediaAsync(int userId);
        Task<int> AddMediaAsync(int userId, string filePath, string? title = null);
        Task<bool> DeleteMediaAsync(int mediaId, int userId);
    }
}