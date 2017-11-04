using Spectro.Core.Interfaces;
using Spectro.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using GalaSoft.MvvmLight.Ioc;

namespace Spectro.Views
{
    public sealed partial class NavigationRoot
    {
        public NavigationRootViewModel ViewModel => DataContext as NavigationRootViewModel;

        public NavigationRoot()
        {
            InitializeComponent();
        }

        public void ItemInvoked(object sender, NavigationViewItemInvokedEventArgs args)
        {
            var navigation = SimpleIoc.Default.GetInstance<ISpectroNavigationService>();
            if (args.IsSettingsInvoked)
            {
                navigation.NavigateToSettings();
            }
            
            if (!(args.InvokedItem is NavigationViewItem item))
            {
                return;
            }

            if (item.Tag.ToString() == "Profile")
            {
                navigation.NavigateToProfile();
            }
            else
            {
                navigation.NavigateToNewsFeed();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SimpleIoc.Default.GetInstance<ISpectroNavigationService>().RegisterFrame(AppNavFrame);
            base.OnNavigatedTo(e);
        }
    }
}
