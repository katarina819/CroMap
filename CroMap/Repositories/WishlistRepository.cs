// Repositories/WishlistRepository.cs
using CroMap.Models;
using CroMap.Data;
using Dapper;

namespace CroMap.Repositories
{
    public interface IWishlistRepository
    {
        Task<WishlistVideo> AddToWishlistAsync(WishlistVideo wishlistItem);
        Task<bool> RemoveFromWishlistAsync(int userId, int videoId);
        Task<IEnumerable<WishlistVideo>> GetUserWishlistAsync(int userId);
        Task<bool> IsInWishlistAsync(int userId, int videoId);
        Task<bool> UpdateWishlistNotesAsync(int userId, int videoId, string notes);
        Task<bool> UpdateWishlistGoingStatusAsync(int userId, int videoId, bool? isGoing);
    }

    public class WishlistRepository : IWishlistRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public WishlistRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<WishlistVideo> AddToWishlistAsync(WishlistVideo wishlistItem)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO wishlist_videos (user_id, video_id, added_at, notes, is_going)
                VALUES (@UserId, @VideoId, @AddedAt, @Notes, @IsGoing)
                ON CONFLICT (user_id, video_id) DO UPDATE SET
                    notes = EXCLUDED.notes,
                    is_going = EXCLUDED.is_going,
                    added_at = EXCLUDED.added_at
                RETURNING id, user_id AS UserId, video_id AS VideoId, added_at AS AddedAt, notes AS Notes, is_going AS IsGoing";

            var result = await connection.QuerySingleOrDefaultAsync<WishlistVideo>(sql, new
            {
                wishlistItem.UserId,
                wishlistItem.VideoId,
                AddedAt = DateTime.UtcNow,
                Notes = wishlistItem.Notes,
                IsGoing = wishlistItem.IsGoing
            });

            if (result != null)
            {
                // Dodaj video informacije
                result.VideoTitle = wishlistItem.VideoTitle;
                result.VideoFilePath = wishlistItem.VideoFilePath;
                result.UserName = wishlistItem.UserName;
            }

            return result ?? wishlistItem;
        }

        public async Task<bool> RemoveFromWishlistAsync(int userId, int videoId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM wishlist_videos WHERE user_id = @UserId AND video_id = @VideoId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId, VideoId = videoId });
            return rowsAffected > 0;
        }

        public async Task<IEnumerable<WishlistVideo>> GetUserWishlistAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    w.id, 
                    w.user_id AS UserId,
                    w.video_id AS VideoId, 
                    w.added_at AS AddedAt, 
                    w.notes AS Notes,
                    w.is_going AS IsGoing,
                    v.title AS VideoTitle, 
                    v.file_path AS VideoFilePath,
                    u.username AS UserName
                FROM wishlist_videos w
                JOIN videos v ON w.video_id = v.id
                JOIN users u ON v.user_id = u.id
                WHERE w.user_id = @UserId
                ORDER BY w.added_at DESC";

            var wishlist = await connection.QueryAsync<WishlistVideo>(sql, new { UserId = userId });
            return wishlist;
        }

        public async Task<bool> IsInWishlistAsync(int userId, int videoId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM wishlist_videos WHERE user_id = @UserId AND video_id = @VideoId";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, VideoId = videoId });
            return count > 0;
        }

        public async Task<bool> UpdateWishlistNotesAsync(int userId, int videoId, string notes)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                UPDATE wishlist_videos 
                SET notes = @Notes
                WHERE user_id = @UserId AND video_id = @VideoId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                VideoId = videoId,
                Notes = notes
            });
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateWishlistGoingStatusAsync(int userId, int videoId, bool? isGoing)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                UPDATE wishlist_videos 
                SET is_going = @IsGoing
                WHERE user_id = @UserId AND video_id = @VideoId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                VideoId = videoId,
                IsGoing = isGoing
            });
            return rowsAffected > 0;
        }
    }
}