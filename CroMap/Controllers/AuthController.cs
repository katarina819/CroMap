using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CroMap.Data; 
using CroMap.Models;
using CroMap.Repositories;
using CroMap.Services;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

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
        private readonly IEmailService _emailService;
        private readonly PasswordResetRepository _resetRepo;

        public AuthController(UserRepository repo, IConfiguration configuration, ILogger<AuthController> logger, DatabaseConnection dbConnection, IEmailService emailService,
    PasswordResetRepository resetRepo)
        {
            _repo = repo;
            _configuration = configuration;
            _logger = logger;
            _dbConnection = dbConnection;
            _emailService = emailService;
            _resetRepo = resetRepo;
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

                // Pošalji dobrodošlicu ako postoji email
                if (!string.IsNullOrWhiteSpace(userDto.Email))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(
                                userDto.Email,
                                "Dobrodošli u CroMap! 🗺️",
                                BuildWelcomeEmail(userDto.FirstName)
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Welcome email failed, ignoring");
                        }
                    });
                }

                return Ok(new { message = "Registracija uspješna" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error");
                if (ex.Message.Contains("23505") || ex.Message.Contains("duplicate key"))
                    return Conflict(new { message = "Korisničko ime, email ili telefon već postoji" });
                return BadRequest(new { message = "Greška pri registraciji" });
            }
        }


        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { message = "Email je obavezan" });

            try
            {
                // Pronađi korisnika po emailu
                using var conn = _dbConnection.CreateConnection();
                var user = await conn.QueryFirstOrDefaultAsync<User>(
                    "SELECT id, first_name AS FirstName, email FROM users WHERE LOWER(email) = LOWER(@Email)",
                    new { dto.Email });

                // Uvijek vrati OK (sigurnost - ne otkrivamo postoji li email)
                if (user == null)
                {
                    _logger.LogInformation($"Password reset requested for non-existent email: {dto.Email}");
                    return Ok(new { message = "Ako email postoji, poslan je kod za reset" });
                }

                // Generiraj 6-znamenkasti kod
                var code = new Random().Next(100000, 999999).ToString();

                await _resetRepo.CreateResetTokenAsync(user.Id, code);

                await _emailService.SendEmailAsync(
                    user.Email,
                    "CroMap - Reset lozinke 🔐",
                    BuildResetEmail(user.FirstName, code)
                );

                return Ok(new { message = "Ako email postoji, poslan je kod za reset" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ForgotPassword error");
                return StatusCode(500, new { message = "Greška pri slanju emaila" });
            }
        }

        // ─── POTVRDI RESET (provjeri kod + postavi novu lozinku) ────────────
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return BadRequest(new { message = "Kod i nova lozinka su obavezni" });

            if (dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Lozinka mora imati najmanje 6 znakova" });

            try
            {
                var (userId, isValid) = await _resetRepo.ValidateTokenAsync(dto.Code);

                if (!isValid)
                    return BadRequest(new { message = "Kod je neispravan ili je istekao" });

                // Ažuriraj lozinku
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
                using var conn = _dbConnection.CreateConnection();
                await conn.ExecuteAsync(
                    "UPDATE users SET password_hash = @Hash WHERE id = @Id",
                    new { Hash = hashedPassword, Id = userId });

                // Obriši iskorišteni token
                await _resetRepo.DeleteTokenAsync(dto.Code);

                return Ok(new { message = "Lozinka uspješno promijenjena" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResetPassword error");
                return StatusCode(500, new { message = "Greška pri resetiranju lozinke" });
            }
        }

        // ─── EMAIL PREDLOŠCI ────────────────────────────────────────────────
        private string BuildWelcomeEmail(string firstName) => $@"
<!DOCTYPE html>
<html>
<body style='font-family:Arial,sans-serif;background:#f5f5f5;padding:20px'>
  <div style='max-width:500px;margin:0 auto;background:#fff;border-radius:16px;padding:32px'>
    <div style='text-align:center;margin-bottom:24px'>
      <h1 style='color:#667eea;font-size:28px;margin:0'>🗺️ CroMap</h1>
    </div>
    <h2 style='color:#333'>Dobrodošli, {firstName}! 👋</h2>
    <p style='color:#666;line-height:1.6'>
      Vaša registracija je uspješna. Sada možete istraživati najbolja mjesta u
      Hrvatskoj, pratiti prijatelje i dijeliti svoje avanture.
    </p>
    <div style='background:#f0f0ff;border-radius:12px;padding:16px;margin:20px 0'>
      <p style='color:#667eea;margin:0;font-weight:bold'>🏖️ Istražite plaže</p>
      <p style='color:#667eea;margin:0;font-weight:bold'>🍽️ Pronađite restorane</p>
      <p style='color:#667eea;margin:0;font-weight:bold'>🏰 Otkrijte znamenitosti</p>
    </div>
    <p style='color:#999;font-size:12px;text-align:center;margin-top:24px'>
      © {DateTime.Now.Year} CroMap
    </p>
  </div>
</body>
</html>";

        private string BuildResetEmail(string firstName, string code) => $@"
<!DOCTYPE html>
<html>
<body style='font-family:Arial,sans-serif;background:#f5f5f5;padding:20px'>
  <div style='max-width:500px;margin:0 auto;background:#fff;border-radius:16px;padding:32px'>
    <div style='text-align:center;margin-bottom:24px'>
      <h1 style='color:#667eea;font-size:28px;margin:0'>🗺️ CroMap</h1>
    </div>
    <h2 style='color:#333'>Reset lozinke, {firstName}</h2>
    <p style='color:#666'>Primili smo zahtjev za reset lozinke. Vaš kod:</p>
    <div style='text-align:center;margin:24px 0'>
      <div style='display:inline-block;background:#667eea;color:#fff;
                  font-size:36px;font-weight:bold;letter-spacing:8px;
                  padding:16px 32px;border-radius:12px'>
        {code}
      </div>
    </div>
    <p style='color:#999;font-size:13px'>
      ⏱️ Kod ističe za <strong>1 sat</strong>.<br>
      Ako niste zatražili reset, zanemarite ovaj email.
    </p>
    <p style='color:#999;font-size:12px;text-align:center;margin-top:24px'>
      © {DateTime.Now.Year} CroMap
    </p>
  </div>
</body>
</html>";


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