namespace CroMap.Models
{
    /// <summary>Jedna obavijest kakvu aplikacija prikazuje u popisu.</summary>
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        /// <summary>Stabilna oznaka kategorije ("club", "museum"...), ne prevedeni naziv.</summary>
        public string? Category { get; set; }
        public int? VideoId { get; set; }
        public int? ActorUserId { get; set; }
        public string ActorName { get; set; } = "";
        public string ActorAvatar { get; set; } = "";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Postavke obavijesti. Dosad su postojale samo na uređaju (AsyncStorage),
    /// pa poslužitelj nije mogao znati koga koja kategorija zanima.
    /// </summary>
    public class NotificationPreferencesDto
    {
        public bool AppEnabled { get; set; } = true;
        public bool EmailEnabled { get; set; }
        /// <summary>Ako je prazno, koristi se adresa s korisničkog računa.</summary>
        public string? Email { get; set; }
        /// <summary>Stabilne oznake kategorija koje korisnik prati.</summary>
        public List<string> Categories { get; set; } = new();

        /// <summary>
        /// Prima li korisnik i sadržaj izvan svojih krajeva. Zadano uključeno
        /// da aplikacija na početku ne izgleda prazno.
        /// </summary>
        public bool GlobalEnabled { get; set; } = true;

        /// <summary>
        /// Dokle za ovog korisnika seže "blizu mene", u kilometrima. Vrijedi i
        /// za feed i za obavijesti — korisnik jednom kaže što mu je blizu, a ne
        /// posebno za svaki dio aplikacije. Poslužitelj vrijednost ograničava
        /// na 1–100.
        /// </summary>
        public int RadiusKm { get; set; } = 50;
    }

    /// <summary>Primatelj obavijesti e-poštom, s podacima za jezik poruke.</summary>
    public class NotificationEmailRecipient
    {
        public int UserId { get; set; }
        public string ToEmail { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string Language { get; set; } = "hr";
    }
}
