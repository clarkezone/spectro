using Spectro.Core.Interfaces;
using Spectro.ViewModels;
using Windows.UI.Xaml.Controls;
using GalaSoft.MvvmLight.Ioc;

namespace Spectro.Views
{
    public sealed partial class NavigationRoot
    {
        public NavigationRootViewModel ViewModel => DataContext as NavigationRootViewModel;

        public NavigationRoot()
        {
            InitializeComponent();
            SimpleIoc.Default.GetInstance<ISpectroNavigationService>().RegisterFrame(AppNavFrame);
        }

        public void ItemInvoked(object sender, NavigationViewItemInvokedEventArgs args)
        {
            var navigation = SimpleIoc.Default.GetInstance<ISpectroNavigationService>();
            if (args.IsSettingsInvoked)
            {
                navigation.NavigateToSettings();
                return;
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

        private double BlurAmount(bool isLoggedIn) => isLoggedIn ? 0 : 2;
    }
}
