// Repositories/StoryRepository.cs - prošireni repozitorij
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CroMap.Data;
using CroMap.Models;
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

        // U StoryRepository.cs, ispravi sql upit

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
            p.avatar AS UserAvatar,
            EXISTS(SELECT 1 FROM story_views sv WHERE sv.story_id = s.id AND sv.user_id = @CurrentUserId) AS ViewedByMe,
            COALESCE((SELECT COUNT(*) FROM story_views WHERE story_id = s.id), 0) AS ViewCount,
            COALESCE((SELECT COUNT(*) FROM story_likes WHERE story_id = s.id), 0) AS LikeCount,
            COALESCE((SELECT COUNT(*) FROM story_comments WHERE story_id = s.id), 0) AS CommentCount,
            EXISTS(SELECT 1 FROM story_likes WHERE story_id = s.id AND user_id = @CurrentUserId) AS LikedByMe
        FROM stories s
        JOIN users u ON s.user_id = u.id
        LEFT JOIN user_profiles p ON s.user_id = p.user_id
        WHERE (s.expires_at > NOW() OR s.expires_at IS NULL)
        ORDER BY s.created_at DESC;
    ";

            var stories = await connection.QueryAsync<Story>(sql, new { CurrentUserId = currentUserId });

            // Dohvati dodatne podatke za svaki story
            foreach (var story in stories)
            {
                story.Viewers = (await GetStoryViewersAsync(story.Id)).ToList();
                story.Likes = (await GetStoryLikesAsync(story.Id)).ToList();
                story.Comments = (await GetStoryCommentsAsync(story.Id)).ToList();

                Console.WriteLine($"Story {story.Id} has {story.Viewers.Count()} viewers");
            }

            return stories;
        }

        public async Task<int> CreateStoryAsync(int userId, string mediaUrl, string mediaType)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO stories (user_id, media_url, media_type, created_at, expires_at)
                VALUES (@UserId, @MediaUrl, @MediaType, @CreatedAt, @ExpiresAt)
                RETURNING id;
            ";

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

            var sql = @"
                DELETE FROM stories 
                WHERE id = @StoryId AND user_id = @UserId;
            ";

            var rowsAffected = await connection.ExecuteAsync(sql, new { StoryId = storyId, UserId = userId });
            return rowsAffected > 0;
        }

        public async Task MarkAsViewedAsync(int storyId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            // Provjeri postoji li već unos
            var checkSql = "SELECT COUNT(*) FROM story_views WHERE story_id = @StoryId AND user_id = @UserId";
            var exists = await connection.ExecuteScalarAsync<int>(checkSql, new { StoryId = storyId, UserId = userId });
            Console.WriteLine($"MarkAsViewed - StoryId: {storyId}, UserId: {userId}, Exists: {exists}");

            var sql = @"
        INSERT INTO story_views (story_id, user_id, viewed_at)
        VALUES (@StoryId, @UserId, @ViewedAt)
        ON CONFLICT (story_id, user_id) DO NOTHING;
    ";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                StoryId = storyId,
                UserId = userId,
                ViewedAt = DateTime.UtcNow
            });
            Console.WriteLine($"Rows affected: {rowsAffected}");
        }

        public async Task<IEnumerable<StoryViewer>> GetStoryViewersAsync(int storyId)
        {
            using var connection = _dbConnection.CreateConnection();

            Console.WriteLine($"GetStoryViewersAsync for storyId: {storyId}");

            var sql = @"
        SELECT 
            u.id AS UserId,
            u.first_name || ' ' || u.last_name AS UserName,
            p.avatar AS UserAvatar,
            sv.viewed_at AS ViewedAt
        FROM story_views sv
        JOIN users u ON sv.user_id = u.id
        LEFT JOIN user_profiles p ON u.id = p.user_id
        WHERE sv.story_id = @StoryId
        ORDER BY sv.viewed_at DESC;
    ";

            var viewers = await connection.QueryAsync<StoryViewer>(sql, new { StoryId = storyId });
            Console.WriteLine($"Found {viewers.Count()} viewers for story {storyId}");

            return viewers;
        }

        public async Task<IEnumerable<StoryLike>> GetStoryLikesAsync(int storyId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    sl.id,
                    sl.story_id AS StoryId,
                    sl.user_id AS UserId,
                    sl.reaction_type AS ReactionType,
                    sl.created_at AS CreatedAt,
                    u.first_name || ' ' || u.last_name AS UserName,
                    p.avatar AS UserAvatar
                FROM story_likes sl
                JOIN users u ON sl.user_id = u.id
                LEFT JOIN user_profiles p ON u.id = p.user_id
                WHERE sl.story_id = @StoryId
                ORDER BY sl.created_at DESC;
            ";

            return await connection.QueryAsync<StoryLike>(sql, new { StoryId = storyId });
        }

        public async Task<IEnumerable<StoryComment>> GetStoryCommentsAsync(int storyId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    sc.id,
                    sc.story_id AS StoryId,
                    sc.user_id AS UserId,
                    sc.text AS Text,
                    sc.created_at AS CreatedAt,
                    u.first_name || ' ' || u.last_name AS UserName,
                    p.avatar AS UserAvatar
                FROM story_comments sc
                JOIN users u ON sc.user_id = u.id
                LEFT JOIN user_profiles p ON u.id = p.user_id
                WHERE sc.story_id = @StoryId
                ORDER BY sc.created_at DESC;
            ";

            var comments = await connection.QueryAsync<StoryComment>(sql, new { StoryId = storyId });

            // Dohvati reakcije za svaki komentar
            foreach (var comment in comments)
            {
                var reactionSql = @"
                    SELECT 
                        id,
                        comment_id AS CommentId,
                        user_id AS UserId,
                        reaction_type AS ReactionType,
                        created_at AS CreatedAt
                    FROM story_comment_reactions
                    WHERE comment_id = @CommentId;
                ";
                comment.Reactions = (await connection.QueryAsync<StoryCommentReaction>(reactionSql, new { CommentId = comment.Id })).ToList();
            }

            return comments;
        }

        public async Task<bool> LikeStoryAsync(int storyId, int userId, string reactionType = "like")
        {
            using var connection = _dbConnection.CreateConnection();

            // Provjeri postoji li već lajk
            var checkSql = "SELECT id FROM story_likes WHERE story_id = @StoryId AND user_id = @UserId";
            var existing = await connection.QueryFirstOrDefaultAsync<int?>(checkSql, new { StoryId = storyId, UserId = userId });

            if (existing.HasValue)
            {
                // Ažuriraj postojeći lajk
                var updateSql = "UPDATE story_likes SET reaction_type = @ReactionType, created_at = @CreatedAt WHERE id = @Id";
                await connection.ExecuteAsync(updateSql, new { ReactionType = reactionType, CreatedAt = DateTime.UtcNow, Id = existing.Value });
            }
            else
            {
                // Dodaj novi lajk
                var insertSql = @"
                    INSERT INTO story_likes (story_id, user_id, reaction_type, created_at)
                    VALUES (@StoryId, @UserId, @ReactionType, @CreatedAt);
                ";
                await connection.ExecuteAsync(insertSql, new { StoryId = storyId, UserId = userId, ReactionType = reactionType, CreatedAt = DateTime.UtcNow });
            }

            return true;
        }

        public async Task<bool> UnlikeStoryAsync(int storyId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM story_likes WHERE story_id = @StoryId AND user_id = @UserId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { StoryId = storyId, UserId = userId });

            return rowsAffected > 0;
        }

        public async Task<StoryComment> AddCommentAsync(int storyId, int userId, string text)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO story_comments (story_id, user_id, text, created_at)
                VALUES (@StoryId, @UserId, @Text, @CreatedAt)
                RETURNING id;
            ";

            var commentId = await connection.ExecuteScalarAsync<int>(sql, new
            {
                StoryId = storyId,
                UserId = userId,
                Text = text,
                CreatedAt = DateTime.UtcNow
            });

            // Dohvati kreirani komentar s podacima o korisniku
            var getSql = @"
                SELECT 
                    sc.id,
                    sc.story_id AS StoryId,
                    sc.user_id AS UserId,
                    sc.text AS Text,
                    sc.created_at AS CreatedAt,
                    u.first_name || ' ' || u.last_name AS UserName,
                    p.avatar AS UserAvatar
                FROM story_comments sc
                JOIN users u ON sc.user_id = u.id
                LEFT JOIN user_profiles p ON u.id = p.user_id
                WHERE sc.id = @CommentId;
            ";

            return await connection.QueryFirstOrDefaultAsync<StoryComment>(getSql, new { CommentId = commentId });
        }

        public async Task<bool> HasUnviewedStoriesAsync(int userId, int currentUserId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        SELECT EXISTS(
            SELECT 1 FROM stories s
            WHERE s.user_id = @UserId 
            AND (s.expires_at > NOW() OR s.expires_at IS NULL)
            AND NOT EXISTS(
                SELECT 1 FROM story_views sv 
                WHERE sv.story_id = s.id AND sv.user_id = @CurrentUserId
            )
        )";

            return await connection.ExecuteScalarAsync<bool>(sql, new { UserId = userId, CurrentUserId = currentUserId });
        }

        public async Task<bool> DeleteCommentAsync(int commentId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM story_comments WHERE id = @CommentId AND user_id = @UserId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { CommentId = commentId, UserId = userId });

            return rowsAffected > 0;
        }

        public async Task<bool> HasActiveStoryAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            // Provjeri ima li korisnik BILO KOJI aktivan story (bez obzira je li pregledao ili ne)
            var sql = @"
        SELECT EXISTS(
            SELECT 1 FROM stories s
            WHERE s.user_id = @UserId
            AND (s.expires_at > NOW() OR s.expires_at IS NULL)
        )";

            return await connection.ExecuteScalarAsync<bool>(sql, new { UserId = userId });
        }

        public async Task<bool> ReactToCommentAsync(int commentId, int userId, string reactionType)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO story_comment_reactions (comment_id, user_id, reaction_type, created_at)
                VALUES (@CommentId, @UserId, @ReactionType, @CreatedAt)
                ON CONFLICT (comment_id, user_id) DO UPDATE SET reaction_type = @ReactionType, created_at = @CreatedAt;
            ";

            await connection.ExecuteAsync(sql, new
            {
                CommentId = commentId,
                UserId = userId,
                ReactionType = reactionType,
                CreatedAt = DateTime.UtcNow
            });

            return true;
        }
    }

    public interface IStoryRepository
    {
        Task<IEnumerable<Story>> GetStoriesAsync(int currentUserId);
        Task<int> CreateStoryAsync(int userId, string mediaUrl, string mediaType);
        Task<bool> DeleteStoryAsync(int storyId, int userId);
        Task MarkAsViewedAsync(int storyId, int userId);
        Task<IEnumerable<StoryViewer>> GetStoryViewersAsync(int storyId);
        Task<IEnumerable<StoryLike>> GetStoryLikesAsync(int storyId);
        Task<IEnumerable<StoryComment>> GetStoryCommentsAsync(int storyId);
        Task<bool> LikeStoryAsync(int storyId, int userId, string reactionType = "like");
        Task<bool> UnlikeStoryAsync(int storyId, int userId);
        Task<StoryComment> AddCommentAsync(int storyId, int userId, string text);
        Task<bool> DeleteCommentAsync(int commentId, int userId);
        Task<bool> ReactToCommentAsync(int commentId, int userId, string reactionType);
        Task<bool> HasUnviewedStoriesAsync(int userId, int currentUserId);
        Task<bool> HasActiveStoryAsync(int userId);

    }
}