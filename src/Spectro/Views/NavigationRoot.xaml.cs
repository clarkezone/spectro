using GalaSoft.MvvmLight.Ioc;
using Spectro.Services;
using Spectro.ViewModels;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Spectro.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NavigationRoot : Page
    {
        public NavigationRootViewModel Vm => (NavigationRootViewModel)DataContext;

        public NavigationRoot()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //TODO: replace with viewmodellocator

            appNavFrame.Navigate(typeof(NewsFeedList));
        }

        public void NavigateToFeedsTapped(object sender, RoutedEventArgs args)
        {
            Vm.NavigateCommand.Execute("");
        }

        public void NavigateToProfileTapped(object sender, RoutedEventArgs args)
        {
            Vm.NavigateCommand.Execute("");
        }

    }
}
