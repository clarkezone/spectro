using Spectro.ViewModels;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Spectro.Views
{
    /// <summary>
    /// The main view displaying list of subscribed feeds
    /// </summary>
    public sealed partial class NewsFeedList : Page
    {
        public NewsFeedListViewModel Vm => (NewsFeedListViewModel) DataContext;

        public NewsFeedList()
        {
            this.InitializeComponent();
        }
    }
}
