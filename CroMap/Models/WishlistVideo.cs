namespace CroMap.Models
{
    public class WishlistVideo
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int VideoId { get; set; }
        public DateTime AddedAt { get; set; }
        public string Notes { get; set; }

        // Navigacijska polja (ne mapiraju se u bazu)
        public Video Video { get; set; }
        public string UserName { get; set; }
        public string VideoTitle { get; set; }
        public string VideoFilePath { get; set; }
    }
}