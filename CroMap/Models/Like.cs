namespace CroMap.Models
{
    public class Like
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int VideoId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}