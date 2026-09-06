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

            string logBucket;
            string srcBucket;
            string dateFormat;
            string lookback;

            // Lookback prozor mora rasti s periodom — prije je uvijek bio
            // fiksiran na 30 dana, pa je "mjesečni" prikaz grupiran po
            // DATE_TRUNC('month', ...) u praksi gotovo uvijek pokazivao samo
            // tekući (djelomični) mjesec, nikad stvarnu mjesečnu povijest.
            switch (period)
            {
                case "weekly":
                    logBucket = "DATE_TRUNC('week', date)::date";
                    srcBucket = "DATE_TRUNC('week', created_at)::date";
                    dateFormat = "YYYY-MM-DD";
                    lookback = "'90 days'";
                    break;
                case "monthly":
                    logBucket = "DATE_TRUNC('month', date)::date";
                    srcBucket = "DATE_TRUNC('month', created_at)::date";
                    dateFormat = "YYYY-MM";
                    lookback = "'365 days'";
                    break;
                default: // daily
                    logBucket = "date";
                    srcBucket = "created_at::date";
                    dateFormat = "YYYY-MM-DD";
                    lookback = "'30 days'";
                    break;
            }

            // Lajkovi, komentari i objave se sada BROJE IZ IZVORNIH TABLICA
            // (likes / comments / videos), a ne iz brojača u activity_logs.
            // Ti brojači ovise o bazi podataka o okidačima (triggerima) koje je
            // jedna ranija migracija u baza.sql greškom obrisala, pa su na
            // postojećim bazama ostajali na nuli — korisnik bi lajkao i
            // komentirao, a arhiva aktivnosti bi i dalje pokazivala 0. Brojanje
            // iz izvora je uz to i idempotentno: ne može se dvostruko zbrojiti
            // niti "izgubiti" ako neki okidač nedostaje ili se pokrene dvaput.
            // Iz activity_logs se i dalje čita samo vrijeme u aplikaciji, jer
            // za njega ne postoji izvorna tablica.
            //
            // FollowersCount je trenutni broj pratitelja, čitan uživo iz
            // tablice follows. Prije je dolazio iz istog (neažuriranog) brojača
            // i k tome ga je aplikacija čitala iz NAJSTARIJEG retka niza, pa je
            // gotovo uvijek pokazivao 0.
            var sql = $@"
                WITH buckets AS (
                    SELECT {logBucket} AS bucket,
                           SUM(session_minutes) AS session_minutes,
                           0 AS likes, 0 AS comments, 0 AS posts
                    FROM activity_logs
                    WHERE user_id = @UserId
                      AND date >= CURRENT_DATE - INTERVAL {lookback}
                    GROUP BY 1

                    UNION ALL

                    SELECT {srcBucket} AS bucket, 0, COUNT(*), 0, 0
                    FROM likes
                    WHERE user_id = @UserId
                      AND created_at >= CURRENT_DATE - INTERVAL {lookback}
                    GROUP BY 1

                    UNION ALL

                    SELECT {srcBucket} AS bucket, 0, 0, COUNT(*), 0
                    FROM comments
                    WHERE user_id = @UserId
                      AND created_at >= CURRENT_DATE - INTERVAL {lookback}
                    GROUP BY 1

                    UNION ALL

                    SELECT {srcBucket} AS bucket, 0, 0, 0, COUNT(*)
                    FROM videos
                    WHERE user_id = @UserId
                      AND created_at >= CURRENT_DATE - INTERVAL {lookback}
                    GROUP BY 1
                )
                SELECT
                    TO_CHAR(bucket, '{dateFormat}') AS Date,
                    COALESCE(SUM(session_minutes), 0)::int AS SessionMinutes,
                    COALESCE(SUM(likes), 0)::int AS Likes,
                    COALESCE(SUM(comments), 0)::int AS Comments,
                    COALESCE(SUM(posts), 0)::int AS Posts,
                    (SELECT COUNT(*) FROM follows WHERE followed_id = @UserId)::int AS FollowersCount
                FROM buckets
                GROUP BY bucket
                ORDER BY bucket DESC";

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