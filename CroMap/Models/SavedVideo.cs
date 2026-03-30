namespace CroMap.Models
{
    public class SavedVideo
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int VideoId { get; set; }
        public DateTime SavedAt { get; set; }
    }
}