using CroMap.Data;
using CroMap.Models;
using Dapper;

namespace CroMap.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public NotificationRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<NotificationDto>> GetForUserAsync(int userId, int limit, int offset)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT
                    n.id                       AS Id,
                    n.type                     AS Type,
                    n.title                    AS Title,
                    n.body                     AS Body,
                    n.category                 AS Category,
                    n.video_id                 AS VideoId,
                    n.actor_user_id            AS ActorUserId,
                    TRIM(COALESCE(u.first_name, '') || ' ' || COALESCE(u.last_name, '')) AS ActorName,
                    COALESCE(p.avatar, '')     AS ActorAvatar,
                    n.is_read                  AS IsRead,
                    n.created_at               AS CreatedAt
                FROM notifications n
                LEFT JOIN users u         ON u.id = n.actor_user_id
                LEFT JOIN user_profiles p ON p.user_id = n.actor_user_id
                WHERE n.user_id = @UserId
                ORDER BY n.created_at DESC
                LIMIT @Limit OFFSET @Offset";

            return await connection.QueryAsync<NotificationDto>(
                sql, new { UserId = userId, Limit = limit, Offset = offset });
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();
            return await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM notifications WHERE user_id = @UserId AND is_read = FALSE",
                new { UserId = userId });
        }

        public async Task<bool> MarkReadAsync(int notificationId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();
            var rows = await connection.ExecuteAsync(
                "UPDATE notifications SET is_read = TRUE WHERE id = @Id AND user_id = @UserId",
                new { Id = notificationId, UserId = userId });
            return rows > 0;
        }

        public async Task<int> MarkAllReadAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();
            return await connection.ExecuteAsync(
                "UPDATE notifications SET is_read = TRUE WHERE user_id = @UserId AND is_read = FALSE",
                new { UserId = userId });
        }

        public async Task<bool> DeleteAsync(int notificationId, int userId)
        {
            using var connection = _dbConnection.CreateConnection();
            var rows = await connection.ExecuteAsync(
                "DELETE FROM notifications WHERE id = @Id AND user_id = @UserId",
                new { Id = notificationId, UserId = userId });
            return rows > 0;
        }

        public async Task<NotificationPreferencesDto> GetPreferencesAsync(int userId)
        {
            using var connection = _dbConnection.CreateConnection();

            var row = await connection.QueryFirstOrDefaultAsync<PreferencesRow>(@"
                SELECT app_enabled AS AppEnabled, email_enabled AS EmailEnabled,
                       email AS Email, categories AS Categories
                FROM notification_preferences
                WHERE user_id = @UserId",
                new { UserId = userId });

            if (row == null)
            {
                // Korisnik još nije spremio postavke — razumne pretpostavke.
                return new NotificationPreferencesDto();
            }

            return new NotificationPreferencesDto
            {
                AppEnabled = row.AppEnabled,
                EmailEnabled = row.EmailEnabled,
                Email = row.Email,
                Categories = SplitCategories(row.Categories),
            };
        }

        public async Task SavePreferencesAsync(int userId, NotificationPreferencesDto prefs)
        {
            using var connection = _dbConnection.CreateConnection();

            var categories = string.Join(
                ",",
                (prefs.Categories ?? new List<string>())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .Distinct());

            await connection.ExecuteAsync(@"
                INSERT INTO notification_preferences
                    (user_id, app_enabled, email_enabled, email, categories, updated_at)
                VALUES (@UserId, @AppEnabled, @EmailEnabled, @Email, @Categories, CURRENT_TIMESTAMP)
                ON CONFLICT (user_id) DO UPDATE SET
                    app_enabled   = EXCLUDED.app_enabled,
                    email_enabled = EXCLUDED.email_enabled,
                    email         = EXCLUDED.email,
                    categories    = EXCLUDED.categories,
                    updated_at    = CURRENT_TIMESTAMP",
                new
                {
                    UserId = userId,
                    prefs.AppEnabled,
                    prefs.EmailEnabled,
                    Email = string.IsNullOrWhiteSpace(prefs.Email) ? null : prefs.Email!.Trim(),
                    Categories = categories,
                });
        }

        public async Task<int> FanOutNewActivityAsync(
            int actorUserId,
            int videoId,
            string title,
            string body,
            IEnumerable<string> categories)
        {
            var list = NormalizeCategories(categories);
            if (list.Length == 0) return 0;

            using var connection = _dbConnection.CreateConnection();

            // Jedan INSERT ... SELECT umjesto dohvaćanja pratitelja pa petlje —
            // objava kluba s tisuću pratitelja inače bi značila tisuću zasebnih
            // upisa.
            var sql = @"
                INSERT INTO notifications
                    (user_id, actor_user_id, type, title, body, category, video_id)
                SELECT f.follower_id, @ActorUserId, 'new_activity', @Title, @Body, @Category, @VideoId
                FROM follows f
                JOIN notification_preferences p ON p.user_id = f.follower_id
                WHERE f.followed_id = @ActorUserId
                  AND p.app_enabled = TRUE
                  AND EXISTS (
                      SELECT 1 FROM unnest(string_to_array(p.categories, ',')) AS c
                      WHERE btrim(c) = ANY(@Categories)
                  )";

            return await connection.ExecuteAsync(sql, new
            {
                ActorUserId = actorUserId,
                VideoId = videoId,
                Title = Truncate(title, 200),
                Body = body ?? "",
                Category = list[0],
                Categories = list,
            });
        }

        public async Task<IEnumerable<NotificationEmailRecipient>> GetEmailRecipientsAsync(
            int actorUserId,
            IEnumerable<string> categories)
        {
            var list = NormalizeCategories(categories);
            if (list.Length == 0) return Array.Empty<NotificationEmailRecipient>();

            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT f.follower_id                              AS UserId,
                       COALESCE(NULLIF(p.email, ''), u.email)     AS ToEmail,
                       COALESCE(u.first_name, '')                 AS FirstName,
                       COALESCE(u.language, 'hr')                 AS Language
                FROM follows f
                JOIN notification_preferences p ON p.user_id = f.follower_id
                JOIN users u                    ON u.id = f.follower_id
                WHERE f.followed_id = @ActorUserId
                  AND p.email_enabled = TRUE
                  AND COALESCE(NULLIF(p.email, ''), u.email) IS NOT NULL
                  AND EXISTS (
                      SELECT 1 FROM unnest(string_to_array(p.categories, ',')) AS c
                      WHERE btrim(c) = ANY(@Categories)
                  )";

            return await connection.QueryAsync<NotificationEmailRecipient>(
                sql, new { ActorUserId = actorUserId, Categories = list });
        }

        private static string[] NormalizeCategories(IEnumerable<string> categories)
        {
            if (categories == null) return Array.Empty<string>();
            return categories
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct()
                .ToArray();
        }

        private static List<string> SplitCategories(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(c => c.Trim())
                      .Where(c => c.Length > 0)
                      .ToList();
        }

        private static string Truncate(string? value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= max ? value : value.Substring(0, max);
        }

        private class PreferencesRow
        {
            public bool AppEnabled { get; set; }
            public bool EmailEnabled { get; set; }
            public string? Email { get; set; }
            public string? Categories { get; set; }
        }
    }
}
