using CroMap.Models;
using CroMap.Data;
using Dapper;
using System.Data;

namespace CroMap.Repositories
{
    public class UserRepository
    {
        private readonly DatabaseConnection _dbConnection;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(DatabaseConnection dbConnection, ILogger<UserRepository> logger)
        {
            _dbConnection = dbConnection;
            _logger = logger;
        }

        public async Task RegisterAsync(User user)
        {
            using var connection = _dbConnection.CreateConnection();

            // 🔥 HASHIRAJ LOZINKU PRIJE SPREMANJA
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);

            var sql = @"
                INSERT INTO users (username, first_name, last_name, email, phone, password_hash, birth_date, created_at)
                VALUES (@Username, @FirstName, @LastName, @Email, @Phone, @PasswordHash, @BirthDate, @CreatedAt)";

            await connection.ExecuteAsync(sql, new
            {
                user.Username,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Phone,
                PasswordHash = hashedPassword,  // ← SPREMI HASH
                user.BirthDate,
                user.CreatedAt
            });

            _logger.LogInformation($"✅ User registered: {user.Username}");
        }

        // 🔥 DODAJ OVU METODU ZA LOGIN PO USERNAME
        public async Task<User> LoginByUsernameAsync(string username, string password)
        {
            using var connection = _dbConnection.CreateConnection();

            _logger.LogInformation($"🔐 Login attempt - Username: '{username}'");

            // Prvo dohvati korisnika po username-u
            var sql = "SELECT * FROM users WHERE LOWER(username) = LOWER(@Username)";
            var user = await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });

            if (user == null)
            {
                _logger.LogWarning($"❌ User not found with username: '{username}'");
                return null;
            }

            _logger.LogInformation($"✅ User found: {user.Username}");

            // 🔥 VERIFIKACIJA LOZINKE POMOĆU BCrypt
            bool passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

            _logger.LogInformation($"Password verification result: {passwordValid}");

            if (passwordValid)
            {
                _logger.LogInformation($"✅ Login successful for: {user.Username}");
                return user;
            }

            _logger.LogWarning($"❌ Invalid password for: {user.Username}");
            return null;
        }

        // Opcionalno: zadrži i staru metodu za backward compatibility
        public async Task<User> LoginAsync(string username, string password)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT * FROM users 
                WHERE (email = @Username OR phone = @Username) 
                AND password_hash = @Password";

            var user = await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username, Password = password });
            return user;
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT * FROM users ORDER BY id";
            return await connection.QueryAsync<User>(sql);
        }

        public async Task<User> GetUserByIdAsync(int id)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT * FROM users WHERE id = @Id";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT * FROM users WHERE LOWER(username) = LOWER(@Username)";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
        }

        public async Task UpdateUserAsync(User user)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                UPDATE users 
                SET username = @Username,
                    first_name = @FirstName, 
                    last_name = @LastName, 
                    email = @Email, 
                    phone = @Phone, 
                    birth_date = @BirthDate
                WHERE id = @Id";

            await connection.ExecuteAsync(sql, user);
        }

        public async Task DeleteUserAsync(int id)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM users WHERE id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}