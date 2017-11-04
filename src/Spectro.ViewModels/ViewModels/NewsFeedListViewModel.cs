using System.Collections.Specialized;
using Spectro.DataModel;
using System.Linq;
using System.Threading.Tasks;
using Cimbalino.Toolkit.Services;
using Realms;

namespace Spectro.ViewModels
{
    public class NewsFeedListViewModel : SpectroViewModelBase
    {
        public NewsFeedListViewModel()
        {
        }

        public INotifyCollectionChanged FeedItemSource { get; private set; }

        public INotifyCollectionChanged StoryItemSource { get; private set; }

        public void SelectFeed(NewsFeed newsFeed)
        {
            StoryItemSource = DataModelManager.RealmInstance.All<Story>().Where(st => st.FeedId == newsFeed.Id && st.ReadStatus == 0).AsRealmCollection();
            RaisePropertyChanged(nameof(StoryItemSource));
        }

        public override Task OnNavigatedToAsync(NavigationServiceNavigationEventArgs eventArgs)
        {
            this.FeedItemSource = DataModelManager.RealmInstance.All<NewsFeed>().Where(it => it.UnreadCount > 0).OrderBy(ob => ob.Title).AsRealmCollection();
            return base.OnNavigatedToAsync(eventArgs);
        }
    }
}
