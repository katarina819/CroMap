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
    }
}