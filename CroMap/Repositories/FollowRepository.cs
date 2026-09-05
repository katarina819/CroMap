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

        // ─── Follow requests (privatni profili) ───────────────────────────────

        public async Task<bool> IsUserPublicAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT COALESCE(
                    (SELECT is_public FROM user_profiles WHERE user_id = @UserId),
                    true)";

            return await connection.ExecuteScalarAsync<bool>(sql, new { UserId = userId });
        }

        public async Task<bool> RequestFollowAsync(int requesterId, int targetId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO follow_requests (requester_id, target_id, created_at)
                VALUES (@RequesterId, @TargetId, @CreatedAt)
                ON CONFLICT (requester_id, target_id) DO NOTHING";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                RequesterId = requesterId,
                TargetId = targetId,
                CreatedAt = DateTime.UtcNow
            });
            return rowsAffected > 0;
        }

        public async Task<bool> CancelFollowRequestAsync(int requesterId, int targetId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM follow_requests WHERE requester_id = @RequesterId AND target_id = @TargetId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { RequesterId = requesterId, TargetId = targetId });
            return rowsAffected > 0;
        }

        public async Task<bool> HasPendingRequestAsync(int requesterId, int targetId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM follow_requests WHERE requester_id = @RequesterId AND target_id = @TargetId";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { RequesterId = requesterId, TargetId = targetId });
            return count > 0;
        }

        public async Task<bool> AcceptFollowRequestAsync(int requesterId, int targetId)
        {
            using var connection = _dbConnection.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                var deleted = await connection.ExecuteAsync(
                    "DELETE FROM follow_requests WHERE requester_id = @RequesterId AND target_id = @TargetId",
                    new { RequesterId = requesterId, TargetId = targetId },
                    transaction);

                if (deleted == 0)
                {
                    transaction.Rollback();
                    return false;
                }

                await connection.ExecuteAsync(@"
                    INSERT INTO follows (follower_id, followed_id, created_at)
                    VALUES (@RequesterId, @TargetId, @CreatedAt)
                    ON CONFLICT (follower_id, followed_id) DO NOTHING",
                    new { RequesterId = requesterId, TargetId = targetId, CreatedAt = DateTime.UtcNow },
                    transaction);

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> DeclineFollowRequestAsync(int requesterId, int targetId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM follow_requests WHERE requester_id = @RequesterId AND target_id = @TargetId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { RequesterId = requesterId, TargetId = targetId });
            return rowsAffected > 0;
        }

        public async Task<IEnumerable<UserSearchDto>> GetPendingFollowRequestsAsync(int targetId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        SELECT
            u.id,
            u.first_name AS FirstName,
            u.last_name AS LastName,
            u.username AS Username,
            p.avatar AS Avatar
        FROM follow_requests fr
        JOIN users u ON fr.requester_id = u.id
        LEFT JOIN user_profiles p ON u.id = p.user_id
        WHERE fr.target_id = @TargetId
        ORDER BY fr.created_at DESC";

            var requests = await connection.QueryAsync<UserSearchDto>(sql, new { TargetId = targetId });
            return requests;
        }

        public async Task<IEnumerable<int>> GetOutgoingRequestTargetIdsAsync(int requesterId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT target_id FROM follow_requests WHERE requester_id = @RequesterId";
            var ids = await connection.QueryAsync<int>(sql, new { RequesterId = requesterId });
            return ids;
        }
    }
}