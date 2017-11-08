using Spectro.Core.Interfaces;
using System;
using System.Threading.Tasks;
using Cimbalino.Toolkit.Services;
using Spectro.Core.Commands;
using Spectro.Core.Extensions;
using Spectro.Core.Services;

namespace Spectro.ViewModels
{
    public class NavigationRootViewModel : SpectroViewModelBase
    {
        private readonly ITranslationService _translationService;
        private readonly ISpectroNavigationService _navigationService;
        private readonly IAuthenticationService _authenticationService;
        private readonly ISynchronizer _synchronizer;
        private readonly IProgressService _progressService;
        private readonly IApplicationInformationService _applicationInformationService;
        private AsyncRelayCommand _loginCommand;

        public NavigationRootViewModel(
            ITranslationService translationService,
            ISpectroNavigationService navigationService,
            IAuthenticationService authenticationService,
            ISynchronizer synchronizer,
            IProgressService progressService,
            IApplicationInformationService applicationInformationService)
        {
            _translationService = translationService;
            _navigationService = navigationService;
            _authenticationService = authenticationService;
            _synchronizer = synchronizer;
            _progressService = progressService;
            _applicationInformationService = applicationInformationService;
            _authenticationService.LoggedInStatusChanged += AuthenticationServiceOnLoggedInStatusChanged;
            progressService.ProgressStatusChanged += ProgressServiceOnProgressStatusChanged;
        }

        public bool IsProgressVisible => _progressService.ProgressIsVisible;

        public bool IsLoggedIn => _authenticationService.IsLoggedIn;

        public AsyncRelayCommand LoginLogoutCommand => _loginCommand
                                                       ?? (_loginCommand = new AsyncRelayCommand(LoginLogout));

        public string LoginButtonText => _authenticationService.IsLoggedIn
            ? _authenticationService.LoggedInUser.Username
            : _translationService.GetString("NavigationRoot_Login");

        public Uri ProfileImageUri => _authenticationService.IsLoggedIn ? new Uri(_authenticationService.LoggedInUser.PhotoUrl) : null;

        public string ApplicationName => _applicationInformationService.AppName;

        private void ProgressServiceOnProgressStatusChanged(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(IsProgressVisible));
        }

        private void AuthenticationServiceOnLoggedInStatusChanged(object sender, LoggedInStatusChangedEventArgs e) 
            => NotifyLoginStateChanged();

        internal void NotifyLoginStateChanged()
        {
            if (IsLoggedIn)
            {
                _synchronizer.StartSync().DontAwait("Just let this go off and run in the background");
            }

            RaisePropertyChanged(nameof(IsLoggedIn));
            RaisePropertyChanged(nameof(LoginButtonText));
            RaisePropertyChanged(nameof(ProfileImageUri));
        }

        public override Task OnNavigatedToAsync(NavigationServiceNavigationEventArgs eventArgs)
        {
            _synchronizer.StartSync().DontAwait("Just let this go off and run in the background");
            _navigationService.NavigateToNewsFeed();
            _navigationService.ClearBackstack();
            return base.OnNavigatedToAsync(eventArgs);
        }

        private async Task LoginLogout()
        {
            await _authenticationService.Logout();
        }
    }
}