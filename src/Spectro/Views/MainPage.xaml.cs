using Spectro.ViewModels;

using Windows.UI.Xaml.Controls;

namespace Spectro.Views
{
    public sealed partial class MainPage
    {
        private MainViewModel ViewModel
        {
            get { return DataContext as MainViewModel; }
        }

        public MainPage()
        {
            InitializeComponent();
        }
    }
}
