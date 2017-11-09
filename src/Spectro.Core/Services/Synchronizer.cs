using NewsBlurSharp;
using Spectro.Core.Interfaces;
using Spectro.DataModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using NewsBlurSharp.Model.Response;
using Story = Spectro.Core.DataModel.Story;

namespace Spectro.Core.Services
{
    public interface ISynchronizer
    {
        Task StartSync();
    }

    public class Synchronizer : ISynchronizer
    {
        private readonly INewsBlurClient _newsBlurClient;
        private readonly IProgressService _progressService;
        private readonly IAuthenticationService _authenticationService;
        private readonly IDataCacheService _dataCacheService;
        private bool _isSynchronizing;
        private readonly object _syncLock = new object();

        public Synchronizer(
            INewsBlurClient newsBlurClient,
            IProgressService progressService,
            IAuthenticationService authenticationService,
            IDataCacheService dataCacheService)
        {
            _newsBlurClient = newsBlurClient;
            _progressService = progressService;
            _authenticationService = authenticationService;
            _dataCacheService = dataCacheService;
        }

        public async Task StartSync()
        {
            if (!_authenticationService.IsLoggedIn)
            {
                return;
            }

            lock (_syncLock)
            {
                if (_isSynchronizing)
                {
                    return;
                }

                _isSynchronizing = true;
            }

            _progressService.ShowProgress();

            await Task.Run(async () =>
            {
                await Task.Delay(1000);

                //TODO: inject logging
                await DoSyncStages();

                lock (_syncLock)
                {
                    _isSynchronizing = false;
                }

                _progressService.HideProgress();
            });
        }

        private async Task DoSyncStages()
        {
            // TODO at some point we should parallalize the sync process.  This will be tricky 
            // due to realm threading requirements, we should build all the sync stages out get to functional
            // correctness before doing this optimization as we should optimze for number of network calls
            // as well as time to sync all feeds considering the whole problem
            // We need unit tests in place before attempting any optimizations so we can preserve functional correctness
            // and measure number of calls / total sync time

            // Stage 0 send pending updates to service
            // TODO

            // Stage 1 Get feeds newly added in service, update local feeds with latest story date
            await GetNewFeedsLatestStoryDate();

            // Stage 2 for any local feed that has been flagged as having new stories, fetch and store those stories
            // then update the cached date to indicate that the cache is up-to-date with service
            await GetNewStoriesForUpdatedFeeds();

            // Stage 3 Update the read status for local stories that have been read on the service
            // TODO
        }

        private async Task GetNewStoriesForUpdatedFeeds()
        {
            //This would be nice but not supported currenlty
            //var query = DataModelManager.RealmInstance.All<NewsFeed>().Where(ld => ld.DownloadedLastStoryDate != ld.LastStoryDateFromService);

            var query = await _dataCacheService.GetAllNewsFeeds();

            var threadId = Thread.CurrentThread.ManagedThreadId;

            AutoResetEvent ar = new AutoResetEvent(false);
            foreach (var localFeed in query)
            {
                //TODO: full vs only unread
                if (localFeed.DownloadedLastStoryDate == localFeed.LastStoryDateFromService)
                {
                    Debug.WriteLine($"Skipping {localFeed.Title}");
                }
                else
                {
                    Debug.WriteLine($"DateFromService:{localFeed.LastStoryDateFromService} DateFromDownloaded:{localFeed.DownloadedLastStoryDate} ");
                    StoriesResponse result = null;

                    ar.Reset();

                    // ProcessNewStories must be called on the same thread for each iteration of loop
                    // This autroreset approach is necessary since configureawait(true) deson't seem to work reliably
                    // Realm needs a consistent thread per instance
                    // TODO: extract as helper
                    var task = _newsBlurClient.GetStoriesAsync(localFeed.Id).ContinueWith(a =>
                    {
                        result = a.Result;
                        ar.Set();
                    });

                    ar.WaitOne();

                    if (result == null)
                    {
                        throw new Exception("Error: webcall failed");
                    }

                    //ProcessNewStories must be called on the same thread for each iteration of loop
                    Debug.Assert(threadId == Thread.CurrentThread.ManagedThreadId);

                    await ProcessNewStories(localFeed, result);
                }
            }
        }

        private async Task GetNewFeedsLatestStoryDate()
        {
            var results = await _newsBlurClient.GetFeedsAsync(false);

            await ProcessFeedUpdates(results);
        }

        private async Task ProcessNewStories(NewsFeed localFeed, StoriesResponse result)
        {
            var addedNewStory = false;
            _dataCacheService.BeginWrite();

            foreach (var story in result.Stories)
            {
                var storyId = story.Id;
                var storyExists = (await _dataCacheService.GetStories(fe => fe.Id == storyId)).FirstOrDefault();
                if (storyExists == null)
                {
                    string summary = "";
                    if (!string.IsNullOrEmpty(story.Content))
                    {
                        summary = Regex.Replace(story.Content, "<.*?>", string.Empty);
                        if (summary.Length > 150)
                        {
                            summary = summary.Substring(0, 150);
                        }
                    }

                    Story s = new Story
                    {
                        Id = storyId,
                        Title = story.Title,
                        FeedId = story.FeedId,
                        ReadStatus = story.ReadStatus,
                        //story.story_timestamp
                        Author = story.Authors,
                        TimeStamp = story.Timestamp,
                        ListImage = story.ImageUrls.Any() ? story.ImageUrls[0] : "",
                        Content = story.Content,
                        Summary = summary,
                        Feed = localFeed
                    };

                    _dataCacheService.AddStory(s);
                    addedNewStory = true;
                }
            }
            if (addedNewStory)
            {
                localFeed.UnreadCount = (await _dataCacheService.GetStories(st => st.ReadStatus == 0 && st.Feed == localFeed)).Count;
            }

            // no need to do a story pass until this changes
            localFeed.DownloadedLastStoryDate = localFeed.LastStoryDateFromService;
            _dataCacheService.Commit();
        }

        private async Task ProcessFeedUpdates(NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.Rootobject results)
        {
            _dataCacheService.BeginWrite();

            foreach (var item in results.feeds.FeedItems)
                //foreach (var item in results.feeds.FeedItems.Where(t => t.properties.feed_title == "AnandTech"))
            {
                //TODO: dependency inject the realmness
                var thisFeed = (await _dataCacheService.GetNewsFeeds(fe => fe.Id == item.id)).FirstOrDefault();
                if (thisFeed == null)
                {
                    try
                    {
                        Debug.WriteLine(item.properties.last_story_date);
                        thisFeed = new NewsFeed()
                        {
                            Id = item.id,
                            FeedUri = item.properties.feed_address,
                            Title = item.properties.feed_title,
                            IconUri = item.properties.favicon_url,
                            Active = item.properties.active,
                            LastStoryDateFromService = !string.IsNullOrEmpty(item.properties.last_story_date) ? DateTimeOffset.Parse(item.properties.last_story_date) : DateTimeOffset.MinValue
                        };

                        _dataCacheService.AddFeed(thisFeed);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
                else
                {
                    if (item.properties?.last_story_date != null)
                    {
                        thisFeed.LastStoryDateFromService = DateTimeOffset.Parse(item.properties.last_story_date);
                    }
                }

                //thisFeed.UnreadCount = DataModelManager.RealmInstance.All<Story>().Where(st => st.ReadStatus == 0 && st.FeedId == thisFeed.Id).Count();
            }

            _dataCacheService.Commit();
        }
    }
}
