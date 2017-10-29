using System.Collections;

namespace Spectro.ViewModels
{
    public class NewsFeedListViewModel
    {
        public NewsFeedListViewModel(IList list)
        {
            this.ItemSource = list;
        }

        public IList ItemSource { get; private set; }
    }
}
