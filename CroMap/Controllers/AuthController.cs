using Microsoft.AspNetCore.Mvc;
using CroMap.Models;
using CroMap.Repositories;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CroMap.Data; 
using Dapper;

namespace CroMap.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserRepository _repo;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;
        private readonly DatabaseConnection _dbConnection;

        public AuthController(UserRepository repo, IConfiguration configuration, ILogger<AuthController> logger, DatabaseConnection dbConnection)
        {
            _repo = repo;
            _configuration = configuration;
            _logger = logger;
            _dbConnection = dbConnection;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserDto userDto)
        {
            // Validacija
            if (string.IsNullOrWhiteSpace(userDto.Username))
                return BadRequest(new { message = "Korisničko ime je obavezno" });

            if (userDto.Username.Length < 3)
                return BadRequest(new { message = "Korisničko ime mora imati najmanje 3 znaka" });

            if (string.IsNullOrWhiteSpace(userDto.FirstName))
                return BadRequest(new { message = "Ime je obavezno" });

            if (string.IsNullOrWhiteSpace(userDto.LastName))
                return BadRequest(new { message = "Prezime je obavezno" });

            if (string.IsNullOrWhiteSpace(userDto.Password) || userDto.Password.Length < 6)
                return BadRequest(new { message = "Lozinka mora imati najmanje 6 znakova" });

            if (string.IsNullOrWhiteSpace(userDto.Email) && string.IsNullOrWhiteSpace(userDto.Phone))
                return BadRequest(new { message = "Email ili telefon su obavezni" });

            if (!userDto.BirthDate.HasValue)
                return BadRequest(new { message = "Datum rođenja je obavezan" });

            var user = new User
            {
                Username = userDto.Username.ToLower(),
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                Email = userDto.Email,
                Phone = userDto.Phone,
                PasswordHash = userDto.Password,  // ← PLAIN TEXT, HASHIRAT ĆE SE U REPOSITORYJU
                BirthDate = userDto.BirthDate.Value,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _repo.RegisterAsync(user);
                _logger.LogInformation($"✅ User registered: {user.Username}");
                return Ok(new { message = "Registracija uspješna" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error");

                if (ex.Message.Contains("23505") || ex.Message.Contains("duplicate key"))
                {
                    return BadRequest(new { message = "Korisničko ime, email ili telefon već postoji" });
                }

                return BadRequest(new { message = "Greška pri registraciji: " + ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                _logger.LogInformation($"🔑 Login attempt: {dto.Username}");

                // Login po username (ne email/telefon)
                var user = await _repo.LoginByUsernameAsync(dto.Username, dto.Password);

                if (user == null)
                {
                    _logger.LogWarning($"❌ Login failed for: {dto.Username}");
                    return Unauthorized(new { message = "Neispravno korisničko ime ili lozinka" });
                }

                _logger.LogInformation($"✅ Login successful: {user.Username} (ID: {user.Id})");

                var token = GenerateJwtToken(user);

                return Ok(new
                {
                    token = token,
                    userId = user.Id,
                    username = user.Username,  // ← DODAJ username
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    phone = user.Phone
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                return StatusCode(500, new { message = "Greška na serveru" });
            }
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                // Direktan SQL upit koji uključuje avatar iz user_profiles tablice
                var sql = @"
            SELECT 
                u.id, 
                u.first_name AS FirstName, 
                u.last_name AS LastName, 
                u.username AS Username,
                p.avatar AS Avatar
            FROM users u
            LEFT JOIN user_profiles p ON u.id = p.user_id
            ORDER BY u.first_name, u.last_name";

                using var connection = _dbConnection.CreateConnection();
                var users = await connection.QueryAsync(sql);

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, new { message = "Greška pri dohvaćanju korisnika" });
            }
        }

        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var user = await _repo.GetUserByIdAsync(id);

                if (user == null)
                    return NotFound(new { message = "Korisnik nije pronađen" });

                // Dohvati avatar iz user_profiles
                var avatar = await _repo.GetUserAvatarAsync(id);

                return Ok(new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Username,
                    Avatar = avatar
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user {id}");
                return StatusCode(500, new { message = "Greška pri dohvaćanju korisnika" });
            }
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserDto userDto)
        {
            try
            {
                // Validacija
                if (string.IsNullOrWhiteSpace(userDto.FirstName))
                    return BadRequest(new { message = "Ime je obavezno" });

                if (string.IsNullOrWhiteSpace(userDto.LastName))
                    return BadRequest(new { message = "Prezime je obavezno" });

                // 🔥 DODAJ PROVJERU ZA BIRTHDATE
                if (!userDto.BirthDate.HasValue)
                    return BadRequest(new { message = "Datum rođenja je obavezan" });

                var user = new User
                {
                    Id = id,
                    Username = userDto.Username,  // ← DODAJ I USERNAME
                    FirstName = userDto.FirstName,
                    LastName = userDto.LastName,
                    Email = userDto.Email,
                    Phone = userDto.Phone,
                    BirthDate = userDto.BirthDate.Value,  // ← KORISTI .Value ZA PRETVORBU
                    CreatedAt = DateTime.UtcNow  // Ovo će se overwrite-ati u bazi, ali dobro je imati
                };

                await _repo.UpdateUserAsync(user);

                return Ok(new { message = "Korisnik ažuriran" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user {id}");
                return StatusCode(500, new { message = "Greška pri ažuriranju korisnika" });
            }
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                await _repo.DeleteUserAsync(id);
                return Ok(new { message = "Korisnik obrisan" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting user {id}");
                return StatusCode(500, new { message = "Greška pri brisanju korisnika" });
            }
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
        new Claim("firstName", user.FirstName ?? ""),
        new Claim("lastName", user.LastName ?? "")
    };

            if (!string.IsNullOrEmpty(user.Email))
                claims.Add(new Claim(ClaimTypes.Email, user.Email));

            if (!string.IsNullOrEmpty(user.Phone))
                claims.Add(new Claim("phone", user.Phone));

            // 🔥 DODAJTE OVO - Admin role
            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                claims.Add(new Claim("isAdmin", "true"));
            }
            else
            {
                claims.Add(new Claim(ClaimTypes.Role, "User"));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }


    }
}