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

        // Popis razgovora — SAMO korisnici s kojima stvarno postoji razmijenjena
        // poruka. Aplikacija je ovaj popis dosad gradila sama: dohvatila bi sve
        // koje korisnik prati i sve koji prate njega, pa za SVAKOG od njih još
        // dva zahtjeva (profil za avatar + cijeli razgovor da vidi zadnju
        // poruku). Zbog toga su se u porukama pojavljivali i ljudi s kojima
        // nikad nije razmijenjena nijedna poruka, a ekran je za tridesetak
        // kontakata radio šezdesetak zahtjeva pri svakom osvježavanju (što je
        // znalo potrošiti i limit zahtjeva pa bi druge akcije dobile 429).
        // Ovdje sve to radi jedan upit.
        public async Task<IEnumerable<ConversationDto>> GetConversationsAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                WITH partners AS (
                    SELECT
                        CASE WHEN m.sender_id = @UserId THEN m.receiver_id ELSE m.sender_id END AS other_id,
                        MAX(m.sent_at) AS last_sent_at
                    FROM messages m
                    WHERE m.sender_id = @UserId OR m.receiver_id = @UserId
                    GROUP BY 1
                )
                SELECT
                    u.id                       AS UserId,
                    u.first_name               AS FirstName,
                    u.last_name                AS LastName,
                    u.username                 AS Username,
                    COALESCE(p.avatar, '')     AS Avatar,
                    COALESCE(last_msg.content, '') AS LastMessage,
                    last_msg.sender_id         AS LastMessageSenderId,
                    partners.last_sent_at      AS Timestamp,
                    (SELECT COUNT(*) FROM messages um
                      WHERE um.sender_id = u.id
                        AND um.receiver_id = @UserId
                        AND um.is_read = false)::int AS UnreadCount
                FROM partners
                JOIN users u ON u.id = partners.other_id
                LEFT JOIN user_profiles p ON p.user_id = u.id
                LEFT JOIN LATERAL (
                    SELECT m2.content, m2.sender_id
                    FROM messages m2
                    WHERE (m2.sender_id = @UserId AND m2.receiver_id = u.id)
                       OR (m2.sender_id = u.id AND m2.receiver_id = @UserId)
                    ORDER BY m2.sent_at DESC
                    LIMIT 1
                ) last_msg ON true
                ORDER BY partners.last_sent_at DESC";

            return await connection.QueryAsync<ConversationDto>(sql, new { UserId = userId });
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

    public class ConversationDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string LastMessage { get; set; } = string.Empty;
        public int LastMessageSenderId { get; set; }
        public DateTime Timestamp { get; set; }
        public int UnreadCount { get; set; }
    }
}