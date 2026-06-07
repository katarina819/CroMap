using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;
using Dapper;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/activity-groups")]
    public class ActivityGroupsController : ControllerBase
    {
        private readonly string _connectionString;

        public ActivityGroupsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
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
            // NOVO: dohvati userId iz JWT
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? creatorUserId = int.TryParse(userIdStr, out int uid) ? uid : null;

            var groupId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

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
                request.CreatorName,
                request.Activity,
                Description = request.Description ?? "",
                request.Latitude,
                request.Longitude,
                request.LocationName,
                request.MaxPeople,
                Members = request.CreatorName,
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
                    request.CreatorName,
                    CreatorUserId = creatorUserId, // NOVO
                    CreatorAvatar = creatorAvatar, // NOVO
                    request.Activity,
                    request.Description,
                    request.Latitude,
                    request.Longitude,
                    request.LocationName,
                    request.MaxPeople,
                    CreatedAt = now,
                    ExpiresAt = now.AddHours(48),
                    Members = new[] { request.CreatorName },
                    MemberCount = 1
                }
            });
        }

        // POST: api/activity-groups/{id}/join - Pridruži se grupi
        [HttpPost("{id}/join")]
        public async Task<IActionResult> JoinGroup(string id, [FromBody] JoinGroupRequest request)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Dohvati grupu
                var group = await connection.QueryFirstOrDefaultAsync<ActivityGroup>(
                    "SELECT * FROM activity_groups WHERE id = @Id",
                    new { Id = id });

                if (group == null)
                {
                    return NotFound(new { error = "Grupa ne postoji" });
                }

                var members = group.Members?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

                if (members.Contains(request.UserName))
                {
                    return BadRequest(new { error = "Već ste član grupe" });
                }

                if (members.Count >= group.MaxPeople)
                {
                    return BadRequest(new { error = "Grupa je puna" });
                }

                members.Add(request.UserName);
                var newMembers = string.Join(",", members);

                await connection.ExecuteAsync(
                    "UPDATE activity_groups SET members = @Members WHERE id = @Id",
                    new { Members = newMembers, Id = id });

                return Ok(new { success = true, message = "Pridružili ste se grupi" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // DELETE: api/activity-groups/{id}/leave - Napusti grupu
        [HttpDelete("{id}/leave")]
        public async Task<IActionResult> LeaveGroup(string id, [FromQuery] string userName)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var group = await connection.QueryFirstOrDefaultAsync<ActivityGroup>(
                    "SELECT * FROM activity_groups WHERE id = @Id",
                    new { Id = id });

                if (group == null)
                {
                    return NotFound(new { error = "Grupa ne postoji" });
                }

                var members = group.Members?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

                if (!members.Contains(userName))
                {
                    return BadRequest(new { error = "Niste član grupe" });
                }

                members.Remove(userName);
                var newMembers = string.Join(",", members);

                await connection.ExecuteAsync(
                    "UPDATE activity_groups SET members = @Members WHERE id = @Id",
                    new { Members = newMembers, Id = id });

                return Ok(new { success = true, message = "Napustili ste grupu" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // DELETE: api/activity-groups/{id} - Obriši grupu (samo kreator)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGroup(string id, [FromQuery] string creatorName)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var group = await connection.QueryFirstOrDefaultAsync<ActivityGroup>(
                    "SELECT * FROM activity_groups WHERE id = @Id",
                    new { Id = id });

                if (group == null)
                {
                    return NotFound(new { error = "Grupa ne postoji" });
                }

                if (group.CreatorName != creatorName)
                {
                    return Forbid("Samo kreator može obrisati grupu");
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
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST: api/activity-groups/{id}/messages - Pošalji poruku u grupu
        [HttpPost("{id}/messages")]
        public async Task<IActionResult> SendMessage(string id,
    [FromBody] GroupMessageRequest request)
        {
            // NOVO: dohvati userId iz JWT
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? senderId = int.TryParse(userIdStr, out int uid) ? uid : null;

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var group = await connection.QueryFirstOrDefaultAsync<ActivityGroup>(
                "SELECT * FROM activity_groups WHERE id = @Id", new { Id = id });
            if (group == null) return NotFound(new { error = "Grupa ne postoji" });

            var members = group.Members?.Split(',',
                StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
            if (!members.Contains(request.UserName))
                return BadRequest(new { error = "Morate biti član grupe" });

            // NOVO: spremi user_id uz poruku
            var sql = @"INSERT INTO group_messages 
        (id, group_id, user_name, text, created_at, user_id)
        VALUES (@Id, @GroupId, @UserName, @Text, @CreatedAt, @UserId)";

            await connection.ExecuteAsync(sql, new
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = id,
                request.UserName,
                request.Text,
                CreatedAt = DateTime.UtcNow,
                UserId = senderId // NOVO
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