using Spectro.Core.Interfaces;
using System.ComponentModel;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Cimbalino.Toolkit.Services;
using GalaSoft.MvvmLight.Command;
using Spectro.Core.Services;

namespace Spectro.ViewModels
{
    public class NavigationRootViewModel : SpectroViewModelBase
    {
        private readonly INewsBlurService _loginService;
        private readonly ITranslationService _translationService;
        private readonly ISpectroNavigationService _navigationService;
        private RelayCommand _loginCommand;

        public NavigationRootViewModel(
            INewsBlurService service,
            ITranslationService translationService,
            ISpectroNavigationService navigationService)
        {
            this._loginService = service;
            _translationService = translationService;
            _navigationService = navigationService;
            this._loginService.CurrentSession.PropertyChanged += CurrentSession_PropertyChanged;
        }

        private void CurrentSession_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyLoginStateChanged();
        }

        public RelayCommand LoginLogoutCommand => _loginCommand
                                                  ?? (_loginCommand = new RelayCommand(LoginLogout));

        internal void NotifyLoginStateChanged()
        {
            RaisePropertyChanged(nameof(LoginButtonText));
            RaisePropertyChanged(nameof(ProfileImageUri));
        }

        public string LoginButtonText => _loginService.CurrentSession.IsLoggedIn ? _loginService.CurrentSession.UserName : _translationService.GetString("NavigationRoot_Login");

        public Uri ProfileImageUri => _loginService.CurrentSession.IsLoggedIn ? new Uri(_loginService.CurrentSession.PhotoUrl) : null;

        public void RegisterCredentialsUX(ICredentialsPrompt ux) => _loginService.RegisterCredentialPrompt(ux);

        public override Task OnNavigatedToAsync(NavigationServiceNavigationEventArgs eventArgs)
        {
            _navigationService.NavigateToNewsFeed();
            return base.OnNavigatedToAsync(eventArgs);
        }

        private void LoginLogout()
        {
            if (this._loginService.CurrentSession.IsLoggedIn)
            {
                this._loginService.Logout();
            }
            else
            {
                this._loginService.Login().ContinueWith((e) =>
                {
                    Debug.WriteLine(e.IsFaulted);
                });
            }

            NotifyLoginStateChanged();
        }
    }
}