using CroMap.Data;
using Dapper;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CroMap.Repositories
{
    public class AdminRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public AdminRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task SeedAdminUser()
        {
            // Lozinka admina više nije zapisana u kodu. Dosad je stajala ovdje
            // doslovno ("Admin@CroMap2024!@#"), a repozitorij je u gitu — tko
            // god ga vidi mogao se prijaviti kao administrator. Sada dolazi iz
            // varijable okoline ADMIN_SEED_PASSWORD; ako nije postavljena,
            // admin se jednostavno ne kreira (postojeći ostaje netaknut).
            var seedPassword = Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD");

            using var connection = _dbConnection.CreateConnection();

            var adminEmail = Environment.GetEnvironmentVariable("ADMIN_SEED_EMAIL")
                             ?? "admin@cromap.com";

            // Provjeri postoji li admin
            var checkQuery = "SELECT COUNT(*) FROM users WHERE email = @Email";
            var exists = await connection.ExecuteScalarAsync<int>(checkQuery, new { Email = adminEmail });

            if (exists == 0)
            {
                if (string.IsNullOrWhiteSpace(seedPassword) || seedPassword.Length < 12)
                {
                    Console.WriteLine(
                        "[SeedAdminUser] ADMIN_SEED_PASSWORD nije postavljen (ili je kraći " +
                        "od 12 znakova) — administrator nije kreiran.");
                    return;
                }

                var passwordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword);

                var insertQuery = @"
            INSERT INTO users (email, username, first_name, last_name, password_hash, birth_date, is_admin, created_at)
            VALUES (@Email, @Username, @FirstName, @LastName, @PasswordHash, @BirthDate, true, NOW())";

                await connection.ExecuteAsync(insertQuery, new
                {
                    Email = adminEmail,
                    Username = "admin_cromap",
                    FirstName = "Admin",
                    LastName = "CroMap",
                    PasswordHash = passwordHash,
                    BirthDate = new DateTime(1990, 1, 1) // Default datum rođenja
                });
            }
        }


        // Dohvati sve korisnike sa statistikama
        //
        // Uz brojače se vraća i datum rođenja (godina i izračunata dob), jer
        // ga admin panel prikazuje uz korisnika — dosad je stajao u bazi, ali
        // ga nijedan admin upit nije dohvaćao. Datum je nullable: korisnici
        // prijavljeni preko Googlea ga ispune tek u "dovrši profil" koraku.
        //
        // LastActiveAt je zadnji dan na kojem se korisnik uopće pojavio —
        // najnoviji od zapisa u activity_logs i vlastitih lajkova, komentara
        // i objava. Bez toga se iz popisa nije vidjelo tko je još aktivan, a
        // tko se registrirao pa nestao.
        public async Task<IEnumerable<AdminUserDto>> GetAllUsersWithStatsAsync()
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        SELECT 
            u.id,
            u.first_name as FirstName,
            u.last_name as LastName,
            u.username,
            u.email,
            u.created_at as CreatedAt,
            u.birth_date as BirthDate,
            EXTRACT(YEAR FROM u.birth_date)::int as BirthYear,
            CASE
                WHEN u.birth_date IS NULL THEN NULL
                ELSE EXTRACT(YEAR FROM AGE(CURRENT_DATE, u.birth_date))::int
            END as Age,
            COALESCE(video_stats.total_posts, 0) as TotalPosts,
            COALESCE(video_stats.total_likes, 0) as TotalLikes,
            COALESCE(video_stats.total_comments, 0) as TotalComments,
            COALESCE(SUM(a.session_minutes), 0) as TotalSessionMinutes,
            COALESCE(followers_count.cnt, 0) as FollowersCount,
            COALESCE(following_count.cnt, 0) as FollowingCount,
            -- GREATEST u PostgreSQL-u preskače NULL-ove, pa je rezultat NULL
            -- samo ako korisnik nema ni jedan trag aktivnosti.
            GREATEST(MAX(a.date), last_content.last_date) as LastActiveAt
        FROM users u
        LEFT JOIN (
            SELECT 
                v.user_id,
                COUNT(DISTINCT v.id) as total_posts,
                COUNT(DISTINCT l.id) as total_likes,
                COUNT(DISTINCT c.id) as total_comments
            FROM videos v
            LEFT JOIN likes l ON l.video_id = v.id
            LEFT JOIN comments c ON c.video_id = v.id
            GROUP BY v.user_id
        ) video_stats ON video_stats.user_id = u.id
        LEFT JOIN activity_logs a ON a.user_id = u.id
        LEFT JOIN (
            SELECT followed_id, COUNT(*) as cnt
            FROM follows
            GROUP BY followed_id
        ) followers_count ON followers_count.followed_id = u.id
        LEFT JOIN (
            SELECT follower_id, COUNT(*) as cnt
            FROM follows
            GROUP BY follower_id
        ) following_count ON following_count.follower_id = u.id
        LEFT JOIN (
            SELECT user_id, MAX(created_at)::date AS last_date
            FROM (
                SELECT user_id, created_at FROM likes
                UNION ALL SELECT user_id, created_at FROM comments
                UNION ALL SELECT user_id, created_at FROM videos
            ) src
            GROUP BY user_id
        ) last_content ON last_content.user_id = u.id
        GROUP BY 
            u.id, u.first_name, u.last_name, u.username, u.email, u.created_at, u.birth_date,
            video_stats.total_posts, video_stats.total_likes, video_stats.total_comments,
            followers_count.cnt, following_count.cnt, last_content.last_date
        ORDER BY u.created_at DESC";

            return await connection.QueryAsync<AdminUserDto>(sql);
        }

        // Dohvati admin summary statistiku
        //
        // Uz ukupne brojeve vraća i raspodjelu korisnika po dobi (iz
        // birth_date) te koliko ih je bilo aktivno u zadnjih 7 dana. Oboje
        // služi admin panelu da se na prvi pogled vidi TKO je publika i koliko
        // ih se još vraća, a ne samo koliko ih se ukupno registriralo.
        public async Task<AdminSummaryDto> GetAdminSummaryAsync()
        {
            using var connection = _dbConnection.CreateConnection();

            var sql = @"
        WITH ages AS (
            SELECT EXTRACT(YEAR FROM AGE(CURRENT_DATE, birth_date))::int AS age
            FROM users
        ),
        active AS (
            SELECT DISTINCT user_id FROM (
                SELECT user_id FROM activity_logs
                    WHERE date >= CURRENT_DATE - INTERVAL '7 days' AND session_minutes > 0
                UNION ALL SELECT user_id FROM likes    WHERE created_at >= CURRENT_DATE - INTERVAL '7 days'
                UNION ALL SELECT user_id FROM comments WHERE created_at >= CURRENT_DATE - INTERVAL '7 days'
                UNION ALL SELECT user_id FROM videos   WHERE created_at >= CURRENT_DATE - INTERVAL '7 days'
            ) src
        )
        SELECT
            (SELECT COUNT(*) FROM users)::int                                  AS TotalUsers,
            (SELECT COUNT(*) FROM likes)::int                                  AS TotalLikes,
            (SELECT COUNT(*) FROM comments)::int                               AS TotalComments,
            (SELECT COALESCE(SUM(session_minutes), 0) FROM activity_logs)::int AS TotalMinutes,
            (SELECT COUNT(*) FROM active)::int                                 AS ActiveLast7Days,
            (SELECT ROUND(AVG(age), 1) FROM ages WHERE age IS NOT NULL)::float8 AS AverageAge,
            (SELECT COUNT(*) FROM ages WHERE age IS NULL)::int                 AS UsersWithoutBirthDate,
            (SELECT COUNT(*) FROM ages WHERE age < 18)::int                    AS AgeUnder18,
            (SELECT COUNT(*) FROM ages WHERE age BETWEEN 18 AND 24)::int       AS Age18To24,
            (SELECT COUNT(*) FROM ages WHERE age BETWEEN 25 AND 34)::int       AS Age25To34,
            (SELECT COUNT(*) FROM ages WHERE age BETWEEN 35 AND 44)::int       AS Age35To44,
            (SELECT COUNT(*) FROM ages WHERE age BETWEEN 45 AND 54)::int       AS Age45To54,
            (SELECT COUNT(*) FROM ages WHERE age >= 55)::int                   AS Age55Plus";

            var row = await connection.QueryFirstOrDefaultAsync<AdminSummaryRow>(sql);
            if (row is null)
                return new AdminSummaryDto();

            return new AdminSummaryDto
            {
                TotalUsers = row.TotalUsers,
                TotalLikes = row.TotalLikes,
                TotalComments = row.TotalComments,
                TotalMinutes = row.TotalMinutes,
                ActiveLast7Days = row.ActiveLast7Days,
                AverageAge = row.AverageAge,
                UsersWithoutBirthDate = row.UsersWithoutBirthDate,
                AgeGroups = new List<AgeGroupDto>
                {
                    new() { Label = "<18",      Count = row.AgeUnder18 },
                    new() { Label = "18-24",    Count = row.Age18To24 },
                    new() { Label = "25-34",    Count = row.Age25To34 },
                    new() { Label = "35-44",    Count = row.Age35To44 },
                    new() { Label = "45-54",    Count = row.Age45To54 },
                    new() { Label = "55+",      Count = row.Age55Plus },
                    new() { Label = "Nepoznato", Count = row.UsersWithoutBirthDate },
                }
            };
        }

        // Spremi ocjenu plana
        public async Task<int> SavePlanRatingAsync(string userName, string destination, int rating)
        {
            using var connection = _dbConnection.CreateConnection();
            var sql = @"
        INSERT INTO plan_ratings (user_name, destination, rating, created_at)
        VALUES (@UserName, @Destination, @Rating, NOW())
        RETURNING id";
            return await connection.ExecuteScalarAsync<int>(sql, new { UserName = userName, Destination = destination, Rating = rating });
        }

        // Dohvati sve ocjene planova (za admin)
        public async Task<IEnumerable<PlanRatingDto>> GetPlanRatingsAsync()
        {
            using var connection = _dbConnection.CreateConnection();
            var sql = @"
        SELECT id, user_name as UserName, destination, rating, created_at as CreatedAt
        FROM plan_ratings
        ORDER BY created_at DESC";
            return await connection.QueryAsync<PlanRatingDto>(sql);
        }

        // Spremi support report
        public async Task SaveSupportReportAsync(SupportReportRequest req)
        {
            using var connection = _dbConnection.CreateConnection();
            var sql = @"
        INSERT INTO support_reports (type, message, user_name, username, created_at)
        VALUES (@Type, @Message, @UserName, @UserUsername, NOW())";
            await connection.ExecuteAsync(sql, req);
        }

        // Dohvati sve support reporte (za admin)
        public async Task<IEnumerable<SupportReportDto>> GetSupportReportsAsync()
        {
            using var connection = _dbConnection.CreateConnection();
            var sql = @"
        SELECT id, type, message,
       user_name as UserName,
       username as UserUsername,
       created_at as CreatedAt
FROM support_reports
ORDER BY created_at DESC";
            return await connection.QueryAsync<SupportReportDto>(sql);
        }
    }

    public class AdminUserDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateOnly? BirthDate { get; set; }
        public int? BirthYear { get; set; }
        public int? Age { get; set; }
        public DateOnly? LastActiveAt { get; set; }
        public int TotalPosts { get; set; }
        public int TotalLikes { get; set; }
        public int TotalComments { get; set; }
        public int TotalSessionMinutes { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
    }

    public class AdminSummaryDto
    {
        public int TotalUsers { get; set; }
        public int TotalLikes { get; set; }
        public int TotalComments { get; set; }
        public int TotalMinutes { get; set; }
        public int ActiveLast7Days { get; set; }
        public double? AverageAge { get; set; }
        public int UsersWithoutBirthDate { get; set; }
        public List<AgeGroupDto> AgeGroups { get; set; } = new();
    }

    public class AgeGroupDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    // Ravni oblik u koji Dapper mapira summary upit; javni DTO iz njega slaže
    // listu dobnih skupina.
    internal class AdminSummaryRow
    {
        public int TotalUsers { get; set; }
        public int TotalLikes { get; set; }
        public int TotalComments { get; set; }
        public int TotalMinutes { get; set; }
        public int ActiveLast7Days { get; set; }
        public double? AverageAge { get; set; }
        public int UsersWithoutBirthDate { get; set; }
        public int AgeUnder18 { get; set; }
        public int Age18To24 { get; set; }
        public int Age25To34 { get; set; }
        public int Age35To44 { get; set; }
        public int Age45To54 { get; set; }
        public int Age55Plus { get; set; }
    }

    public class PlanRatingDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SavePlanRatingRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public int Rating { get; set; }
    }

    public class SupportReportRequest
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserUsername { get; set; } = string.Empty;
    }

    public class SupportReportDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserUsername { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }


}