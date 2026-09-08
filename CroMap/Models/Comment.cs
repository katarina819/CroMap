namespace CroMap.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int VideoId { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        // Za prikaz komentara s korisničkim imenom
        public string UserName { get; set; }

        // Avatar i ime dolaze uz komentar da klijent ne mora raditi zaseban
        // poziv po komentaru (isti razlog kao kod Video.UserAvatar).
        public string? UserAvatar { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}