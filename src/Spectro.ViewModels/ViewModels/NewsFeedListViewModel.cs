using GalaSoft.MvvmLight;
using Microsoft.Practices.ServiceLocation;
using System.Collections;

namespace Spectro.ViewModels
{
    public class NewsFeedListViewModel : ViewModelBase
    {
        public NewsFeedListViewModel(IList list)
        {
            this.ItemSource = list;
        }

        public IList ItemSource { get; private set; }
    }
}
