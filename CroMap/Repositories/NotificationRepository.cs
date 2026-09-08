using CroMap.Data;
using CroMap.Models;
using Dapper;

namespace CroMap.Repositories
{
    /// <summary>
    /// Oblik u kojem red iz notification_preferences dolazi iz baze:
    /// kategorije i dobne skupine su ondje CSV, a u modelu su liste.
    /// </summary>
    internal sealed class NotificationPreferencesRow
    {
        public int UserId { get; set; }
        public bool AppEnabled { get; set; }
        public bool EmailEnabled { get; set; }
        public string? Email { get; set; }
        public string? Categories { get; set; }
        public string? AgeGroups { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class NotificationRepository : INotificationRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public NotificationRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        // ─── Obavijesti ──────────────────────────────────────────────────────

        public async Task<IEnumerable<Notification>> GetForUserAsync(int userId, int page, int pageSize, bool unreadOnly)
        {
            using var connection = _dbConnection.CreateConnection();

            // Paginirano od početka — popis obavijesti raste neograničeno, a
            // ekran ionako prikazuje samo zadnjih nekoliko desetaka.
            var offset = (Math.Max(page, 1) - 1) * pageSize;

            var sql = @"
                SELECT id, user_id, category, title, body, place_name,
                       latitude, longitude, scheduled_at, is_read, source,
                       created_by, email_sent, created_at
                FROM notifications
                WHERE user_id = @UserId
                  AND (@UnreadOnly = FALSE OR is_read = FALSE)
                ORDER BY created_at DESC, id DESC
                LIMIT @PageSize OFFSET @Offset";

            return await connection.QueryAsync<Notification>(sql, new
            {
                UserId = userId,
                UnreadOnly = unreadOnly,
                PageSize = pageSize,
                Offset = offset
            });
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "SELECT COUNT(*) FROM notifications WHERE user_id = @UserId AND is_read = FALSE";
            return await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
        }

        public async Task<Notification?> GetByIdAsync(int id, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT id, user_id, category, title, body, place_name,
                       latitude, longitude, scheduled_at, is_read, source,
                       created_by, email_sent, created_at
                FROM notifications
                WHERE id = @Id AND user_id = @UserId";

            return await connection.QueryFirstOrDefaultAsync<Notification>(sql, new { Id = id, UserId = userId });
        }

        public async Task<int> CreateAsync(Notification notification)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO notifications
                    (user_id, category, title, body, place_name, latitude,
                     longitude, scheduled_at, is_read, source, created_by,
                     email_sent, created_at)
                VALUES
                    (@UserId, @Category, @Title, @Body, @PlaceName, @Latitude,
                     @Longitude, @ScheduledAt, FALSE, @Source, @CreatedBy,
                     @EmailSent, @CreatedAt)
                RETURNING id";

            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                notification.UserId,
                notification.Category,
                notification.Title,
                notification.Body,
                notification.PlaceName,
                notification.Latitude,
                notification.Longitude,
                notification.ScheduledAt,
                notification.Source,
                notification.CreatedBy,
                notification.EmailSent,
                CreatedAt = notification.CreatedAt == default ? DateTime.UtcNow : notification.CreatedAt
            });
        }

        public async Task<bool> MarkReadAsync(int id, int userId, bool isRead)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "UPDATE notifications SET is_read = @IsRead WHERE id = @Id AND user_id = @UserId";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, UserId = userId, IsRead = isRead });
            return rows > 0;
        }

        public async Task<int> MarkAllReadAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "UPDATE notifications SET is_read = TRUE WHERE user_id = @UserId AND is_read = FALSE";
            return await connection.ExecuteAsync(sql, new { UserId = userId });
        }

        public async Task<bool> DeleteAsync(int id, int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM notifications WHERE id = @Id AND user_id = @UserId";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, UserId = userId });
            return rows > 0;
        }

        public async Task<int> DeleteAllAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "DELETE FROM notifications WHERE user_id = @UserId";
            return await connection.ExecuteAsync(sql, new { UserId = userId });
        }

        public async Task<bool> MarkEmailSentAsync(int id)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = "UPDATE notifications SET email_sent = TRUE WHERE id = @Id";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }

        // ─── Postavke ────────────────────────────────────────────────────────

        private static List<string> SplitCsv(string? csv) =>
            string.IsNullOrWhiteSpace(csv)
                ? new List<string>()
                : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        // Zapisuje se bez razmaka jer GetSubscribersForCategoryAsync traži
        // ",kategorija," kao doslovan podniz.
        private static string JoinCsv(List<string>? values) =>
            string.Join(",", (values ?? new List<string>())
                .Select(v => v?.Trim() ?? "")
                .Where(v => v.Length > 0)
                .Distinct());

        private static NotificationPreferences ToModel(NotificationPreferencesRow row) => new()
        {
            UserId = row.UserId,
            AppEnabled = row.AppEnabled,
            EmailEnabled = row.EmailEnabled,
            Email = row.Email,
            Categories = SplitCsv(row.Categories),
            AgeGroups = SplitCsv(row.AgeGroups),
            UpdatedAt = row.UpdatedAt
        };

        public async Task<NotificationPreferences> GetPreferencesAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT user_id, app_enabled, email_enabled, email,
                       categories, age_groups, updated_at
                FROM notification_preferences
                WHERE user_id = @UserId";

            var row = await connection.QueryFirstOrDefaultAsync<NotificationPreferencesRow>(sql, new { UserId = userId });

            // Korisnik koji još nikad nije otvorio postavke nema red u tablici —
            // vraćamo "sve isključeno" umjesto null da pozivatelji ne moraju
            // svugdje provjeravati.
            if (row == null)
                return new NotificationPreferences { UserId = userId };

            return ToModel(row);
        }

        public async Task SavePreferencesAsync(NotificationPreferences prefs)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO notification_preferences
                    (user_id, app_enabled, email_enabled, email, categories, age_groups, updated_at)
                VALUES
                    (@UserId, @AppEnabled, @EmailEnabled, @Email, @Categories, @AgeGroups, @UpdatedAt)
                ON CONFLICT (user_id) DO UPDATE SET
                    app_enabled   = EXCLUDED.app_enabled,
                    email_enabled = EXCLUDED.email_enabled,
                    email         = EXCLUDED.email,
                    categories    = EXCLUDED.categories,
                    age_groups    = EXCLUDED.age_groups,
                    updated_at    = EXCLUDED.updated_at";

            await connection.ExecuteAsync(sql, new
            {
                prefs.UserId,
                prefs.AppEnabled,
                prefs.EmailEnabled,
                prefs.Email,
                Categories = JoinCsv(prefs.Categories),
                AgeGroups = JoinCsv(prefs.AgeGroups),
                UpdatedAt = DateTime.UtcNow
            });
        }

        public async Task<IEnumerable<NotificationPreferences>> GetSubscribersForCategoryAsync(string category)
        {
            using var connection = _dbConnection.CreateConnection();

            // Kategorije su CSV, pa se traži cijeli element popisa — zarezi s
            // obje strane sprječavaju da "bar" upadne kao dio "sportsbar".
            var sql = @"
                SELECT user_id, app_enabled, email_enabled, email,
                       categories, age_groups, updated_at
                FROM notification_preferences
                WHERE app_enabled = TRUE
                  AND strpos(',' || categories || ',', @Needle) > 0";

            var rows = await connection.QueryAsync<NotificationPreferencesRow>(sql, new { Needle = $",{category}," });
            return rows.Select(ToModel);
        }
    }
}
