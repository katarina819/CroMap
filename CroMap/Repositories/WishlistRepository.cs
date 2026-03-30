using CroMap.Models;
using Dapper;
using System.Data;
using CroMap.Data;

namespace CroMap.Repositories
{
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
                INSERT INTO wishlist_videos (user_id, video_id, added_at, notes)
                VALUES (@UserId, @VideoId, @AddedAt, @Notes)
                ON CONFLICT (user_id, video_id) DO UPDATE SET 
                    notes = EXCLUDED.notes,
                    added_at = EXCLUDED.added_at
                RETURNING id";

            wishlistItem.Id = await connection.ExecuteScalarAsync<int>(sql, wishlistItem);
            return wishlistItem;
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
                    w.*,
                    v.title as VideoTitle,
                    v.file_path as VideoFilePath,
                    v.location,
                    v.additional_description,
                    u.username as UserName
                FROM wishlist_videos w
                LEFT JOIN videos v ON w.video_id = v.id
                LEFT JOIN users u ON v.user_id = u.id
                WHERE w.user_id = @UserId
                ORDER BY w.added_at DESC";

            var wishlistItems = await connection.QueryAsync<WishlistVideo, Video, WishlistVideo>(
                sql,
                (wishlist, video) =>
                {
                    wishlist.Video = video;
                    return wishlist;
                },
                new { UserId = userId },
                splitOn: "Id"
            );

            return wishlistItems;
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

            var sql = "UPDATE wishlist_videos SET notes = @Notes WHERE user_id = @UserId AND video_id = @VideoId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId, VideoId = videoId, Notes = notes });
            return rowsAffected > 0;
        }
    }
}