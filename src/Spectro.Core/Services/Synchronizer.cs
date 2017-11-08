using NewsBlurSharp;
using Spectro.Core.Interfaces;
using Spectro.DataModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;

namespace Spectro.Core.Services
{
    public interface ISynchronizer
    {
        Task StartSync();

        void RegisterCredentialPrompt(ICredentialsPrompt prompt);
    }

    public class Synchronizer : ISynchronizer
    {
        private readonly INewsBlurClient _newsBlurClient;
        private bool _isSynchronizing;
        private readonly object _syncLock = new object();
        private ICredentialsPrompt _prompt;

        public Synchronizer(INewsBlurClient newsBlurClient)
        {
            _newsBlurClient = newsBlurClient;
        }

        public async Task StartSync()
        {
         
            //TODO: bail if not logged in
            //TODO: show progress dots
            lock (_syncLock)
            {
                if (_isSynchronizing)
                {
                    return;
                }

                _isSynchronizing = true;
            }
            if (_prompt != null && _prompt.HaveNetwork())
            {
                _prompt?.ShowProgress();
                await Task.Run(async () =>
                {
                    await Task.Delay(1000);

                    //TODO: inject logging
                    await DoSyncStages();

                    lock (_syncLock)
                    {
                        _isSynchronizing = false;
                    }
                    _prompt?.HideProgress();
                });
            }
        }

        private async Task DoSyncStages()
        {
            // TODO at some point we should parallalize the sync process.  This will be tricky 
            // due to realm threading requirements, we should build all the sync stages out get to functional
            // correctness before doing this optimization as we should optimze for number of network calls
            // as well as time to sync all feeds considering the whole problem

            // Stage 0 send pending updates to service
            // TODO

            // Stage 1 Get feeds newly added in service, update local feeds with latest story date
            await GetNewFeedsLatestStoryDate();

            // Stage 2 for any local feed that has been flagged as having new stories, fetch and store those stories
            // then update the cached date to indicate that the cache is up-to-date with service
            GetNewStoriesForUpdatedFeeds();

            // Stage 3 Update the read status for local stories that have been read on the service
            // TODO
        }

        private void GetNewStoriesForUpdatedFeeds()
        {
            //This would be nice but not supported currenlty
            //var query = DataModelManager.RealmInstance.All<NewsFeed>().Where(ld => ld.DownloadedLastStoryDate != ld.LastStoryDateFromService);

            var query = DataModelManager.RealmInstance.All<NewsFeed>();

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
                    NewsBlurSharp.Model.GetStoriesResponse.Rootobject result = null;

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

                    ProcessNewStories(localFeed, result);
                }
            }
        }

        private async Task GetNewFeedsLatestStoryDate()
        {
            var results = await _newsBlurClient.GetFeedsAsync(false);

            ProcessFeedUpdates(results);
        }

        private static void ProcessNewStories(NewsFeed localFeed, NewsBlurSharp.Model.GetStoriesResponse.Rootobject result)
        {
            var addedNewStory = false;
            using (var trans = DataModelManager.RealmInstance.BeginWrite())
            {
                foreach (var story in result.stories)
                {
                    var storyId = story.id;
                    var storyExists = DataModelManager.RealmInstance.All<Story>().Where(fe => fe.Id == storyId).FirstOrDefault();
                    if (storyExists == null)
                    {
                        string summary = "";
                        if (!string.IsNullOrEmpty(story.story_content))
                        {
                            summary = Regex.Replace(story.story_content, "<.*?>", string.Empty);
                            if (summary.Length > 150)
                            {
                                summary = summary.Substring(0, 150);
                            }
                        }

                        Story s = new Story()
                        {
                            Id = storyId,
                            Title = story.story_title,
                            FeedId = story.story_feed_id,
                            ReadStatus = story.read_status,
                            //story.story_timestamp
                            Author = story.story_authors,
                            TimeStamp = story.story_timestamp,
                            ListImage = (story.image_urls.Count() >= 1 ? story.image_urls[0] : ""),
                            Content = story.story_content,
                            Summary = summary,
                            Feed = localFeed
                        };
                        DataModelManager.RealmInstance.Add(s);
                        addedNewStory = true;
                    }
                }
                if (addedNewStory)
                {
                    localFeed.UnreadCount = DataModelManager.RealmInstance.All<Story>().Where(st => st.ReadStatus == 0 && st.Feed == localFeed).Count();
                }

                // no need to do a story pass until this changes
                localFeed.DownloadedLastStoryDate = localFeed.LastStoryDateFromService;
                trans.Commit();
            }
        }

        private static void ProcessFeedUpdates(NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.Rootobject results)
        {
            using (var trans = DataModelManager.RealmInstance.BeginWrite())
            {

                foreach (var item in results.feeds.FeedItems)
                //foreach (var item in results.feeds.FeedItems.Where(t => t.properties.feed_title == "AnandTech"))
                {
                    //TODO: dependency inject the realmness
                    var thisFeed = DataModelManager.RealmInstance.All<NewsFeed>().Where(fe => fe.Id == item.id).FirstOrDefault();
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



                            DataModelManager.RealmInstance.Add(thisFeed);
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

                trans.Commit();
            }
        }

        public void RegisterCredentialPrompt(ICredentialsPrompt prompt)
        {
            _prompt = prompt;
            lock (_syncLock)
            {
                if (_isSynchronizing)
                {
                    _prompt.ShowProgress();
                }
            }
        }
    }
}
