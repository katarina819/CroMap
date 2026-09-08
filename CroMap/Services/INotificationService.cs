using CroMap.Models;

namespace CroMap.Services
{
    public interface INotificationService
    {
        /// <summary>
        /// Sprema obavijest za korisnika i, ako on to želi, šalje mu je i
        /// emailom. Vraća spremljenu obavijest, ili null ako je preskočena
        /// (korisnik ne prati tu kategoriju ili je isključio obavijesti).
        /// </summary>
        Task<Notification?> CreateAsync(Notification notification, bool? forceEmail = null);

        /// <summary>
        /// Šalje istu obavijest svima koji prate zadanu kategoriju.
        /// Vraća broj korisnika kojima je obavijest stvorena.
        /// </summary>
        Task<int> BroadcastToCategoryAsync(string category, string title, string? body,
            string? placeName, double? latitude, double? longitude, int? createdBy);
    }
}
