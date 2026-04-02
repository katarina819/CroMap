// Models/Profile.cs
using System;

namespace CroMap.Models
{
    public class Profile
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? Avatar { get; set; }
        public bool IsPublic { get; set; } = true;
        public int? ScreenTimeLimitMinutes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Podaci iz users tablice (join)
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }

        // Izračunati podaci
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
    }

    public class ProfileDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IsPublic { get; set; }
        public int? ScreenTimeLimitMinutes { get; set; }
    }

    public class SettingsDto
    {
        public bool IsPublic { get; set; }
        public int ScreenTimeLimitMinutes { get; set; }
    }
}