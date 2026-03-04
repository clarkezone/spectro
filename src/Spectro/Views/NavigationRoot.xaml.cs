using Spectro.Core.Interfaces;
using Spectro.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;

namespace Spectro.Views
{
    public sealed partial class NavigationRoot
    {
        public NavigationRootViewModel ViewModel => DataContext as NavigationRootViewModel;

        public NavigationRoot()
        {
            InitializeComponent();
            DataContext = ViewModelLocator.ServiceProvider.GetRequiredService<NavigationRootViewModel>();
            Loaded += (s, e) =>
            {
                ViewModelLocator.ServiceProvider.GetRequiredService<ISpectroNavigationService>().RegisterFrame(AppNavFrame);
            };
        }

        public void ItemInvoked(object sender, NavigationViewItemInvokedEventArgs args)
        {
            var navigation = ViewModelLocator.ServiceProvider.GetRequiredService<ISpectroNavigationService>();
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
