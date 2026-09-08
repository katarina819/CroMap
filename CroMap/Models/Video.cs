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

        // Avatar i ime autora dolaze zajedno s videom. Prije ovoga klijent je
        // za SVAKI video radio zaseban poziv na /api/auth/users/{id} samo da
        // dozna avatar, pa je pri sporom odgovoru (ili 429 s rate limitera)
        // ostajao na inicijalima — dok je npr. pretraga, koja avatar dobiva
        // u istom popisu, prikazivala pravu sliku.
        public string? UserAvatar { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsLiked { get; set; }
        public bool IsSaved { get; set; }
        public bool IsOwner { get; set; }

        [NotMapped]
        public bool IsInWishlist { get; set; }
    }
}