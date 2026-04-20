// Repositories/PasswordResetRepository.cs
using CroMap.Data;
using Dapper;

namespace CroMap.Repositories
{
    public class PasswordResetRepository
    {
        private readonly DatabaseConnection _db;

        public PasswordResetRepository(DatabaseConnection db)
        {
            _db = db;
        }

        public async Task CreateResetTokenAsync(int userId, string token)
        {
            using var conn = _db.CreateConnection();

            // Obriši stare tokene za ovog korisnika
            await conn.ExecuteAsync(
                "DELETE FROM password_reset_tokens WHERE user_id = @UserId",
                new { UserId = userId });

            await conn.ExecuteAsync(@"
                INSERT INTO password_reset_tokens (user_id, token, expires_at, created_at)
                VALUES (@UserId, @Token, @ExpiresAt, @CreatedAt)",
                new
                {
                    UserId = userId,
                    Token = token,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    CreatedAt = DateTime.UtcNow
                });
        }

        public async Task<(int UserId, bool IsValid)> ValidateTokenAsync(string token)
        {
            using var conn = _db.CreateConnection();

            var result = await conn.QueryFirstOrDefaultAsync<ResetTokenResult>(@"
        SELECT user_id AS UserId, expires_at AS ExpiresAt
        FROM password_reset_tokens
        WHERE token = @Token",
                new { Token = token });

            if (result == null)
                return (0, false);

            if (result.ExpiresAt < DateTime.UtcNow)
                return (0, false);

            return (result.UserId, true);
        }

        // Dodaj ovu klasu na dno fajla, unutar namespace-a ali izvan PasswordResetRepository klase
        public class ResetTokenResult
        {
            public int UserId { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
        public async Task DeleteTokenAsync(string token)
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(
                "DELETE FROM password_reset_tokens WHERE token = @Token",
                new { Token = token });
        }
    }
}