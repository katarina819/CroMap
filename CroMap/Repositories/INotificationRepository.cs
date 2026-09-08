using CroMap.Models;

namespace CroMap.Repositories
{
    public interface INotificationRepository
    {
        Task<IEnumerable<Notification>> GetForUserAsync(int userId, int page, int pageSize, bool unreadOnly);
        Task<int> GetUnreadCountAsync(int userId);
        Task<Notification?> GetByIdAsync(int id, int userId);
        Task<int> CreateAsync(Notification notification);
        Task<bool> MarkReadAsync(int id, int userId, bool isRead);
        Task<int> MarkAllReadAsync(int userId);
        Task<bool> DeleteAsync(int id, int userId);
        Task<int> DeleteAllAsync(int userId);
        Task<bool> MarkEmailSentAsync(int id);

        Task<NotificationPreferences> GetPreferencesAsync(int userId);
        Task SavePreferencesAsync(NotificationPreferences prefs);

        /// <summary>
        /// Svi korisnici koji su uključili obavijesti u aplikaciji i prate
        /// zadanu kategoriju — meta za obavijest vezanu uz tu kategoriju.
        /// </summary>
        Task<IEnumerable<NotificationPreferences>> GetSubscribersForCategoryAsync(string category);
    }
}
