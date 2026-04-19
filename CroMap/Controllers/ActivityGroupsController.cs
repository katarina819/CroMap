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
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
            SELECT 
                id, 
                creator_name AS CreatorName, 
                activity, 
                description, 
                latitude, 
                longitude, 
                location_name AS LocationName, 
                max_people AS MaxPeople, 
                created_at AS CreatedAt, 
                expires_at AS ExpiresAt,
                members
            FROM activity_groups 
            WHERE expires_at > @now 
            ORDER BY created_at DESC";

                var groups = await connection.QueryAsync<ActivityGroup>(sql, new { now = DateTime.UtcNow });

                var result = groups.Select(g => new
                {
                    g.Id,
                    g.CreatorName,
                    g.Activity,
                    g.Description,
                    g.Latitude,
                    g.Longitude,
                    g.LocationName,
                    g.MaxPeople,
                    g.CreatedAt,
                    g.ExpiresAt,
                    Members = g.Members?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    MemberCount = g.Members?.Split(',', StringSplitOptions.RemoveEmptyEntries).Length ?? 0,
                    Messages = new List<object>() // ← DODAJ OVO - prazan niz poruka
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST: api/activity-groups - Kreiraj grupu
        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "Niste prijavljeni" });
                }

                var groupId = Guid.NewGuid().ToString();
                var now = DateTime.UtcNow;

                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    INSERT INTO activity_groups 
                        (id, creator_name, activity, description, latitude, longitude, location_name, max_people, members, created_at, expires_at)
                    VALUES 
                        (@Id, @CreatorName, @Activity, @Description, @Latitude, @Longitude, @LocationName, @MaxPeople, @Members, @CreatedAt, @ExpiresAt)";

                await connection.ExecuteAsync(sql, new
                {
                    Id = groupId,
                    CreatorName = request.CreatorName,
                    Activity = request.Activity,
                    Description = request.Description ?? "",
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    LocationName = request.LocationName,
                    MaxPeople = request.MaxPeople,
                    Members = request.CreatorName,
                    CreatedAt = now,
                    ExpiresAt = now.AddHours(48)
                });

                var newGroup = new
                {
                    Id = groupId,
                    request.CreatorName,
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
                };

                return Ok(new { success = true, group = newGroup });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
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
        public async Task<IActionResult> SendMessage(string id, [FromBody] GroupMessageRequest request)
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

                if (!members.Contains(request.UserName))
                {
                    return BadRequest(new { error = "Morate biti član grupe za slanje poruka" });
                }

                var messageId = Guid.NewGuid().ToString();

                var sql = @"
                    INSERT INTO group_messages (id, group_id, user_name, text, created_at)
                    VALUES (@Id, @GroupId, @UserName, @Text, @CreatedAt)";

                await connection.ExecuteAsync(sql, new
                {
                    Id = messageId,
                    GroupId = id,
                    UserName = request.UserName,
                    Text = request.Text,
                    CreatedAt = DateTime.UtcNow
                });

                return Ok(new { success = true, message = "Poruka poslana" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: api/activity-groups/{id}/messages - Dohvati poruke grupe
        [HttpGet("{id}/messages")]
        public async Task<IActionResult> GetMessages(string id)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT 
                        user_name AS UserName, 
                        text AS Text, 
                        created_at AS Time
                    FROM group_messages 
                    WHERE group_id = @GroupId 
                    ORDER BY created_at ASC";

                var messages = await connection.QueryAsync<GroupMessageDto>(sql, new { GroupId = id });

                return Ok(messages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
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

    public class GroupMessageDto
    {
        public string UserName { get; set; }
        public string Text { get; set; }
        public DateTime Time { get; set; }
    }
}