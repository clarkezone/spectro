using System;
using System.Collections.ObjectModel;
using System.Linq;
using Spectro.DataModel;
using System.Threading.Tasks;
using Cimbalino.Toolkit.Extensions;
using Cimbalino.Toolkit.Services;
using Spectro.Core.DataModel;
using Spectro.Core.Interfaces;

namespace Spectro.ViewModels
{
    public class NewsFeedListViewModel : SpectroViewModelBase
    {
        private readonly IDataCacheService _dataCacheService;

        public NewsFeedListViewModel(IDataCacheService dataCacheService)
        {
            _dataCacheService = dataCacheService;
            _dataCacheService.DataChanged += DataCacheServiceOnDataChanged;
        }

        private async void DataCacheServiceOnDataChanged(object sender, EventArgs eventArgs)
        {
            var feeds = (await _dataCacheService.GetNewsFeeds(it => it.UnreadCount > 0)).OrderBy(ob => ob.Title).ToList();

            // Add items
            foreach (var feed in feeds)
            {
                var item = FeedItemSource.FirstOrDefault(x => x.Id == feed.Id);
                if (item == null)
                {
                    FeedItemSource.Add(feed);
                }
            }

            // Remove items
            foreach (var feed in FeedItemSource.ToList())
            {
                var item = feeds.FirstOrDefault(x => x.Id == feed.Id);
                if (item == null)
                {
                    FeedItemSource.Remove(feed);
                }
            }
        }

        public ObservableCollection<NewsFeed> FeedItemSource { get; private set; } = new ObservableCollection<NewsFeed>();

        public ObservableCollection<Story> StoryItemSource { get; } = new ObservableCollection<Story>();

        public async Task SelectFeed(NewsFeed newsFeed)
        {
            StoryItemSource.Clear();
            StoryItemSource.AddRange(await _dataCacheService.GetStories(x => x.FeedId == newsFeed.Id && x.ReadStatus == 0));
        }

        public override async Task OnNavigatedToAsync(NavigationServiceNavigationEventArgs eventArgs)
        {
            FeedItemSource = (await _dataCacheService.GetNewsFeeds(it => it.UnreadCount > 0)).OrderBy(ob => ob.Title).ToList().ToObservableCollection();
        }
    }
}
