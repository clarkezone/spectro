using System;
using System.Collections;
using System.Collections.Specialized;
using Spectro.DataModel;
using System.Linq;
using Realms;
using System.ComponentModel;

namespace Spectro.ViewModels
{
    public class NewsFeedListViewModel : INotifyPropertyChanged
    {
        public NewsFeedListViewModel(INotifyCollectionChanged list)
        {
            this.FeedItemSource = list;
        }

        public INotifyCollectionChanged FeedItemSource { get; private set; }

        public INotifyCollectionChanged StoryItemSource { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void SelectFeed(NewsFeed newsFeed)
        {
            StoryItemSource = DataModelManager.RealmInstance.All<Story>().Where(st => st.FeedId == newsFeed.Id && st.ReadStatus == 0).AsRealmCollection();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StoryItemSource)));
        }
    }
}
