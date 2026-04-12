// Models/Story.cs - prošireni modeli
using System;
using System.Collections.Generic;

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
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool LikedByMe { get; set; }
        public List<StoryViewer>? Viewers { get; set; }
        public List<StoryLike>? Likes { get; set; }
        public List<StoryComment>? Comments { get; set; }
    }

    public class StoryViewer
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? UserAvatar { get; set; }
        public DateTime ViewedAt { get; set; }
    }

    public class StoryLike
    {
        public int Id { get; set; }
        public int StoryId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? UserAvatar { get; set; }
        public string ReactionType { get; set; } = "like"; // like, heart, smile, etc.
        public DateTime CreatedAt { get; set; }
    }

    public class StoryComment
    {
        public int Id { get; set; }
        public int StoryId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? UserAvatar { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<StoryCommentReaction>? Reactions { get; set; }
    }

    public class StoryCommentReaction
    {
        public int Id { get; set; }
        public int CommentId { get; set; }
        public int UserId { get; set; }
        public string ReactionType { get; set; } = string.Empty; // smile, heart, etc.
        public DateTime CreatedAt { get; set; }
    }
}