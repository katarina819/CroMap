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

        // Follow requests (privatni profili)
        Task<bool> IsUserPublicAsync(int userId);
        Task<bool> RequestFollowAsync(int requesterId, int targetId);
        Task<bool> CancelFollowRequestAsync(int requesterId, int targetId);
        Task<bool> HasPendingRequestAsync(int requesterId, int targetId);
        Task<bool> AcceptFollowRequestAsync(int requesterId, int targetId);
        Task<bool> DeclineFollowRequestAsync(int requesterId, int targetId);
        Task<IEnumerable<UserSearchDto>> GetPendingFollowRequestsAsync(int targetId);
        Task<IEnumerable<int>> GetOutgoingRequestTargetIdsAsync(int requesterId);
    }
}