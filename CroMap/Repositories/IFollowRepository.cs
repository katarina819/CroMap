namespace CroMap.Repositories
{
    public interface IFollowRepository
    {
        Task<bool> FollowAsync(int followerId, int followedId);
        Task<bool> UnfollowAsync(int followerId, int followedId);
        Task<IEnumerable<UserSearchDto>> GetFollowingAsync(int userId);
        Task<IEnumerable<UserSearchDto>> GetFollowersAsync(int userId);
        Task<int> GetFollowersCountAsync(int userId);
        Task<int> GetFollowingCountAsync(int userId);
        Task<bool> IsFollowingAsync(int followerId, int followedId);
    }
}