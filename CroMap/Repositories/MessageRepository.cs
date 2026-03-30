using CroMap.Models;
using Dapper;
using System.Data;
using CroMap.Data;

namespace CroMap.Repositories
{
    public class MessageRepository : IMessageRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public MessageRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<Message> SendMessageAsync(Message message)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO messages (sender_id, receiver_id, content, is_read, sent_at)
                VALUES (@SenderId, @ReceiverId, @Content, @IsRead, @SentAt)
                RETURNING id";

            message.Id = await connection.ExecuteScalarAsync<int>(sql, message);
            return message;
        }

        public async Task<IEnumerable<Message>> GetConversationAsync(int userId1, int userId2)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    m.*,
                    u1.username as SenderName,
                    u2.username as ReceiverName
                FROM messages m
                LEFT JOIN users u1 ON m.sender_id = u1.id
                LEFT JOIN users u2 ON m.receiver_id = u2.id
                WHERE (m.sender_id = @UserId1 AND m.receiver_id = @UserId2)
                   OR (m.sender_id = @UserId2 AND m.receiver_id = @UserId1)
                ORDER BY m.sent_at ASC";

            var messages = await connection.QueryAsync<Message>(sql, new { UserId1 = userId1, UserId2 = userId2 });
            return messages;
        }

        public async Task<IEnumerable<Message>> GetUserMessagesAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    m.*,
                    u1.username as SenderName,
                    u2.username as ReceiverName
                FROM messages m
                LEFT JOIN users u1 ON m.sender_id = u1.id
                LEFT JOIN users u2 ON m.receiver_id = u2.id
                WHERE m.sender_id = @UserId OR m.receiver_id = @UserId
                ORDER BY m.sent_at DESC";

            var messages = await connection.QueryAsync<Message>(sql, new { UserId = userId });
            return messages;
        }

        public async Task<IEnumerable<Message>> GetUnreadMessagesAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    m.*,
                    u1.username as SenderName,
                    u2.username as ReceiverName
                FROM messages m
                LEFT JOIN users u1 ON m.sender_id = u1.id
                LEFT JOIN users u2 ON m.receiver_id = u2.id
                WHERE m.receiver_id = @UserId AND m.is_read = false
                ORDER BY m.sent_at DESC";

            var messages = await connection.QueryAsync<Message>(sql, new { UserId = userId });
            return messages;
        }

        public async Task<bool> MarkAsReadAsync(int messageId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "UPDATE messages SET is_read = true WHERE id = @MessageId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { MessageId = messageId });
            return rowsAffected > 0;
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM messages WHERE receiver_id = @UserId AND is_read = false";
            return await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
        }

        public async Task<bool> DeleteMessageAsync(int messageId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            // Može obrisati samo pošiljatelj ili primatelj
            var sql = "DELETE FROM messages WHERE id = @MessageId AND (sender_id = @UserId OR receiver_id = @UserId)";
            var rowsAffected = await connection.ExecuteAsync(sql, new { MessageId = messageId, UserId = userId });
            return rowsAffected > 0;
        }
    }
}