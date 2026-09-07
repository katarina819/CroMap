using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;
using Dapper;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/activity-groups")]
    // Cijeli je kontroler ranije bio anoniman, a identitet se čitao iz tijela
    // zahtjeva ("userName", "creatorName"). To je značilo da je bilo tko, bez
    // ijednog tokena, mogao: kreirati grupe u tuđe ime, izbaciti bilo kojeg
    // člana iz bilo koje grupe, čitati i pisati u tuđe grupne razgovore, te —
    // najgore — obrisati bilo čiju grupu, jer je provjera vlasništva bila
    // usporedba s imenom iz query stringa, a imena kreatora javno vraća
    // GET /api/activity-groups. Sada je identitet isključivo iz JWT-a.
    [Authorize]
    public class ActivityGroupsController : ControllerBase
    {
        private readonly string _connectionString;

        public ActivityGroupsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        /// <summary>Id prijavljenog korisnika iz tokena; null ako ga nema.</summary>
        private int? CurrentUserId
        {
            get
            {
                var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(raw, out var id) ? (int?)id : null;
            }
        }

        /// <summary>
        /// Ime pod kojim se korisnik vodi u grupama. Čita se iz baze, ne iz
        /// zahtjeva — klijent više ne može tvrditi da je netko drugi. Format je
        /// isti kao dosad ("Ime Prezime"), pa postojeći zapisi ostaju valjani.
        /// </summary>
        private static async Task<string?> GetDisplayNameAsync(NpgsqlConnection connection, int userId)
        {
            var name = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT TRIM(COALESCE(first_name, '') || ' ' || COALESCE(last_name, '')) FROM users WHERE id = @Id",
                new { Id = userId });
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        // GET: api/activity-groups - Dohvati sve aktivne grupe

        [HttpGet]
        public async Task<IActionResult> GetAllGroups()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // NOVO: JOIN s user_profiles za avatar kreatora
            var sql = @"
        SELECT 
            ag.id, 
            ag.creator_name AS CreatorName, 
            ag.activity, 
            ag.description, 
            ag.latitude, 
            ag.longitude, 
            ag.location_name AS LocationName, 
            ag.max_people AS MaxPeople, 
            ag.created_at AS CreatedAt, 
            ag.expires_at AS ExpiresAt,
            ag.members,
            ag.creator_user_id AS CreatorUserId,
            COALESCE(up.avatar, '') AS CreatorAvatar
        FROM activity_groups ag
        LEFT JOIN user_profiles up ON ag.creator_user_id = up.user_id
        WHERE ag.expires_at > @now 
        ORDER BY ag.created_at DESC";

            var groups = await connection.QueryAsync<ActivityGroupWithAvatar>(sql,
                new { now = DateTime.UtcNow });

            var result = groups.Select(g => new
            {
                g.Id,
                g.CreatorName,
                g.CreatorUserId, // NOVO
                g.CreatorAvatar, // NOVO
                g.Activity,
                g.Description,
                g.Latitude,
                g.Longitude,
                g.LocationName,
                g.MaxPeople,
                g.CreatedAt,
                g.ExpiresAt,
                Members = g.Members?.Split(',',
                    StringSplitOptions.RemoveEmptyEntries).ToList()
                    ?? new List<string>(),
                Messages = new List<object>()
            });

            return Ok(result);
        }

        // POST: api/activity-groups - Kreiraj grupu
        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
        {
            var creatorUserId = CurrentUserId;
            if (creatorUserId is null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Activity))
                return BadRequest(new { error = "Aktivnost je obavezna" });
            if (request.Activity.Length > 100 || (request.Description?.Length ?? 0) > 1000
                || (request.LocationName?.Length ?? 0) > 200)
                return BadRequest(new { error = "Predugačak unos" });
            if (request.MaxPeople < 2 || request.MaxPeople > 100)
                return BadRequest(new { error = "Broj sudionika mora biti između 2 i 100" });

            var groupId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Ime kreatora dolazi iz baze, ne iz request.CreatorName.
            var creatorName = await GetDisplayNameAsync(connection, creatorUserId.Value);
            if (creatorName is null) return Unauthorized();

            // NOVO: spremi creator_user_id
            var sql = @"
        INSERT INTO activity_groups 
            (id, creator_name, activity, description, latitude, longitude, 
             location_name, max_people, members, created_at, expires_at, creator_user_id)
        VALUES 
            (@Id, @CreatorName, @Activity, @Description, @Latitude, @Longitude, 
             @LocationName, @MaxPeople, @Members, @CreatedAt, @ExpiresAt, @CreatorUserId)";

            await connection.ExecuteAsync(sql, new
            {
                Id = groupId,
                CreatorName = creatorName,
                request.Activity,
                Description = request.Description ?? "",
                request.Latitude,
                request.Longitude,
                request.LocationName,
                request.MaxPeople,
                Members = creatorName,
                CreatedAt = now,
                ExpiresAt = now.AddHours(48),
                CreatorUserId = creatorUserId // NOVO
            });

            // NOVO: dohvati avatar kreatora za response
            var avatarSql = "SELECT COALESCE(avatar, '') FROM user_profiles WHERE user_id = @UserId";
            var creatorAvatar = creatorUserId.HasValue
                ? await connection.QueryFirstOrDefaultAsync<string>(avatarSql,
                    new { UserId = creatorUserId }) ?? ""
                : "";

            return Ok(new
            {
                success = true,
                group = new
                {
                    Id = groupId,
                    CreatorName = creatorName,
                    CreatorUserId = creatorUserId,
                    CreatorAvatar = creatorAvatar, // NOVO
                    request.Activity,
                    request.Description,
                    request.Latitude,
                    request.Longitude,
                    request.LocationName,
                    request.MaxPeople,
                    CreatedAt = now,
                    ExpiresAt = now.AddHours(48),
                    Members = new[] { creatorName },
                    MemberCount = 1
                }
            });
        }

        // POST: api/activity-groups/{id}/join - Pridruži se grupi
        [HttpPost("{id}/join")]
        public async Task<IActionResult> JoinGroup(string id, [FromBody] JoinGroupRequest request)
        {
            var userId = CurrentUserId;
            if (userId is null) return Unauthorized();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // request.UserName se namjerno ignorira — tko se pridružuje
                // određuje token, ne tijelo zahtjeva.
                var userName = await GetDisplayNameAsync(connection, userId.Value);
                if (userName is null) return Unauthorized();

                var group = await connection.QueryFirstOrDefaultAsync<ActivityGroup>(
                    "SELECT * FROM activity_groups WHERE id = @Id",
                    new { Id = id });

                if (group == null)
                {
                    return NotFound(new { error = "Grupa ne postoji" });
                }

                var members = group.Members?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

                if (members.Contains(userName))
                {
                    return BadRequest(new { error = "Već ste član grupe" });
                }

                if (members.Count >= group.MaxPeople)
                {
                    return BadRequest(new { error = "Grupa je puna" });
                }

                members.Add(userName);
                var newMembers = string.Join(",", members);

                await connection.ExecuteAsync(
                    "UPDATE activity_groups SET members = @Members WHERE id = @Id",
                    new { Members = newMembers, Id = id });

                return Ok(new { success = true, message = "Pridružili ste se grupi" });
            }
            catch (Exception)
            {
                // Poruka iznimke se više ne vraća klijentu — otkrivala je
                // strukturu baze i interne putanje.
                return StatusCode(500, new { error = "Greška pri pridruživanju grupi" });
            }
        }

        // DELETE: api/activity-groups/{id}/leave - Napusti grupu
        [HttpDelete("{id}/leave")]
        public async Task<IActionResult> LeaveGroup(string id, [FromQuery] string userName)
        {
            var userId = CurrentUserId;
            if (userId is null) return Unauthorized();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Parametar userName se ignorira — dosad je omogućavao da bilo
                // tko izbaci bilo kojeg člana iz bilo koje grupe.
                var callerName = await GetDisplayNameAsync(connection, userId.Value);
                if (callerName is null) return Unauthorized();

                var group = await connection.QueryFirstOrDefaultAsync<ActivityGroup>(
                    "SELECT * FROM activity_groups WHERE id = @Id",
                    new { Id = id });

                if (group == null)
                {
                    return NotFound(new { error = "Grupa ne postoji" });
                }

                var members = group.Members?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

                if (!members.Contains(callerName))
                {
                    return BadRequest(new { error = "Niste član grupe" });
                }

                members.Remove(callerName);
                var newMembers = string.Join(",", members);

                await connection.ExecuteAsync(
                    "UPDATE activity_groups SET members = @Members WHERE id = @Id",
                    new { Members = newMembers, Id = id });

                return Ok(new { success = true, message = "Napustili ste grupu" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Greška pri napuštanju grupe" });
            }
        }

        // DELETE: api/activity-groups/{id} - Obriši grupu (samo kreator)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGroup(string id, [FromQuery] string creatorName)
        {
            var userId = CurrentUserId;
            if (userId is null) return Unauthorized();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var group = await connection.QueryFirstOrDefaultAsync<ActivityGroupWithAvatar>(
                    "SELECT * FROM activity_groups WHERE id = @Id",
                    new { Id = id });

                if (group == null)
                {
                    return NotFound(new { error = "Grupa ne postoji" });
                }

                // Vlasništvo se provjerava po id-u kreatora iz tokena. Prije se
                // uspoređivalo s imenom iz query stringa, a ta su imena javna
                // (vraća ih GET /api/activity-groups) — dakle bilo tko je mogao
                // obrisati bilo čiju grupu zajedno sa svim porukama.
                bool isOwner;
                if (group.CreatorUserId.HasValue)
                {
                    isOwner = group.CreatorUserId.Value == userId.Value;
                }
                else
                {
                    // Stari zapisi nastali prije nego što se spremao id kreatora:
                    // usporedi s imenom iz BAZE za prijavljenog korisnika.
                    var callerName = await GetDisplayNameAsync(connection, userId.Value);
                    isOwner = callerName != null && group.CreatorName == callerName;
                }

                if (!isOwner)
                {
                    return StatusCode(403,
                        new { error = "Samo kreator može obrisati grupu" });
                }

                // Prvo obriši sve poruke
                await connection.ExecuteAsync(
                    "DELETE FROM group_messages WHERE group_id = @GroupId",
                    new { GroupId = id });

                // Zatim obriši grupu
                await connection.ExecuteAsync(
                    "DELETE FROM activity_groups WHERE id = @Id",
                    new { Id = id });

                return Ok(new { success = true, message = "Grupa je obrisana" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Greška pri brisanju grupe" });
            }
        }

        // POST: api/activity-groups/{id}/messages - Pošalji poruku u grupu
        [HttpPost("{id}/messages")]
        public async Task<IActionResult> SendMessage(string id,
    [FromBody] GroupMessageRequest request)
        {
            var senderId = CurrentUserId;
            if (senderId is null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { error = "Poruka je prazna" });
            if (request.Text.Length > 2000)
                return BadRequest(new { error = "Poruka je predugačka" });

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Pošiljatelj je onaj tko drži token — dosad se ime uzimalo iz
            // tijela zahtjeva, pa je bilo tko mogao pisati u tuđe ime.
            var senderName = await GetDisplayNameAsync(connection, senderId.Value);
            if (senderName is null) return Unauthorized();

            var group = await connection.QueryFirstOrDefaultAsync<ActivityGroup>(
                "SELECT * FROM activity_groups WHERE id = @Id", new { Id = id });
            if (group == null) return NotFound(new { error = "Grupa ne postoji" });

            var members = group.Members?.Split(',',
                StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
            if (!members.Contains(senderName))
                return StatusCode(403,
                    new { error = "Morate biti član grupe" });

            // NOVO: spremi user_id uz poruku
            var sql = @"INSERT INTO group_messages 
        (id, group_id, user_name, text, created_at, user_id)
        VALUES (@Id, @GroupId, @UserName, @Text, @CreatedAt, @UserId)";

            await connection.ExecuteAsync(sql, new
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = id,
                UserName = senderName,
                request.Text,
                CreatedAt = DateTime.UtcNow,
                UserId = senderId
            });

            return Ok(new { success = true });
        }

        // GET: api/activity-groups/{id}/messages - Dohvati poruke grupe
        [HttpGet("{id}/messages")]
        public async Task<IActionResult> GetMessages(string id)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // NOVO: JOIN s user_profiles za avatar pošiljatelja
            var sql = @"
        SELECT 
            gm.user_name AS UserName, 
            gm.text AS Text, 
            gm.created_at AS Time,
            COALESCE(up.avatar, '') AS UserAvatar
        FROM group_messages gm
        LEFT JOIN user_profiles up ON gm.user_id = up.user_id
        WHERE gm.group_id = @GroupId 
        ORDER BY gm.created_at ASC";

            var messages = await connection.QueryAsync<GroupMessageDto>(sql,
                new { GroupId = id });

            return Ok(messages);
        }
    }

    // Request models
    public class CreateGroupRequest
    {
        public string CreatorName { get; set; }
        public string Activity { get; set; }
        public string Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string LocationName { get; set; }
        public int MaxPeople { get; set; }
    }

    public class JoinGroupRequest
    {
        public string UserName { get; set; }
    }

    public class GroupMessageRequest
    {
        public string UserName { get; set; }
        public string Text { get; set; }
    }

    // Entity models
    public class ActivityGroup
    {
        public string Id { get; set; }
        public string CreatorName { get; set; }
        public string Activity { get; set; }
        public string Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string LocationName { get; set; }
        public int MaxPeople { get; set; }
        public string Members { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    

    public class ActivityGroupWithAvatar : ActivityGroup
    {
        public int? CreatorUserId { get; set; }
        public string CreatorAvatar { get; set; }
    }

    // Ažuriraj GroupMessageDto:
    public class GroupMessageDto
    {
        public string UserName { get; set; }
        public string Text { get; set; }
        public DateTime Time { get; set; }
        public string UserAvatar { get; set; } // NOVO
    }
}