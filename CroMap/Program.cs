using System.Text;
using System.Threading.RateLimiting;
using CroMap.Data;
using CroMap.Repositories;
using CroMap.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["R2:AccessKeyId"] = Environment.GetEnvironmentVariable("R2_ACCESS_KEY_ID");
builder.Configuration["R2:SecretAccessKey"] = Environment.GetEnvironmentVariable("R2_SECRET_ACCESS_KEY");
builder.Configuration["R2:Endpoint"] = Environment.GetEnvironmentVariable("R2_ENDPOINT");
builder.Configuration["R2:BucketName"] = Environment.GetEnvironmentVariable("R2_BUCKET_NAME");
builder.Configuration["R2:PublicUrl"] = Environment.GetEnvironmentVariable("R2_PUBLIC_URL");

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Services
builder.Services.AddSingleton<DatabaseConnection>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<IVideoRepository, VideoRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IStoryRepository, StoryRepository>();
builder.Services.AddScoped<IFollowRepository, FollowRepository>();
builder.Services.AddScoped<ISavedVideoRepository, SavedVideoRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
builder.Services.AddScoped<IGoldenFriendRepository, GoldenFriendRepository>();
builder.Services.AddScoped<IBlockRepository, BlockRepository>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<MediaRepository>();
builder.Services.AddScoped<AdminRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailServiceWithInlineImages, EmailService>();
builder.Services.AddScoped<PasswordResetRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();  // ← Bez security definicije

// CORS — u produkciji ograničeno na stvarne domene aplikacije (postavi
// AllowedOrigins u konfiguraciji/env varijabli, npr. "https://vara.app,
// https://www.vara.app"). Mobilna aplikacija (Bearer token, ne kolačići)
// CORS uopće ne provjerava, ovo štiti isključivo web/browser klijente.
var allowedOrigins = (builder.Configuration["AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment() || allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
        }
    });
});

// Rate limiting — glavna obrana od automatiziranih botova koji bi inače
// mogli neograničeno brzo pokušavati registraciju/login (brute force,
// masovno kreiranje lažnih računa). Prije ovoga nije postojalo NIKAKVO
// ograničenje broja zahtjeva.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Opći limit za sve rute, po IP adresi. Namjerno velikodušan (300/min)
    // jer isti pipeline poslužuje i statične datoteke (avatari, thumbnaili,
    // videi) — jedan ekran zna napraviti desetke takvih zahtjeva odjednom;
    // "auth" politika ispod je stroža jer je to stvarna meta botova.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Stroži limit specifično za registraciju i prijavu — najčešća meta
    // botova (masovna registracija, brute-force lozinki)
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

builder.Services.AddHttpClient();

builder.Services.AddScoped<CroMap.Services.IR2StorageService, CroMap.Services.R2StorageService>();



var app = builder.Build();


var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var avatarsPath = Path.Combine(wwwrootPath, "avatars");
var videosPath = Path.Combine(wwwrootPath, "videos");
var storiesPath = Path.Combine(wwwrootPath, "stories");


if (!Directory.Exists(wwwrootPath))
    Directory.CreateDirectory(wwwrootPath);


if (!Directory.Exists(avatarsPath))
    Directory.CreateDirectory(avatarsPath);

if (!Directory.Exists(videosPath))
    Directory.CreateDirectory(videosPath);

if (!Directory.Exists(storiesPath))
    Directory.CreateDirectory(storiesPath);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Render (i slične platforme) stavljaju aplikaciju iza reverse proxyja —
// bez ovoga bi RemoteIpAddress uvijek bio proxyjev IP, pa bi rate limiter
// gore partitionirao SVE korisnike zajedno kao jednog "klijenta" umjesto
// svakog posebno po njegovoj stvarnoj IP adresi.
var forwardedHeaderOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeaderOptions.KnownNetworks.Clear();
forwardedHeaderOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaderOptions);

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();


app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars")),
    RequestPath = "/avatars"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "videos")),
    RequestPath = "/videos"
});


app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "stories")),
    RequestPath = "/stories"
});

app.MapControllers();

// Seed admin korisnika
using (var scope = app.Services.CreateScope())
{
    var adminRepo = scope.ServiceProvider.GetRequiredService<AdminRepository>();
    await adminRepo.SeedAdminUser();
}


app.Run("http://0.0.0.0:7089");