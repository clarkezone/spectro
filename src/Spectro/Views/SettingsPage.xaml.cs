using System.ComponentModel;
using Spectro.ViewModels;
using Spectro.Core.Services;
namespace Spectro.Views
{
    public sealed partial class SettingsPage
    {
        private SettingsViewModel ViewModel => DataContext as SettingsViewModel;

        //// TODO WTS: Change the URL for your privacy policy in the Resource File, currently set to https://YourPrivacyUrlGoesHere

        public SettingsPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateThemeSelection();
        }

        private void Page_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.ElementTheme))
            {
                UpdateThemeSelection();
            }
        }

        private void UpdateThemeSelection()
        {
            LightThemeRadioButton.IsChecked = ViewModel.ElementTheme == SpectroTheme.Light;
            DarkThemeRadioButton.IsChecked = ViewModel.ElementTheme == SpectroTheme.Dark;
            DefaultThemeRadioButton.IsChecked = ViewModel.ElementTheme == SpectroTheme.Default;
        }
    }
}
