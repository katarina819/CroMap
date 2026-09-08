using System.Net;
using CroMap.Models;
using CroMap.Repositories;

namespace CroMap.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _repo;
        private readonly UserRepository _userRepo;
        private readonly IEmailService _emailService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            INotificationRepository repo,
            UserRepository userRepo,
            IEmailService emailService,
            ILogger<NotificationService> logger)
        {
            _repo = repo;
            _userRepo = userRepo;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<Notification?> CreateAsync(Notification notification, bool? forceEmail = null)
        {
            var prefs = await _repo.GetPreferencesAsync(notification.UserId);
            var isUserCreated = notification.Source == "user";

            // Obavijest koju je korisnik sam kreirao (podsjetnik) sprema se
            // uvijek — sam ju je tražio. Sistemske se spremaju samo ako je
            // uključio obavijesti u aplikaciji i prati tu kategoriju.
            if (!isUserCreated)
            {
                if (!prefs.AppEnabled)
                    return null;

                if (!string.IsNullOrWhiteSpace(notification.Category)
                    && notification.Category != "general"
                    && !prefs.Categories.Contains(notification.Category))
                    return null;
            }

            notification.CreatedAt = notification.CreatedAt == default ? DateTime.UtcNow : notification.CreatedAt;
            notification.Id = await _repo.CreateAsync(notification);

            var wantsEmail = forceEmail ?? prefs.EmailEnabled;
            if (wantsEmail)
                await TrySendEmailAsync(notification, prefs);

            return notification;
        }

        public async Task<int> BroadcastToCategoryAsync(string category, string title, string? body,
            string? placeName, double? latitude, double? longitude, int? createdBy)
        {
            var subscribers = await _repo.GetSubscribersForCategoryAsync(category);
            var created = 0;

            foreach (var prefs in subscribers)
            {
                var notification = new Notification
                {
                    UserId = prefs.UserId,
                    Category = category,
                    Title = title,
                    Body = body,
                    PlaceName = placeName,
                    Latitude = latitude,
                    Longitude = longitude,
                    Source = "system",
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow
                };

                // Jedan korisnik kojem email ne prođe ne smije srušiti slanje
                // ostalima — CreateAsync već lovi greške slanja, ali upis u
                // bazu za pojedinog korisnika može pasti i iz drugog razloga.
                try
                {
                    notification.Id = await _repo.CreateAsync(notification);
                    created++;

                    if (prefs.EmailEnabled)
                        await TrySendEmailAsync(notification, prefs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Neuspjelo stvaranje obavijesti za korisnika {UserId}", prefs.UserId);
                }
            }

            return created;
        }

        // ─── Email ───────────────────────────────────────────────────────────

        private async Task TrySendEmailAsync(Notification notification, NotificationPreferences prefs)
        {
            try
            {
                var address = prefs.Email;

                // Ako korisnik nije upisao poseban email za obavijesti,
                // koristi se onaj s kojim je registriran.
                if (string.IsNullOrWhiteSpace(address))
                {
                    var user = await _userRepo.GetUserByIdAsync(notification.UserId);
                    address = user?.Email;
                }

                if (string.IsNullOrWhiteSpace(address))
                {
                    _logger.LogWarning(
                        "Obavijest {Id}: email je uključen, ali korisnik {UserId} nema adresu",
                        notification.Id, notification.UserId);
                    return;
                }

                await _emailService.SendEmailAsync(address, notification.Title, BuildEmailBody(notification));
                await _repo.MarkEmailSentAsync(notification.Id);
            }
            catch (Exception ex)
            {
                // Obavijest je već spremljena i korisnik ju vidi u aplikaciji;
                // pad slanja emaila ne smije srušiti sam zahtjev.
                _logger.LogError(ex, "Slanje emaila za obavijest {Id} nije uspjelo", notification.Id);
            }
        }

        private static string BuildEmailBody(Notification notification)
        {
            // Sav korisnički tekst ide kroz HtmlEncode — naslov i tekst
            // obavijesti dolaze iz unosa, pa bi inače završili u HTML-u emaila
            // kao oznake.
            var title = WebUtility.HtmlEncode(notification.Title);
            var body = WebUtility.HtmlEncode(notification.Body ?? "");
            var place = WebUtility.HtmlEncode(notification.PlaceName ?? "");

            var placeRow = string.IsNullOrWhiteSpace(place)
                ? ""
                : $@"<p style=""margin:0 0 8px;color:#3a4a35;font-size:14px;"">📍 {place}</p>";

            var timeRow = notification.ScheduledAt.HasValue
                ? $@"<p style=""margin:0 0 8px;color:#3a4a35;font-size:14px;"">🕒 {notification.ScheduledAt.Value:dd.MM.yyyy. HH:mm}</p>"
                : "";

            return $@"
<div style=""font-family:Segoe UI,Roboto,Arial,sans-serif;background:#f0ede4;padding:24px;"">
  <div style=""max-width:520px;margin:0 auto;background:#ffffff;border-radius:16px;
              border:1px solid #c0d0a8;overflow:hidden;"">
    <div style=""background:#1a2e1a;padding:18px 24px;"">
      <span style=""color:#e8e8e8;font-size:18px;font-weight:700;"">VARA</span>
    </div>
    <div style=""padding:24px;"">
      <h1 style=""margin:0 0 12px;color:#1a2a18;font-size:20px;"">{title}</h1>
      {placeRow}
      {timeRow}
      <p style=""margin:12px 0 0;color:#3a4a35;font-size:15px;line-height:22px;"">{body}</p>
    </div>
    <div style=""padding:16px 24px;border-top:1px solid #e4ead8;"">
      <p style=""margin:0;color:#7a8a75;font-size:12px;"">
        Ovu poruku primaš jer si u aplikaciji uključio/la email obavijesti.
        Možeš ih isključiti u Postavkama obavijesti.
      </p>
    </div>
  </div>
</div>";
        }
    }
}
