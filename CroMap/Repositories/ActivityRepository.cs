using CroMap.Data;
using CroMap.Models;
using Dapper;

namespace CroMap.Repositories
{
    public class ActivityRepository : IActivityRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public ActivityRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        // Dohvati statistiku za period (dnevno, tjedno, mjesečno)
        public async Task<IEnumerable<ActivityStats>> GetActivityStatsAsync(int userId, string period = "daily")
        {
            using var connection = _dbConnection.CreateConnection();

            string groupBy;
            string dateFormat;
            string lookback;

            // Lookback prozor mora rasti s periodom — prije je uvijek bio
            // fiksiran na 30 dana, pa je "mjesečni" prikaz grupiran po
            // DATE_TRUNC('month', ...) u praksi gotovo uvijek pokazivao samo
            // tekući (djelomični) mjesec, nikad stvarnu mjesečnu povijest.
            switch (period)
            {
                case "weekly":
                    groupBy = "DATE_TRUNC('week', date)";
                    dateFormat = "YYYY-MM-DD";
                    lookback = "'90 days'";
                    break;
                case "monthly":
                    groupBy = "DATE_TRUNC('month', date)";
                    dateFormat = "YYYY-MM";
                    lookback = "'365 days'";
                    break;
                default: // daily
                    groupBy = "date";
                    dateFormat = "YYYY-MM-DD";
                    lookback = "'30 days'";
                    break;
            }

            // FollowersCount koristi zadnju poznatu vrijednost unutar perioda
            // (po datumu), ne MAX — MAX bi znao pokazati "vršni" broj pratitelja
            // čak i nakon što su neki otpratili, što je zavaravajuće za
            // "trenutno stanje" na kraju tjedna/mjeseca.
            var sql = $@"
                SELECT
                    TO_CHAR({groupBy}, '{dateFormat}') AS Date,
                    SUM(session_minutes) AS SessionMinutes,
                    SUM(likes) AS Likes,
                    SUM(comments) AS Comments,
                    SUM(posts) AS Posts,
                    (ARRAY_AGG(followers_count ORDER BY date DESC))[1] AS FollowersCount
                FROM activity_logs
                WHERE user_id = @UserId
                AND date >= CURRENT_DATE - INTERVAL {lookback}
                GROUP BY {groupBy}
                ORDER BY {groupBy} DESC";

            var stats = await connection.QueryAsync<ActivityStats>(sql, new { UserId = userId });
            return stats;
        }

        // Ažuriraj ili kreiraj dnevnu aktivnost
        public async Task UpdateDailyActivity(int userId, string actionType, int value = 1)
        {
            using var connection = _dbConnection.CreateConnection();

            string columnName = actionType switch
            {
                "session" => "session_minutes",
                "like" => "likes",
                "comment" => "comments",
                "post" => "posts",
                "follower" => "followers_count",
                _ => throw new ArgumentException("Invalid action type")
            };

            // Atomični upsert umjesto "provjeri pa update/insert" — stari kod je
            // prvo pokušao UPDATE, i ako 0 redaka pogođeno, radio INSERT. Kad bi
            // dva zahtjeva za istog korisnika stigla gotovo istovremeno (npr.
            // brzi dupli tap), oba su znala vidjeti "nema retka" i pokušati
            // INSERT, pa bi jedan pao na unique(user_id, date) constraintu i
            // taj brojač bio izgubljen. INSERT ... ON CONFLICT je atoman pa se
            // to više ne može dogoditi.
            var columnInsertValues = new Dictionary<string, string>
            {
                ["likes"] = "0",
                ["comments"] = "0",
                ["posts"] = "0",
                ["session_minutes"] = "0",
                ["followers_count"] = "(SELECT COUNT(*) FROM follows WHERE followed_id = @UserId)",
            };
            columnInsertValues[columnName] = "@Value";

            var sql = $@"
INSERT INTO activity_logs (user_id, date, likes, comments, posts, session_minutes, followers_count)
VALUES (@UserId, CURRENT_DATE,
    {columnInsertValues["likes"]},
    {columnInsertValues["comments"]},
    {columnInsertValues["posts"]},
    {columnInsertValues["session_minutes"]},
    {columnInsertValues["followers_count"]})
ON CONFLICT (user_id, date)
DO UPDATE SET {columnName} = activity_logs.{columnName} + @Value";

            await connection.ExecuteAsync(sql, new { UserId = userId, Value = value });
        }

        // Zabilježi sesiju (vrijeme provedeno u aplikaciji)
        public async Task TrackSessionTime(int userId, int minutes)
        {
            await UpdateDailyActivity(userId, "session", minutes);
        }

        // Zabilježi lajk
        public async Task TrackLike(int userId)
        {
            await UpdateDailyActivity(userId, "like", 1);
        }

        // Zabilježi komentar
        public async Task TrackComment(int userId)
        {
            await UpdateDailyActivity(userId, "comment", 1);
        }

        // Zabilježi objavu (video ili slika)
        public async Task TrackPost(int userId)
        {
            await UpdateDailyActivity(userId, "post", 1);
        }

        // Ažuriraj broj pratitelja
        public async Task UpdateFollowersCount(int userId, int followersCount)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                INSERT INTO activity_logs (user_id, date, followers_count, likes, comments, posts, session_minutes)
                VALUES (@UserId, CURRENT_DATE, @FollowersCount, 0, 0, 0, 0)
                ON CONFLICT (user_id, date) 
                DO UPDATE SET 
                    followers_count = @FollowersCount";

            await connection.ExecuteAsync(sql, new { UserId = userId, FollowersCount = followersCount });
        }

        // Dohvati detaljnu statistiku za zadnji N dana
        public async Task<IEnumerable<DailyActivity>> GetDailyStatsAsync(int userId, int days = 7)
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
                SELECT 
                    TO_CHAR(date, 'YYYY-MM-DD') AS Date,
                    session_minutes AS SessionMinutes,
                    likes AS Likes,
                    comments AS Comments,
                    posts AS Posts,
                    followers_count AS FollowersCount
                FROM activity_logs
                WHERE user_id = @UserId
                AND date >= CURRENT_DATE - (@Days || ' days')::INTERVAL
                ORDER BY date ASC";

            var stats = await connection.QueryAsync<DailyActivity>(sql, new { UserId = userId, Days = days });
            return stats;
        }
    }

    public interface IActivityRepository
    {
        Task<IEnumerable<ActivityStats>> GetActivityStatsAsync(int userId, string period = "daily");
        Task UpdateDailyActivity(int userId, string actionType, int value = 1);
        Task TrackSessionTime(int userId, int minutes);
        Task TrackLike(int userId);
        Task TrackComment(int userId);
        Task TrackPost(int userId);
        Task UpdateFollowersCount(int userId, int followersCount);
        Task<IEnumerable<DailyActivity>> GetDailyStatsAsync(int userId, int days = 7);
    }

    public class ActivityStats
    {
        public string Date { get; set; } = string.Empty;
        public int SessionMinutes { get; set; }
        public int Likes { get; set; }
        public int Comments { get; set; }
        public int Posts { get; set; }
        public int FollowersCount { get; set; }
    }

    public class DailyActivity
    {
        public string Date { get; set; } = string.Empty;
        public int SessionMinutes { get; set; }
        public int Likes { get; set; }
        public int Comments { get; set; }
        public int Posts { get; set; }
        public int FollowersCount { get; set; }
    }
}