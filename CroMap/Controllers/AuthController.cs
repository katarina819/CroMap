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

        // ─── VARA logo: hostana slika na vlastitom serveru ───────────────────
        // KORAK 1: Kopiraj vara_email_80.png i vara_email_36.png u wwwroot/images/
        // KORAK 2: Provjeri da appsettings.json ima "AppUrl": "http://10.82.106.206:7089"
        // KORAK 3: Inject IConfiguration i citaj AppUrl automatski (vidi konstruktor)
        private string _appUrl => _configuration["AppUrl"] ?? "http://10.82.106.206:7089";
        private string Logo80 => $"{_appUrl}/images/vara_email_80.png";
        private string Logo36 => $"{_appUrl}/images/vara_email_36.png";

        private static string LogoImg(string url, int px)
        {
            var radius = px / 6;
            return $"<img src='{url}' width='{px}' height='{px}' alt='VARA' style='display:block;border-radius:{radius}px;margin:0 auto' />";
        }

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
            if (string.IsNullOrWhiteSpace(userDto.Email))
                return BadRequest(new { message = "Email je obavezan" });
            if (!userDto.BirthDate.HasValue)
                return BadRequest(new { message = "Datum ro\u0111enja je obavezan" });

            var user = new User
            {
                Username = userDto.Username.ToLower(),
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                Email = userDto.Email,
                PasswordHash = userDto.Password,
                BirthDate = userDto.BirthDate.Value,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _repo.RegisterAsync(user);
                if (!string.IsNullOrWhiteSpace(userDto.Email))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(
                                userDto.Email,
                                "Dobrodo\u0161li u VARA! \U0001F5FA\uFE0F",
                                BuildWelcomeEmail(userDto.FirstName)
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Welcome email failed, ignoring");
                        }
                    });
                }
                return Ok(new { message = "Registracija uspje\u0161na" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error");
                if (ex.Message.Contains("23505") || ex.Message.Contains("duplicate key"))
                    return Conflict(new { message = "Korisni\u010dko ime, email ili telefon ve\u0107 postoji" });
                return BadRequest(new { message = "Gre\u0161ka pri registraciji" });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { message = "Email je obavezan" });
            try
            {
                using var conn = _dbConnection.CreateConnection();
                var user = await conn.QueryFirstOrDefaultAsync<User>(
                    "SELECT id, first_name AS FirstName, email FROM users WHERE LOWER(email) = LOWER(@Email)",
                    new { dto.Email });
                if (user == null)
                    return NotFound(new { message = "Nije prona\u0111en korisnik s tim emailom" });
                var code = new Random().Next(100000, 999999).ToString();
                await _resetRepo.CreateResetTokenAsync(user.Id, code);
                await _emailService.SendEmailAsync(
                    user.Email,
                    "VARA - Reset lozinke \U0001F510",
                    BuildResetEmail(user.FirstName, code)
                );
                return Ok(new { message = "Kod poslan na email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ForgotPassword error");
                return StatusCode(500, new { message = "Gre\u0161ka pri slanju emaila" });
            }
        }

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
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
                using var conn = _dbConnection.CreateConnection();
                await conn.ExecuteAsync(
                    "UPDATE users SET password_hash = @Hash WHERE id = @Id",
                    new { Hash = hashedPassword, Id = userId });
                await _resetRepo.DeleteTokenAsync(dto.Code);
                return Ok(new { message = "Lozinka uspje\u0161no promijenjena" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResetPassword error");
                return StatusCode(500, new { message = "Gre\u0161ka pri resetiranju lozinke" });
            }
        }

        private string BuildWelcomeEmail(string firstName) => $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family:Arial,sans-serif;background:#1B3F0E;padding:20px;margin:0'>
  <div style='max-width:520px;margin:0 auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 8px 32px rgba(0,0,0,0.35)'>
    <div style='background:linear-gradient(160deg,#2D6418 0%,#142F09 100%);padding:36px 32px 28px;text-align:center'>
      <table cellpadding='0' cellspacing='0' border='0' width='100%'>
        <tr><td align='center' style='padding-bottom:12px'>{LogoImg(Logo80, 80)}</td></tr>
        <tr><td align='center'><div style='color:#ffffff;font-size:34px;font-weight:900;letter-spacing:12px;padding-left:12px'>VARA</div></td></tr>
        <tr><td align='center'><div style='color:rgba(200,225,200,0.6);font-size:12px;letter-spacing:3px;text-transform:uppercase;padding-top:4px'>Otkrijte svako mjesto</div></td></tr>
      </table>
    </div>
    <div style='padding:32px'>
      <h2 style='color:#1a1a1a;font-size:22px;margin:0 0 12px;font-weight:800'>Dobrodo&#353;li, {firstName}! &#128075;</h2>
      <p style='color:#555;line-height:1.7;font-size:15px;margin:0 0 24px'>
        Va&#353;a registracija je uspje&#353;na. Sada mo&#382;ete istra&#382;ivati najljep&#353;a mjesta, pratiti prijatelje i dijeliti svoje avanture diljem Hrvatske i &#353;ire.
      </p>
      <div style='background:#f0f7ee;border-radius:14px;padding:20px;margin-bottom:24px'>
        <div style='margin-bottom:10px'><span style='color:#2D6418;font-weight:700;font-size:14px'>&#127958;&#65039; Istra&#382;ite pla&#382;e i nacionalne parkove</span></div>
        <div style='margin-bottom:10px'><span style='color:#2D6418;font-weight:700;font-size:14px'>&#127869;&#65039; Prona&#273;ite restorane i kafi&#263;e</span></div>
        <div style='margin-bottom:0'><span style='color:#2D6418;font-weight:700;font-size:14px'>&#127968; Otkrijte znamenitosti i skrivena mjesta</span></div>
      </div>
      <table cellpadding='0' cellspacing='0' border='0' width='100%' style='margin-bottom:8px'>
        <tr><td align='center'>
          <a href='vara://' style='display:inline-block;background:#2D6418;color:#ffffff;font-size:16px;font-weight:700;padding:16px 48px;border-radius:14px;text-decoration:none'>Otvori VARA</a>
        </td></tr>
      </table>
    </div>
    <div style='background:#f8f8f8;border-top:1px solid #e8e8e8;padding:16px 32px'>
      <table cellpadding='0' cellspacing='0' border='0' width='100%'>
        <tr><td align='center' style='padding-bottom:8px'>{LogoImg(Logo36, 36)}</td></tr>
        <tr><td align='center'><span style='color:#aaa;font-size:12px'>&#169; {DateTime.Now.Year} VARA. Sva prava pridr&#382;ana.</span></td></tr>
      </table>
    </div>
  </div>
</body>
</html>";

        private string BuildResetEmail(string firstName, string code) => $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family:Arial,sans-serif;background:#1B3F0E;padding:20px;margin:0'>
  <div style='max-width:520px;margin:0 auto;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 8px 32px rgba(0,0,0,0.35)'>
    <div style='background:linear-gradient(160deg,#2D6418 0%,#142F09 100%);padding:36px 32px 28px;text-align:center'>
      <table cellpadding='0' cellspacing='0' border='0' width='100%'>
        <tr><td align='center' style='padding-bottom:12px'>{LogoImg(Logo80, 80)}</td></tr>
        <tr><td align='center'><div style='color:#ffffff;font-size:34px;font-weight:900;letter-spacing:12px;padding-left:12px'>VARA</div></td></tr>
        <tr><td align='center'><div style='color:rgba(200,225,200,0.6);font-size:12px;letter-spacing:3px;text-transform:uppercase;padding-top:4px'>Sigurnosni kod</div></td></tr>
      </table>
    </div>
    <div style='padding:32px'>
      <h2 style='color:#1a1a1a;font-size:20px;margin:0 0 8px;font-weight:800'>Reset lozinke za korisnika {firstName}</h2>
      <p style='color:#555;line-height:1.7;font-size:15px;margin:0 0 28px'>
        Primili smo zahtjev za promjenu lozinke va&#353;eg VARA ra&#269;una. Upotrijebite kod ispod u aplikaciji:
      </p>
      <table cellpadding='0' cellspacing='0' border='0' width='100%' style='margin-bottom:28px'>
        <tr>
          <td align='center' valign='middle'>
            <table cellpadding='0' cellspacing='0' border='0' style='margin:0 auto'>
              <tr>
                <td align='center' valign='middle' style='background:linear-gradient(135deg,#2D6418,#142F09);border-radius:16px;padding:20px 32px'>
                  <table cellpadding='0' cellspacing='0' border='0' style='margin:0 auto'>
                    <tr>
                      {string.Join("", code.Select(c => $"<td width='38' align='center' valign='middle' style='width:38px;min-width:38px;color:#ffffff;font-size:38px;font-weight:900;line-height:1;font-family:Courier New,Courier,monospace;text-align:center;padding:8px 4px'>{c}</td>"))}
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
          </td>
        </tr>
      </table>
      <div style='background:#fff8e1;border:1px solid #ffe082;border-radius:12px;padding:16px'>
        <p style='color:#795548;font-size:13px;margin:0;line-height:1.6'>
          &#9203;&#65039; Kod je valjan <strong>1 sat</strong> od slanja ovog emaila.<br>
          &#128274; Ako niste zatra&#382;ili promjenu lozinke, zanemarite ovaj email &#8212; va&#353; ra&#269;un je siguran.
        </p>
      </div>
    </div>
    <div style='background:#f8f8f8;border-top:1px solid #e8e8e8;padding:16px 32px'>
      <table cellpadding='0' cellspacing='0' border='0' width='100%'>
        <tr><td align='center' style='padding-bottom:8px'>{LogoImg(Logo36, 36)}</td></tr>
        <tr><td align='center'><span style='color:#aaa;font-size:12px'>&#169; {DateTime.Now.Year} VARA. Sva prava pridr&#382;ana.</span></td></tr>
      </table>
    </div>
  </div>
</body>
</html>";

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                _logger.LogInformation($"Login attempt: {dto.Username}");
                var user = await _repo.LoginByUsernameAsync(dto.Username, dto.Password);
                if (user == null)
                {
                    _logger.LogWarning($"Login failed for: {dto.Username}");
                    return Unauthorized(new { message = "Neispravno korisni\u010dko ime ili lozinka" });
                }
                _logger.LogInformation($"Login successful: {user.Username} (ID: {user.Id})");
                var token = GenerateJwtToken(user);
                return Ok(new
                {
                    token = token,
                    userId = user.Id,
                    username = user.Username,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    phone = user.Phone
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                return StatusCode(500, new { message = "Gre\u0161ka na serveru" });
            }
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
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
                return StatusCode(500, new { message = "Gre\u0161ka pri dohva\u0107anju korisnika" });
            }
        }

        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var user = await _repo.GetUserByIdAsync(id);
                if (user == null)
                    return NotFound(new { message = "Korisnik nije prona\u0111en" });
                var avatar = await _repo.GetUserAvatarAsync(id);
                return Ok(new { user.Id, user.FirstName, user.LastName, user.Username, Avatar = avatar });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user {id}");
                return StatusCode(500, new { message = "Gre\u0161ka pri dohva\u0107anju korisnika" });
            }
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserDto userDto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userDto.FirstName))
                    return BadRequest(new { message = "Ime je obavezno" });
                if (string.IsNullOrWhiteSpace(userDto.LastName))
                    return BadRequest(new { message = "Prezime je obavezno" });
                if (!userDto.BirthDate.HasValue)
                    return BadRequest(new { message = "Datum ro\u0111enja je obavezan" });
                var user = new User
                {
                    Id = id,
                    Username = userDto.Username,
                    FirstName = userDto.FirstName,
                    LastName = userDto.LastName,
                    Email = userDto.Email,
                    BirthDate = userDto.BirthDate.Value,
                    CreatedAt = DateTime.UtcNow
                };
                await _repo.UpdateUserAsync(user);
                return Ok(new { message = "Korisnik a\u017euriran" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user {id}");
                return StatusCode(500, new { message = "Gre\u0161ka pri a\u017euriranju korisnika" });
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
                return StatusCode(500, new { message = "Gre\u0161ka pri brisanju korisnika" });
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