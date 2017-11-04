using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewsBlurSharp.Model.Response;

namespace NewsBlurSharp
{
    public interface INewsBlurClient
    {
        void SetCookieSessionId(string cookieSessionId);
        Task<LoginResponse> LoginAsync(string username, string password, CancellationToken cancellation = default(CancellationToken));
        Task LogoutAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task<SignupResponse> SignupAsync(string username, string emailAddress, string password = "", CancellationToken cancellationToken = default(CancellationToken));
        Task<NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.Rootobject> GetFeedsAsync(bool? includeFavIcons = null, bool? isFlatStructure = null, bool? updateCounts = null, CancellationToken cancellationToken = default(CancellationToken));
        Task<NewsBlurSharp.Model.GetStoriesResponse.Rootobject> GetStoriesAsync(int feedId, int? pageIndex = null, bool invertOrder = false, bool filterReadStories = false, bool includeHiddenStories = false, CancellationToken cancellationToken = default(CancellationToken));
        Task<object> MarkStoriesReadAsync(List<String> storyHashList);
        Task<object> MarkStoryUnreadAsync(String storyHash);
        Task<object> GetUserPublicProfileAsync(int userID, CancellationToken cancellationToken = default(CancellationToken));
        Task<ProfileResponse> GetUserProfileAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}