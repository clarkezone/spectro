using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewsBlurSharp.Model.Response;

namespace NewsBlurSharp
{
    public interface INewsBlurClient
    {
        void SetCookieSessionId(string cookieSessionId);
        Task<LoginResponse> LoginAsync(string username, string password, CancellationToken cancellation = default);
        Task LogoutAsync(CancellationToken cancellationToken = default);
        Task<NewsFeedResponse> GetFeedsAsync(bool? includeFavIcons = null, bool? isFlatStructure = null, bool? updateCounts = null, CancellationToken cancellationToken = default);
        Task<StoriesResponse> GetStoriesAsync(int feedId, int? pageIndex = null, bool invertOrder = false, bool filterReadStories = false, bool includeHiddenStories = false, CancellationToken cancellationToken = default);
        Task<StoriesResponse> GetRiverStoriesAsync(int? pageIndex = null, bool invertOrder = false, bool filterReadStories = false, CancellationToken cancellationToken = default);
        Task<StoriesResponse> GetStarredStoriesAsync(int? pageIndex = null, CancellationToken cancellationToken = default);
        Task<UnreadStoryHashesResponse> GetUnreadStoryHashesAsync(CancellationToken cancellationToken = default);
        Task<OperationResponse> MarkStoriesReadAsync(List<string> storyHashList);
        Task<OperationResponse> MarkStoriesReadAsync(List<string> storyHashList, CancellationToken cancellationToken);
        Task<OperationResponse> MarkStoryUnreadAsync(string storyHash);
        Task<OperationResponse> MarkStoryUnreadAsync(string storyHash, CancellationToken cancellationToken);
        Task<OperationResponse> StarStoryAsync(string storyHash, CancellationToken cancellationToken = default);
        Task<OperationResponse> UnstarStoryAsync(string storyHash, CancellationToken cancellationToken = default);
        Task<OperationResponse> MarkFeedReadAsync(int feedId, CancellationToken cancellationToken = default);
        Task<ProfileResponse> GetUserProfileAsync(CancellationToken cancellationToken = default);
    }
}