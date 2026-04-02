// Repositories/StoryRepository.cs
using CroMap.Models;
using CroMap.Data;
using Dapper;

namespace CroMap.Repositories
{
    public class StoryRepository : IStoryRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public StoryRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<Story>> GetStoriesAsync(int currentUserId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    s.id, 
                    s.user_id AS UserId, 
                    s.media_url AS MediaUrl, 
                    s.media_type AS MediaType, 
                    s.created_at AS CreatedAt, 
                    s.expires_at AS ExpiresAt,
                    u.first_name || ' ' || u.last_name AS UserName,
                    u.avatar AS UserAvatar,
                    EXISTS(SELECT 1 FROM story_views sv WHERE sv.story_id = s.id AND sv.user_id = @CurrentUserId) AS ViewedByMe,
                    (SELECT COUNT(*) FROM story_views WHERE story_id = s.id) AS ViewCount
                FROM stories s
                JOIN users u ON s.user_id = u.id
                WHERE s.expires_at > NOW() OR s.expires_at IS NULL
                ORDER BY s.created_at DESC";

            var stories = await connection.QueryAsync<Story>(sql, new { CurrentUserId = currentUserId });
            return stories;
        }

        public async Task<int> CreateStoryAsync(int userId, string mediaUrl, string mediaType)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO stories (user_id, media_url, media_type, created_at, expires_at)
                VALUES (@UserId, @MediaUrl, @MediaType, @CreatedAt, @ExpiresAt)
                RETURNING id";

            var storyId = await connection.ExecuteScalarAsync<int>(sql, new
            {
                UserId = userId,
                MediaUrl = mediaUrl,
                MediaType = mediaType,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            return storyId;
        }

        public async Task<bool> DeleteStoryAsync(int storyId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM stories WHERE id = @StoryId AND user_id = @UserId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { StoryId = storyId, UserId = userId });
            return rowsAffected > 0;
        }

        public async Task MarkAsViewedAsync(int storyId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO story_views (story_id, user_id, viewed_at)
                VALUES (@StoryId, @UserId, @ViewedAt)
                ON CONFLICT (story_id, user_id) DO NOTHING";

            await connection.ExecuteAsync(sql, new
            {
                StoryId = storyId,
                UserId = userId,
                ViewedAt = DateTime.UtcNow
            });
        }

        public async Task<IEnumerable<StoryViewer>> GetStoryViewersAsync(int storyId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    u.id AS UserId,
                    u.first_name || ' ' || u.last_name AS UserName
                FROM story_views sv
                JOIN users u ON sv.user_id = u.id
                WHERE sv.story_id = @StoryId";

            var viewers = await connection.QueryAsync<StoryViewer>(sql, new { StoryId = storyId });
            return viewers;
        }
    }

    public interface IStoryRepository
    {
        Task<IEnumerable<Story>> GetStoriesAsync(int currentUserId);
        Task<int> CreateStoryAsync(int userId, string mediaUrl, string mediaType);
        Task<bool> DeleteStoryAsync(int storyId, int userId);
        Task MarkAsViewedAsync(int storyId, int userId);
        Task<IEnumerable<StoryViewer>> GetStoryViewersAsync(int storyId);
    }
}