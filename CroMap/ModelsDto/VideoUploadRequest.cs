namespace CroMap.ModelsDto
{
    public class VideoUploadRequest
    {
        public IFormFile Video { get; set; }

        /// <summary>
        /// Sličica videa koju je telefon napravio iz prvog kadra. Nije
        /// obavezna: stariji klijenti je ne šalju, a slike je ne trebaju.
        /// </summary>
        public IFormFile? Thumbnail { get; set; }
        public string Title { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public int UserId { get; set; }
        public string MediaType { get; set; } = "video";
        /// <summary>
        /// Stabilne oznake kategorija odvojene zarezom ("club,cafe"). Opis i
        /// dalje nosi prevedene nazive za prikaz, ali njih poslužitelj ne može
        /// usporediti s ničim — obavijestima trebaju oznake.
        /// </summary>
        public string Categories { get; set; } = "";
        /// <summary>Stabilne oznake dobnih skupina ("youth,students").</summary>
        public string AgeGroups { get; set; } = "";

        /// <summary>Označio li autor objavu kao najavu događaja.</summary>
        public bool IsEvent { get; set; }

        /// <summary>Početak događaja (ISO 8601), ako je objava događaj.</summary>
        public DateTime? EventStartAt { get; set; }

        /// <summary>
        /// Koordinate odabrane lokacije. Neobavezne: stariji klijent ih ne
        /// šalje, a objava bez njih se i dalje prihvaća — samo ne može ući u
        /// prikaz "blizu mene".
        /// </summary>
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}