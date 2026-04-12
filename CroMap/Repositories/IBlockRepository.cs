using CroMap.Models;

namespace CroMap.Repositories
{
    public interface IBlockRepository
    {
        Task<bool> BlockUserAsync(int userId, int blockedUserId);

        Task<bool> UnblockUserAsync(int userId, int blockedUserId);

        Task<IEnumerable<BlockedUser>> GetBlockedUsersAsync(int userId);

        Task<bool> IsBlockedAsync(int userId, int blockedUserId);
    }
}