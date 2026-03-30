using CroMap.Models;

namespace CroMap.Repositories
{
    public interface IWishlistRepository
    {
        Task<WishlistVideo> AddToWishlistAsync(WishlistVideo wishlistItem);
        Task<bool> RemoveFromWishlistAsync(int userId, int videoId);
        Task<IEnumerable<WishlistVideo>> GetUserWishlistAsync(int userId);
        Task<bool> IsInWishlistAsync(int userId, int videoId);
        Task<bool> UpdateWishlistNotesAsync(int userId, int videoId, string notes);
    }
}