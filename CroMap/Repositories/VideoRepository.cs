using CroMap.Models;
using Dapper;
using System.Data;
using CroMap.Data;

namespace CroMap.Repositories
{
    public class VideoRepository : IVideoRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public VideoRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<Video>> GetAllVideosAsync(int? currentUserId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    v.*,
                    u.username as UserName,
                    COALESCE(lc.like_count, 0) as LikeCount,
                    COALESCE(cc.comment_count, 0) as CommentCount,
                    CASE WHEN ul.user_id IS NOT NULL THEN true ELSE false END as IsLiked,
                    CASE WHEN sv.user_id IS NOT NULL THEN true ELSE false END as IsSaved,
                    CASE WHEN v.user_id = @CurrentUserId THEN true ELSE false END as IsOwner
                FROM videos v
                LEFT JOIN users u ON v.user_id = u.id
                LEFT JOIN (
                    SELECT video_id, COUNT(*) as like_count
                    FROM likes
                    GROUP BY video_id
                ) lc ON v.id = lc.video_id
                LEFT JOIN (
                    SELECT video_id, COUNT(*) as comment_count
                    FROM comments
                    GROUP BY video_id
                ) cc ON v.id = cc.video_id
                LEFT JOIN likes ul ON v.id = ul.video_id AND ul.user_id = @CurrentUserId
                LEFT JOIN saved_videos sv ON v.id = sv.video_id AND sv.user_id = @CurrentUserId
                ORDER BY v.created_at DESC";

            var videos = await connection.QueryAsync<Video>(sql, new { CurrentUserId = currentUserId });
            return videos;
        }

        public async Task<Video> GetVideoByIdAsync(int id, int? currentUserId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    v.*,
                    u.username as UserName,
                    COALESCE(lc.like_count, 0) as LikeCount,
                    COALESCE(cc.comment_count, 0) as CommentCount,
                    CASE WHEN ul.user_id IS NOT NULL THEN true ELSE false END as IsLiked,
                    CASE WHEN sv.user_id IS NOT NULL THEN true ELSE false END as IsSaved,
                    CASE WHEN v.user_id = @CurrentUserId THEN true ELSE false END as IsOwner
                FROM videos v
                LEFT JOIN users u ON v.user_id = u.id
                LEFT JOIN (
                    SELECT video_id, COUNT(*) as like_count
                    FROM likes
                    WHERE video_id = @Id
                    GROUP BY video_id
                ) lc ON v.id = lc.video_id
                LEFT JOIN (
                    SELECT video_id, COUNT(*) as comment_count
                    FROM comments
                    WHERE video_id = @Id
                    GROUP BY video_id
                ) cc ON v.id = cc.video_id
                LEFT JOIN likes ul ON v.id = ul.video_id AND ul.user_id = @CurrentUserId
                LEFT JOIN saved_videos sv ON v.id = sv.video_id AND sv.user_id = @CurrentUserId
                WHERE v.id = @Id";

            var video = await connection.QueryFirstOrDefaultAsync<Video>(sql, new { Id = id, CurrentUserId = currentUserId });
            return video;
        }

        public async Task<IEnumerable<Video>> GetVideosByUserAsync(int userId, int? currentUserId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        SELECT 
            v.*,
            u.username as UserName,
            COALESCE(lc.like_count, 0) as LikeCount,
            COALESCE(cc.comment_count, 0) as CommentCount,
            CASE WHEN ul.user_id IS NOT NULL THEN true ELSE false END as IsLiked,
            CASE WHEN sv.user_id IS NOT NULL THEN true ELSE false END as IsSaved,
            CASE WHEN wv.user_id IS NOT NULL THEN true ELSE false END as IsInWishlist,
            CASE WHEN v.user_id = @CurrentUserId THEN true ELSE false END as IsOwner
        FROM videos v
        LEFT JOIN users u ON v.user_id = u.id
        LEFT JOIN (
            SELECT video_id, COUNT(*) as like_count
            FROM likes
            GROUP BY video_id
        ) lc ON v.id = lc.video_id
        LEFT JOIN (
            SELECT video_id, COUNT(*) as comment_count
            FROM comments
            GROUP BY video_id
        ) cc ON v.id = cc.video_id
        LEFT JOIN likes ul ON v.id = ul.video_id AND ul.user_id = @CurrentUserId
        LEFT JOIN saved_videos sv ON v.id = sv.video_id AND sv.user_id = @CurrentUserId
        LEFT JOIN wishlist_videos wv ON v.id = wv.video_id AND wv.user_id = @CurrentUserId
        WHERE v.user_id = @UserId
        ORDER BY v.created_at DESC";

            var videos = await connection.QueryAsync<Video>(sql, new { UserId = userId, CurrentUserId = currentUserId });
            return videos;
        }

        public async Task CreateVideoAsync(Video video)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO videos (user_id, title, location, additional_description, file_path, created_at)
                VALUES (@UserId, @Title, @Location, @AdditionalDescription, @FilePath, @CreatedAt)
                RETURNING id";

            video.Id = await connection.ExecuteScalarAsync<int>(sql, video);
        }

        public async Task UpdateVideoAsync(Video video)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                UPDATE videos 
                SET title = @Title, 
                    location = @Location, 
                    additional_description = @AdditionalDescription,
                    file_path = @FilePath
                WHERE id = @Id AND user_id = @UserId";

            await connection.ExecuteAsync(sql, video);
        }

        public async Task DeleteVideoAsync(int id, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM videos WHERE id = @Id AND user_id = @UserId";
            await connection.ExecuteAsync(sql, new { Id = id, UserId = userId });
        }

        // LIKE METODE
        public async Task<bool> ToggleLikeAsync(int videoId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            // Prvo provjeri postoji li like
            var checkSql = "SELECT id FROM likes WHERE video_id = @VideoId AND user_id = @UserId";
            var existingLike = await connection.QueryFirstOrDefaultAsync<int?>(checkSql, new { VideoId = videoId, UserId = userId });

            if (existingLike.HasValue)
            {
                // Ako postoji, obriši
                var deleteSql = "DELETE FROM likes WHERE video_id = @VideoId AND user_id = @UserId";
                await connection.ExecuteAsync(deleteSql, new { VideoId = videoId, UserId = userId });
                return false; // unlike
            }
            else
            {
                // Ako ne postoji, dodaj
                var insertSql = "INSERT INTO likes (user_id, video_id, created_at) VALUES (@UserId, @VideoId, @CreatedAt)";
                await connection.ExecuteAsync(insertSql, new { UserId = userId, VideoId = videoId, CreatedAt = DateTime.UtcNow });
                return true; // like
            }
        }

        public async Task<int> GetLikeCountAsync(int videoId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM likes WHERE video_id = @VideoId";
            return await connection.ExecuteScalarAsync<int>(sql, new { VideoId = videoId });
        }

        public async Task<bool> IsLikedByUserAsync(int videoId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM likes WHERE video_id = @VideoId AND user_id = @UserId";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { VideoId = videoId, UserId = userId });
            return count > 0;
        }

        // SAVE VIDEO METODE
        public async Task<bool> ToggleSavedVideoAsync(int videoId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var checkSql = "SELECT id FROM saved_videos WHERE video_id = @VideoId AND user_id = @UserId";
            var existing = await connection.QueryFirstOrDefaultAsync<int?>(checkSql, new { VideoId = videoId, UserId = userId });

            if (existing.HasValue)
            {
                var deleteSql = "DELETE FROM saved_videos WHERE video_id = @VideoId AND user_id = @UserId";
                await connection.ExecuteAsync(deleteSql, new { VideoId = videoId, UserId = userId });
                return false; // unsave
            }
            else
            {
                var insertSql = "INSERT INTO saved_videos (user_id, video_id, saved_at) VALUES (@UserId, @VideoId, @SavedAt)";
                await connection.ExecuteAsync(insertSql, new { UserId = userId, VideoId = videoId, SavedAt = DateTime.UtcNow });
                return true; // save
            }
        }

        public async Task<bool> IsSavedByUserAsync(int videoId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM saved_videos WHERE video_id = @VideoId AND user_id = @UserId";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { VideoId = videoId, UserId = userId });
            return count > 0;
        }

        // COMMENT METODE
        public async Task AddCommentAsync(Comment comment)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO comments (user_id, video_id, content, created_at)
                VALUES (@UserId, @VideoId, @Content, @CreatedAt)
                RETURNING id";

            comment.Id = await connection.ExecuteScalarAsync<int>(sql, comment);
        }

        public async Task<IEnumerable<Comment>> GetCommentsByVideoIdAsync(int videoId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT c.*, u.username as UserName
                FROM comments c
                LEFT JOIN users u ON c.user_id = u.id
                WHERE c.video_id = @VideoId
                ORDER BY c.created_at DESC";

            var comments = await connection.QueryAsync<Comment>(sql, new { VideoId = videoId });
            return comments;
        }

        public async Task<int> GetCommentCountAsync(int videoId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM comments WHERE video_id = @VideoId";
            return await connection.ExecuteScalarAsync<int>(sql, new { VideoId = videoId });
        }
    }
}