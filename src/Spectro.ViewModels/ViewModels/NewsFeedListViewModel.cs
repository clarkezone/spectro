using System.Collections.Specialized;
using System.Linq;
using Spectro.DataModel;
using System.Threading.Tasks;
using Cimbalino.Toolkit.Extensions;
using Cimbalino.Toolkit.Services;
using Spectro.Core.Interfaces;

namespace Spectro.ViewModels
{
    public class NewsFeedListViewModel : SpectroViewModelBase
    {
        private readonly IDataCacheService _dataCacheService;

        public NewsFeedListViewModel(IDataCacheService dataCacheService)
        {
            _dataCacheService = dataCacheService;
        }

        public INotifyCollectionChanged FeedItemSource { get; private set; }

        public INotifyCollectionChanged StoryItemSource { get; private set; }

        public async Task SelectFeed(NewsFeed newsFeed)
        {
            StoryItemSource = (await _dataCacheService.GetStories(x => x.FeedId == newsFeed.FeedId && x.ReadStatus == 0)).ToObservableCollection();
            RaisePropertyChanged(nameof(StoryItemSource));
        }

        public override async Task OnNavigatedToAsync(NavigationServiceNavigationEventArgs eventArgs)
        {
            FeedItemSource = (await _dataCacheService.GetNewsFeeds(it => it.UnreadCount > 0)).OrderBy(ob => ob.Title).ToList().ToObservableCollection();
        }
    }
}
