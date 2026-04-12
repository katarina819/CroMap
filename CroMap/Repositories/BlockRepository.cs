using CroMap.Data;
using CroMap.Models;
using Dapper;

namespace CroMap.Repositories
{
    public class BlockRepository : IBlockRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public BlockRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<bool> BlockUserAsync(int userId, int blockedUserId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO blocks (user_id, blocked_user_id, created_at)
                VALUES (@UserId, @BlockedUserId, @CreatedAt)
                ON CONFLICT (user_id, blocked_user_id) DO NOTHING";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                BlockedUserId = blockedUserId,
                CreatedAt = DateTime.UtcNow
            });

            return rowsAffected > 0;
        }

        public async Task<bool> UnblockUserAsync(int userId, int blockedUserId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                DELETE FROM blocks
                WHERE user_id = @UserId 
                AND blocked_user_id = @BlockedUserId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                BlockedUserId = blockedUserId
            });

            return rowsAffected > 0;
        }

        public async Task<IEnumerable<BlockedUser>> GetBlockedUsersAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT
                    u.id,
                    u.first_name AS FirstName,
                    u.last_name AS LastName,
                    u.username AS Username
                FROM blocks b
                JOIN users u ON b.blocked_user_id = u.id
                WHERE b.user_id = @UserId
                ORDER BY u.first_name, u.last_name";

            var blockedUsers = await connection.QueryAsync<BlockedUser>(sql, new { UserId = userId });

            return blockedUsers;
        }

        public async Task<bool> IsBlockedAsync(int userId, int blockedUserId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT COUNT(*)
                FROM blocks
                WHERE user_id = @UserId 
                AND blocked_user_id = @BlockedUserId";

            var count = await connection.ExecuteScalarAsync<int>(sql, new
            {
                UserId = userId,
                BlockedUserId = blockedUserId
            });

            return count > 0;
        }
    }

   
}