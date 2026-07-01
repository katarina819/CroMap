using System.Text;
using CroMap.Data;
using CroMap.Repositories;
using CroMap.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

builder.Services.AddCors();

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

app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
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