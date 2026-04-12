// Repositories/ProfileRepository.cs
using CroMap.Models;
using CroMap.Data;
using Dapper;
using System.Data;

namespace CroMap.Repositories
{
    public class ProfileRepository : IProfileRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public ProfileRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<ProfileDto?> GetProfileAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
    SELECT 
        u.id AS Id,
        u.first_name AS FirstName, 
        u.last_name AS LastName, 
        u.username AS Username,
        COALESCE(p.avatar, '') AS Avatar,
        COALESCE(p.is_public, true) AS IsPublic,
        COALESCE(p.show_username, true) AS ShowUsername,
        p.screen_time_limit_minutes AS ScreenTimeLimitMinutes,
        (SELECT COUNT(*) FROM follows WHERE followed_id = u.id) AS FollowersCount,
        (SELECT COUNT(*) FROM follows WHERE follower_id = u.id) AS FollowingCount
    FROM users u
    LEFT JOIN user_profiles p ON u.id = p.user_id
    WHERE u.id = @UserId";

            var profile = await connection.QueryFirstOrDefaultAsync<ProfileDto>(sql, new { UserId = userId });
            return profile;
        }

        
        public async Task<IEnumerable<UserSearchDto>> GetAllUsersAsync(int currentUserId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        SELECT 
            u.id, 
            u.first_name AS FirstName, 
            u.last_name AS LastName, 
            u.username AS Username, 
            p.avatar AS Avatar
        FROM users u
        LEFT JOIN user_profiles p ON u.id = p.user_id
        WHERE u.id != @CurrentUserId
        ORDER BY u.first_name, u.last_name";

            var users = await connection.QueryAsync<UserSearchDto>(sql, new { CurrentUserId = currentUserId });
            return users;
        }

        public async Task<bool> UpdateAvatarAsync(int userId, string avatarUrl)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO user_profiles (user_id, avatar, updated_at)
                VALUES (@UserId, @Avatar, @UpdatedAt)
                ON CONFLICT (user_id) 
                DO UPDATE SET avatar = @Avatar, updated_at = @UpdatedAt";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                Avatar = avatarUrl,
                UpdatedAt = DateTime.UtcNow
            });
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAvatarAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                UPDATE user_profiles 
                SET avatar = NULL, updated_at = @UpdatedAt
                WHERE user_id = @UserId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                UpdatedAt = DateTime.UtcNow
            });
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateSettingsAsync(int userId, bool isPublic, bool showUsername, int screenTimeLimitMinutes)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        INSERT INTO user_profiles (user_id, is_public, show_username, screen_time_limit_minutes, updated_at)
        VALUES (@UserId, @IsPublic, @ShowUsername, @ScreenTimeLimit, @UpdatedAt)
        ON CONFLICT (user_id) 
        DO UPDATE SET 
            is_public = @IsPublic,
            show_username = @ShowUsername,
            screen_time_limit_minutes = @ScreenTimeLimit,
            updated_at = @UpdatedAt";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                IsPublic = isPublic,
                ShowUsername = showUsername,
                ScreenTimeLimit = screenTimeLimitMinutes == 0 ? (int?)null : screenTimeLimitMinutes,
                UpdatedAt = DateTime.UtcNow
            });

            return rowsAffected > 0;
        }

        public async Task<bool> IsUsernameAvailableAsync(string username, int? excludeUserId = null)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM users WHERE username = @Username";
            if (excludeUserId.HasValue)
            {
                sql += " AND id != @ExcludeUserId";
            }

            var count = await connection.ExecuteScalarAsync<int>(sql, new
            {
                Username = username,
                ExcludeUserId = excludeUserId
            });
            return count == 0;
        }

        public async Task<bool> UpdateUsernameAsync(int userId, string username)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "UPDATE users SET username = @Username WHERE id = @UserId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Username = username, UserId = userId });
            return rowsAffected > 0;
        }

        public async Task<bool> InitializeProfileAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO user_profiles (user_id, is_public, created_at, updated_at)
                VALUES (@UserId, true, @CreatedAt, @UpdatedAt)
                ON CONFLICT (user_id) DO NOTHING";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAccountAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            // user_profiles će biti obrisan kaskadno zbog ON DELETE CASCADE
            var sql = "DELETE FROM users WHERE id = @UserId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId });
            return rowsAffected > 0;
        }
    }

    public interface IProfileRepository
    {
        Task<ProfileDto?> GetProfileAsync(int userId);
        Task<IEnumerable<UserSearchDto>> GetAllUsersAsync(int currentUserId);
        Task<bool> UpdateAvatarAsync(int userId, string avatarUrl);
        Task<bool> DeleteAvatarAsync(int userId);
        Task<bool> UpdateSettingsAsync(int userId, bool isPublic, bool showUsername, int screenTimeLimitMinutes);
        Task<bool> IsUsernameAvailableAsync(string username, int? excludeUserId = null);
        Task<bool> UpdateUsernameAsync(int userId, string username);
        Task<bool> InitializeProfileAsync(int userId);
        Task<bool> DeleteAccountAsync(int userId);
    }

    public class UserSearchDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public int? FollowersCount { get; set; }
    }
}