using NewsBlurSharp;
using Spectro.Core.Interfaces;
using Spectro.DataModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Threading;

namespace Spectro.Core.Services
{
    class Synchronizer
    {
        private NewsBlurClient _api;
        private bool _isSynchronizing;
        private NewsBlurService _service;
        private object _syncLock = new object();
        private ICredentialsPrompt _prompt;

        public Synchronizer(NewsBlurClient api, NewsBlurService parent)
        {
            //TODO: this should be an interface
            _api = api;
            _service = parent;
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

                    var results = await _api.GetFeedsAsync(false);

                    var trans = DataModelManager.RealmInstance.BeginWrite();

                    foreach (var item in results.feeds.FeedItems)
                    {
                        //TODO: dependency inject the realmness
                        //var thisFeed = DataModelManager.RealmInstance.All<NewsFeed>().Where(fe => fe.Id == item.id).FirstOrDefault();
                        //if (thisFeed == null)
                        {
                            try
                            {
                                Debug.WriteLine(item.properties.last_story_date);
                                var thisFeed = new NewsFeed()
                                {
                                    Id = item.id,
                                    FeedUri = item.properties.feed_address,
                                    Title = item.properties.feed_title,
                                    IconUri = item.properties.favicon_url,
                                    Active = item.properties.active,
                                    LastStoryDateFromService = !string.IsNullOrEmpty(item.properties.last_story_date) ? DateTimeOffset.Parse(item.properties.last_story_date) : DateTimeOffset.MinValue
                                };

                                thisFeed.UnreadCount = DataModelManager.RealmInstance.All<Story>().Where(st => st.ReadStatus == 0 && st.FeedId == thisFeed.Id).Count();
                                //thisFeed.UnreadCount = 0;

                                DataModelManager.RealmInstance.Add(thisFeed, true);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                            }
                        }
                    }

                    trans.Commit();

                    ////var query = DataModelManager.RealmInstance.All<NewsFeed>().Where(ld => ld.LastStoryDateDownloaded != ld.LastStoryDateFromService);
                    //var query = DataModelManager.RealmInstance.All<NewsFeed>().Where(ld => ld.Active);
                    //Debug.WriteLine($"Feeds needing update: {query.Count()}");

                    //foreach (var localFeed in query)
                    //{
                    //    //TODO: full vs only unread
                    //    if (localFeed.LastStoryDateDownloaded == localFeed.LastStoryDateFromService)
                    //    {
                    //        Debug.WriteLine($"Skipping {localFeed.Title}");
                    //    }
                    //    else
                    //    {
                    //        Debug.WriteLine($"DateFromService:{localFeed.LastStoryDateFromService} DateFromDownloaded:{localFeed.LastStoryDateDownloaded} ");
                    //        NewsBlurSharp.Model.GetStoriesResponse.Rootobject result = null;
                    //        AutoResetEvent ar = new AutoResetEvent(false);
                    //        //var stories = await _api.GetStoriesAsync(localFeed.Id);
                    //        var task = _api.GetStoriesAsync(localFeed.Id).ContinueWith(a =>
                    //        {
                    //            result = a.Result;
                    //            ar.Set();
                    //        });

                    //        ar.WaitOne();

                    //        if (result == null)
                    //        {
                    //            throw new Exception("Error: webcall failed");
                    //        }

                    //        var addedNewStory = false;
                    //        trans = DataModelManager.RealmInstance.BeginWrite();
                    //        foreach (var story in result.stories)
                    //        {
                    //            var storyId = story.id;
                    //            var storyExists = DataModelManager.RealmInstance.All<Story>().Where(fe => fe.Id == storyId).FirstOrDefault();
                    //            if (storyExists == null)
                    //            {
                    //                Story s = new Story()
                    //                {
                    //                    Id = storyId,
                    //                    Title = story.story_title,
                    //                    FeedId = story.story_feed_id,
                    //                    ReadStatus = story.read_status,
                    //                    //story.story_timestamp
                    //                    Content = story.story_content,
                    //                    Feed = localFeed
                    //                };
                    //                DataModelManager.RealmInstance.Add(s);
                    //                addedNewStory = true;
                    //            }
                    //        }
                    //        if (addedNewStory)
                    //        {
                    //            //TODO: update counts in feed
                    //        }

                    //        localFeed.UnreadCount = DataModelManager.RealmInstance.All<Story>().Where(st => st.ReadStatus == 0 && st.Feed==localFeed).Count();

                    //        // no need to do a story pass until this changes
                    //        localFeed.LastStoryDateDownloaded = localFeed.LastStoryDateFromService;
                    //        trans.Commit();
                    //    }
                    //}

                    lock (_syncLock)
                    {
                        _isSynchronizing = false;
                    }
                    _prompt?.HideProgress();
                    //TODO: dispatcherhelp stop progress
                });
            }
        }

        internal void RegisterCredentialPrompt(ICredentialsPrompt prompt)
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
