// Repositories/PasswordResetRepository.cs
using System.Security.Cryptography;
using System.Text;
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

        /// <summary>
        /// Najviše ovoliko pogrešnih pokušaja po kodu prije nego što se poništi.
        /// </summary>
        private const int MaxAttempts = 5;

        /// <summary>
        /// Provjeri kod za resetiranje lozinke.
        ///
        /// Prije se kod tražio SAMO po vrijednosti, bez ikakve veze s korisnikom
        /// koji ga koristi: tko god bi pogodio bilo koji živi šesteroznamenkasti
        /// kod, resetirao bi lozinku onome kome taj kod pripada. Uz to nije
        /// postojalo ograničenje broja pokušaja, pa se prostor od 900.000
        /// kombinacija mogao pretraživati sve dok se ne pogodi.
        ///
        /// Sada kod mora pripadati baš onom korisniku čiji je e-mail unesen, a
        /// svaki promašaj troši jedan od <see cref="MaxAttempts"/> pokušaja;
        /// nakon toga se kod briše i mora se zatražiti novi.
        /// </summary>
        public async Task<(int UserId, bool IsValid)> ValidateTokenAsync(string token, string email)
        {
            using var conn = _db.CreateConnection();

            var result = await conn.QueryFirstOrDefaultAsync<ResetTokenResult>(@"
        SELECT t.user_id AS UserId, t.expires_at AS ExpiresAt, COALESCE(t.attempts, 0) AS Attempts
        FROM password_reset_tokens t
        JOIN users u ON u.id = t.user_id
        WHERE LOWER(u.email) = LOWER(@Email)",
                new { Email = email });

            if (result == null)
                return (0, false);

            if (result.ExpiresAt < DateTime.UtcNow || result.Attempts >= MaxAttempts)
            {
                await conn.ExecuteAsync(
                    "DELETE FROM password_reset_tokens WHERE user_id = @UserId",
                    new { result.UserId });
                return (0, false);
            }

            // Usporedba u konstantnom vremenu — kod je kratak, ali nema razloga
            // da trajanje usporedbe odaje koliko je znamenki pogođeno.
            var stored = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT token FROM password_reset_tokens WHERE user_id = @UserId",
                new { result.UserId });

            var matches = stored != null && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(token));

            if (!matches)
            {
                await conn.ExecuteAsync(
                    "UPDATE password_reset_tokens SET attempts = COALESCE(attempts, 0) + 1 WHERE user_id = @UserId",
                    new { result.UserId });
                return (0, false);
            }

            return (result.UserId, true);
        }

        public class ResetTokenResult
        {
            public int UserId { get; set; }
            public DateTime ExpiresAt { get; set; }
            public int Attempts { get; set; }
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