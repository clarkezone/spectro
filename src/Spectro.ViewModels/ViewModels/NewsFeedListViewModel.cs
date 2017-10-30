using System.Collections;
using System.Collections.Specialized;

namespace Spectro.ViewModels
{
    public class NewsFeedListViewModel
    {
        public NewsFeedListViewModel(INotifyCollectionChanged list)
        {
            this.ItemSource = list;
        }

        public INotifyCollectionChanged ItemSource { get; private set; }
    }
}
