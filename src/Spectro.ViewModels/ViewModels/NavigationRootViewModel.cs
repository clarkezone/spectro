using Spectro.Core.Interfaces;
using System;
using System.Threading.Tasks;
using Cimbalino.Toolkit.Services;
using Spectro.Core.Commands;
using Spectro.Core.Services;

namespace Spectro.ViewModels
{
    public class NavigationRootViewModel : SpectroViewModelBase
    {
        private readonly ITranslationService _translationService;
        private readonly ISpectroNavigationService _navigationService;
        private readonly IAuthenticationService _authenticationService;
        private AsyncRelayCommand _loginCommand;

        public NavigationRootViewModel(
            ITranslationService translationService,
            ISpectroNavigationService navigationService,
            IAuthenticationService authenticationService)
        {
            _translationService = translationService;
            _navigationService = navigationService;
            _authenticationService = authenticationService;
            _authenticationService.LoggedInStatusChanged += AuthenticationServiceOnLoggedInStatusChanged;
        }

        private void AuthenticationServiceOnLoggedInStatusChanged(object sender, LoggedInStatusChangedEventArgs e) 
            => NotifyLoginStateChanged();

        public AsyncRelayCommand LoginLogoutCommand => _loginCommand
                                                  ?? (_loginCommand = new AsyncRelayCommand(LoginLogout));

        internal void NotifyLoginStateChanged()
        {
            RaisePropertyChanged(nameof(LoginButtonText));
            RaisePropertyChanged(nameof(ProfileImageUri));
        }

        public string LoginButtonText => _authenticationService.IsLoggedIn ? _authenticationService.LoggedInUser.Username : _translationService.GetString("NavigationRoot_Login");

        public Uri ProfileImageUri => _authenticationService.IsLoggedIn ? new Uri(_authenticationService.LoggedInUser.PhotoUrl) : null;

        public override Task OnNavigatedToAsync(NavigationServiceNavigationEventArgs eventArgs)
        {
            _navigationService.NavigateToNewsFeed();
            return base.OnNavigatedToAsync(eventArgs);
        }

        private async Task LoginLogout()
        {
            await _authenticationService.Logout();

            _navigationService.NavigateToLogin();
        }
    }
}