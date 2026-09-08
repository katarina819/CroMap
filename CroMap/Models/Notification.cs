namespace CroMap.Models
{
    /// <summary>
    /// Jedna obavijest za jednog korisnika. Nastaje ili tako da ju korisnik
    /// sam kreira (podsjetnik, <see cref="Source"/> = "user") ili iz aktivnosti
    /// u aplikaciji za kategoriju koju korisnik prati (<see cref="Source"/> =
    /// "system").
    /// </summary>
    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        /// <summary>Kategorija mjesta ("cafe", "beach", ...) ili "general".</summary>
        public string Category { get; set; } = "general";

        public string Title { get; set; } = "";
        public string? Body { get; set; }

        public string? PlaceName { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        /// <summary>Kad korisnik želi biti podsjećen (samo za vlastite podsjetnike).</summary>
        public DateTime? ScheduledAt { get; set; }

        public bool IsRead { get; set; }

        /// <summary>"user" ili "system".</summary>
        public string Source { get; set; } = "system";

        public int? CreatedBy { get; set; }
        public bool EmailSent { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Korisnikov izbor: prima li obavijesti u aplikaciji, prima li ih emailom,
    /// na koji email i za koje kategorije.
    /// </summary>
    public class NotificationPreferences
    {
        public int UserId { get; set; }
        public bool AppEnabled { get; set; }
        public bool EmailEnabled { get; set; }
        public string? Email { get; set; }

        /// <summary>Kategorije koje korisnik prati.</summary>
        public List<string> Categories { get; set; } = new();

        /// <summary>Odabrane dobne skupine (koriste se za predlaganje kategorija).</summary>
        public List<string> AgeGroups { get; set; } = new();

        public DateTime UpdatedAt { get; set; }
    }
}
