using CroMap.Models;

namespace CroMap.Repositories
{
    public interface INotificationRepository
    {
        Task<IEnumerable<NotificationDto>> GetForUserAsync(int userId, int limit, int offset);
        Task<int> GetUnreadCountAsync(int userId);
        Task<bool> MarkReadAsync(int notificationId, int userId);
        Task<int> MarkAllReadAsync(int userId);
        Task<bool> DeleteAsync(int notificationId, int userId);

        Task<NotificationPreferencesDto> GetPreferencesAsync(int userId);
        Task SavePreferencesAsync(int userId, NotificationPreferencesDto prefs);

        /// <summary>
        /// Napravi obavijest svim pratiteljima koji su uključili obavijesti u
        /// aplikaciji i prate barem jednu od kategorija ove objave.
        /// </summary>
        Task<int> FanOutNewActivityAsync(
            int actorUserId,
            int videoId,
            string title,
            string body,
            IEnumerable<string> categories);

        /// <summary>Pratitelji koji istu obavijest žele i e-poštom.</summary>
        Task<IEnumerable<NotificationEmailRecipient>> GetEmailRecipientsAsync(
            int actorUserId,
            IEnumerable<string> categories);
    }
}
