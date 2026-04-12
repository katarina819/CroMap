// Repositories/FollowRepository.cs
using CroMap.Data;
using Dapper;

namespace CroMap.Repositories
{
    public class FollowRepository : IFollowRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public FollowRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<bool> FollowAsync(int followerId, int followedId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO follows (follower_id, followed_id, created_at)
                VALUES (@FollowerId, @FollowedId, @CreatedAt)
                ON CONFLICT (follower_id, followed_id) DO NOTHING";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                FollowerId = followerId,
                FollowedId = followedId,
                CreatedAt = DateTime.UtcNow
            });
            return rowsAffected > 0;
        }

        public async Task<bool> UnfollowAsync(int followerId, int followedId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM follows WHERE follower_id = @FollowerId AND followed_id = @FollowedId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { FollowerId = followerId, FollowedId = followedId });
            return rowsAffected > 0;
        }

        public async Task<IEnumerable<UserSearchDto>> GetFollowingAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        SELECT 
            u.id,
            u.first_name AS FirstName,
            u.last_name AS LastName,
            u.username AS Username,
            p.avatar AS Avatar
        FROM follows f
        JOIN users u ON f.followed_id = u.id
        LEFT JOIN user_profiles p ON u.id = p.user_id
        WHERE f.follower_id = @UserId
        ORDER BY u.first_name, u.last_name";

            var following = await connection.QueryAsync<UserSearchDto>(sql, new { UserId = userId });
            return following;
        }

        public async Task<int> GetFollowersCountAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM follows WHERE followed_id = @UserId";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
            return count;
        }

        public async Task<int> GetFollowingCountAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM follows WHERE follower_id = @UserId";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
            return count;
        }

        public async Task<bool> IsFollowingAsync(int followerId, int followedId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM follows WHERE follower_id = @FollowerId AND followed_id = @FollowedId";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { FollowerId = followerId, FollowedId = followedId });
            return count > 0;
        }

        public async Task<IEnumerable<UserSearchDto>> GetFollowersAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        SELECT 
            u.id,
            u.first_name AS FirstName,
            u.last_name AS LastName,
            u.username AS Username,
            p.avatar AS Avatar
        FROM follows f
        JOIN users u ON f.follower_id = u.id
        LEFT JOIN user_profiles p ON u.id = p.user_id
        WHERE f.followed_id = @UserId
        ORDER BY u.first_name, u.last_name";

            var followers = await connection.QueryAsync<UserSearchDto>(sql, new { UserId = userId });
            return followers;
        }
    }

   
}