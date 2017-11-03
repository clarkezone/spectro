﻿using Spectro.ViewModels;
using Windows.UI.Xaml.Navigation;

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

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.Initialize();
        }
    }
}
