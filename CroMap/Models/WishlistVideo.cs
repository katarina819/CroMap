namespace CroMap.Models
{
    public class WishlistVideo
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int VideoId { get; set; }
        public DateTime AddedAt { get; set; }
        public string? Notes { get; set; }
        public bool? IsGoing { get; set; }  // ← DODAJ OVO

        // Navigacijska polja (ne mapiraju se u bazu)
        public Video? Video { get; set; }
        public string? UserName { get; set; }
        public string? VideoTitle { get; set; }
        public string? VideoFilePath { get; set; }

        // Dodaj ova polja za jednostavnije korištenje
        public string Title
        {
            get => VideoTitle ?? Video?.Title ?? string.Empty;
            set => VideoTitle = value;
        }

        public string FilePath
        {
            get => VideoFilePath ?? Video?.FilePath ?? string.Empty;
            set => VideoFilePath = value;
        }
    }
}