using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Spectro.DataModel;
using System.Threading.Tasks;
using Cimbalino.Toolkit.Extensions;
using Cimbalino.Toolkit.Services;
using Realms;
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
            var feeds = (await _dataCacheService.GetNewsFeeds(it => it.UnreadCount > 0)).OrderBy(ob => ob.Title);
            foreach (var feed in feeds)
            {
                var item = FeedItemSource.FirstOrDefault(x => x.Id == feed.Id);
                if (item == null)
                {
                    FeedItemSource.Add(feed);
                }
            }
        }

        public ObservableCollection<NewsFeed> FeedItemSource { get; private set; } = new ObservableCollection<NewsFeed>();

        public ObservableCollection<Story> StoryItemSource { get; private set; } = new ObservableCollection<Story>();

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
