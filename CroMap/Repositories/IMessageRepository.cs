using CroMap.Models;

namespace CroMap.Repositories
{
    public interface IMessageRepository
    {
        Task<Message> SendMessageAsync(Message message);
        Task<IEnumerable<Message>> GetConversationAsync(int userId1, int userId2);
        Task<IEnumerable<Message>> GetUserMessagesAsync(int userId);
        Task<IEnumerable<Message>> GetUnreadMessagesAsync(int userId);
        Task<bool> MarkAsReadAsync(int messageId);
        Task<int> GetUnreadCountAsync(int userId);
        Task<bool> DeleteMessageAsync(int messageId, int userId);
    }
}