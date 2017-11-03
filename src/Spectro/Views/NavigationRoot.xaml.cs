using Spectro.Core.Interfaces;
using Spectro.Helpers;
using Spectro.Services;
using Spectro.ViewModels;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Microsoft.Toolkit.Uwp.Helpers;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Spectro.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NavigationRoot : ICredentialsPrompt
    {
        public NavigationRootViewModel Vm => DataContext as NavigationRootViewModel;

        public NavigationRoot()
        {
            this.InitializeComponent();
            Singleton<NavigationServiceEx>.Instance.Frame = appNavFrame;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //TODO: replace with viewmodellocator

            appNavFrame.Navigate(typeof(NewsFeedList));
            Vm.RegisterCredentialsUX(this);
        }

        public void NavigateToFeedsTapped(object sender, RoutedEventArgs args)
        {
            //Vm.NavigateCommand.Execute("");
        }

        public void NavigateToProfileTapped(object sender, RoutedEventArgs args)
        {
            //Vm.NavigateCommand.Execute("");
        }

        public void ItemInvoked(object sender, NavigationViewItemInvokedEventArgs args)
        {
            //Expeectiving navigationex

            //TODO: localize this
            //TODO: switch statement
            if (args.InvokedItem.ToString() == "Profile")
            {
                Singleton<NavigationServiceEx>.Instance.Navigate(typeof(ProfileViewModel).FullName);
            } else
            {
                Singleton<NavigationServiceEx>.Instance.Navigate(typeof(NewsFeedListViewModel).FullName);
            }

            if (args.IsSettingsInvoked)
            {
                Singleton<NavigationServiceEx>.Instance.Navigate(typeof(SettingsViewModel).FullName);
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
    }
}
