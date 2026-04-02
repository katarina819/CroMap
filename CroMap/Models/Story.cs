// Models/Story.cs (novi model)
using System;

namespace CroMap.Models
{
    public class Story
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? UserAvatar { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool ViewedByMe { get; set; }
        public int ViewCount { get; set; }
    }

    public class StoryViewer
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
    }
}