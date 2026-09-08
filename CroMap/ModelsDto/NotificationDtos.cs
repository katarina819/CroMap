namespace CroMap.ModelsDto
{
    /// <summary>Tijelo zahtjeva kad korisnik sam kreira obavijest/podsjetnik.</summary>
    public class CreateNotificationRequest
    {
        public string Title { get; set; } = "";
        public string? Body { get; set; }
        public string Category { get; set; } = "general";
        public string? PlaceName { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime? ScheduledAt { get; set; }

        /// <summary>
        /// Pošalji i email kopiju. Ako je null, odlučuje korisnikova postavka
        /// "Email obavijesti".
        /// </summary>
        public bool? SendEmail { get; set; }
    }

    /// <summary>
    /// Obavijest koju administrator šalje svima koji prate zadanu kategoriju.
    /// </summary>
    public class BroadcastNotificationRequest
    {
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Body { get; set; }
        public string? PlaceName { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    /// <summary>Postavke obavijesti kakve ih klijent šalje i prima.</summary>
    public class NotificationPreferencesDto
    {
        public bool AppEnabled { get; set; }
        public bool EmailEnabled { get; set; }
        public string? Email { get; set; }
        public List<string> Categories { get; set; } = new();
        public List<string> AgeGroups { get; set; } = new();
    }
}
