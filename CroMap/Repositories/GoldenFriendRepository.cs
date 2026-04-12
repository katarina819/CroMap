// Repositories/GoldenFriendRepository.cs
using CroMap.Models;
using CroMap.Data;
using Dapper;

namespace CroMap.Repositories
{
    public class GoldenFriendRepository : IGoldenFriendRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public GoldenFriendRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<GoldenFriend>> GetGoldenFriendsAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        SELECT 
            u.id AS UserId,
            u.first_name AS FirstName,
            u.last_name AS LastName,
            u.username AS Username,
            p.avatar AS Avatar
        FROM golden_friends gf
        JOIN users u ON gf.friend_id = u.id
        LEFT JOIN user_profiles p ON u.id = p.user_id
        WHERE gf.user_id = @UserId
        ORDER BY u.first_name, u.last_name";

            var friends = await connection.QueryAsync<GoldenFriend>(sql, new { UserId = userId });
            return friends;
        }

        public async Task<bool> AddGoldenFriendAsync(int userId, int friendId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO golden_friends (user_id, friend_id, created_at)
                VALUES (@UserId, @FriendId, @CreatedAt)
                ON CONFLICT (user_id, friend_id) DO NOTHING";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                FriendId = friendId,
                CreatedAt = DateTime.UtcNow
            });
            return rowsAffected > 0;
        }

        public async Task<bool> RemoveGoldenFriendAsync(int userId, int friendId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM golden_friends WHERE user_id = @UserId AND friend_id = @FriendId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId, FriendId = friendId });
            return rowsAffected > 0;
        }

        public async Task<bool> IsGoldenFriendAsync(int userId, int friendId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"SELECT COUNT(*) 
                FROM golden_friends 
                WHERE user_id = @UserId AND friend_id = @FriendId";

            var count = await connection.ExecuteScalarAsync<int>(sql, new
            {
                UserId = userId,
                FriendId = friendId
            });

            return count > 0;
        }
    }

    public interface IGoldenFriendRepository
    {
        Task<IEnumerable<GoldenFriend>> GetGoldenFriendsAsync(int userId);

        Task<bool> AddGoldenFriendAsync(int userId, int friendId);

        Task<bool> RemoveGoldenFriendAsync(int userId, int friendId);

        Task<bool> IsGoldenFriendAsync(int userId, int friendId);
    }
}