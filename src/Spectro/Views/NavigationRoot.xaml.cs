using Spectro.Core.Interfaces;
using Spectro.Helpers;
using Spectro.Services;
using Spectro.ViewModels;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml.Navigation;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Toolkit.Uwp.Helpers;
using Spectro.Core.Services;

namespace Spectro.Views
{
    public sealed partial class NavigationRoot : ICredentialsPrompt
    {
        public NavigationRootViewModel Vm => DataContext as NavigationRootViewModel;

        public NavigationRoot()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Vm.RegisterCredentialsUX(this);
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

        #region CredentialsPrompt
        public async Task<bool> PromptCredentials()
        {
            var result = await credentialsPrompt.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        public void ShowProgress()
        {
            //TODO task
            DispatcherHelper.ExecuteOnUIThreadAsync(() => {
                serviceProgress.Visibility = Visibility.Visible;
            });
        }

        public void HideProgress()
        {
            //TODO task
            DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                serviceProgress.Visibility = Visibility.Collapsed;
            });
        }

        public async Task ShowError(string v)
        {
            MessageDialog md = new MessageDialog(v);
            await md.ShowAsync();
        }

        public (string Username, string Password) GetUsernamePassword()
        {
            return credentialsPrompt.GetUsernamePassword();
        }

        public bool HaveNetwork()
        {
            return Microsoft.Toolkit.Uwp.Connectivity.NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable;
        }
        #endregion

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SimpleIoc.Default.GetInstance<ISpectroNavigationService>().RegisterFrame(AppNavFrame);
            base.OnNavigatedTo(e);
        }
    }
}
