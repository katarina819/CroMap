using System.ComponentModel.DataAnnotations.Schema;

namespace CroMap.Models
{
    public class Video
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; }
        public string Location { get; set; }
        public string AdditionalDescription { get; set; }
        public string FilePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public string MediaType { get; set; } = "video";

        // Dodatna polja za frontend
        public string UserName { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsLiked { get; set; }
        public bool IsSaved { get; set; }
        public bool IsOwner { get; set; }

        [NotMapped]
        public bool IsInWishlist { get; set; }
    }
}