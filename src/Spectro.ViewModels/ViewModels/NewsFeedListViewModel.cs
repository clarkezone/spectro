using System.Collections.Specialized;
using Spectro.DataModel;
using System.Linq;
using Realms;
using GalaSoft.MvvmLight;

namespace Spectro.ViewModels
{
    public class NewsFeedListViewModel : ViewModelBase
    {
        public NewsFeedListViewModel()
        {
            // TODO: This should probably be done at another stage rather than in the ctor
            this.FeedItemSource = DataModelManager.RealmInstance.All<NewsFeed>().Where(it => it.UnreadCount > 0).OrderBy(ob => ob.Title).AsRealmCollection();
        }

        public INotifyCollectionChanged FeedItemSource { get; }

        public INotifyCollectionChanged StoryItemSource { get; private set; }

        public void SelectFeed(NewsFeed newsFeed)
        {
            StoryItemSource = DataModelManager.RealmInstance.All<Story>().Where(st => st.FeedId == newsFeed.Id && st.ReadStatus == 0).AsRealmCollection();
            RaisePropertyChanged(nameof(StoryItemSource));
        }
    }
}
