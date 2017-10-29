using Spectro.Commands;
using Spectro.Core.Interfaces;
using System.ComponentModel;
using System;

namespace Spectro.ViewModels
{
    public class NavigationRootViewModel : INotifyPropertyChanged
    {
        private INewsBlurService loginService;
        private LoginLogoutCommand loginCommand;
        private ICredentialsPrompt _credentialsPrompt;
        Func<string, string> _getResource;

        public NavigationRootViewModel(INewsBlurService service, Func<string,string> getResource)
        {
            this.loginService = service;
            this.loginService.CurrentSession.PropertyChanged += CurrentSession_PropertyChanged;
            _getResource = getResource;
        }

        private void CurrentSession_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyLoginStateChanged();
        }

        public LoginLogoutCommand LoginLogoutCommand
        {
            get
            {
                return loginCommand
                       ?? (loginCommand = new LoginLogoutCommand(loginService, this));
            }
        }

        public ICredentialsPrompt CredentialsPrompt
        {
            get
            {
                return _credentialsPrompt;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        internal void NotifyLoginStateChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoginButtonText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProfileImageUri)));
        }

        public string LoginButtonText
        {
            get
            {
                return
                  loginService.CurrentSession.IsLoggedIn ? loginService.CurrentSession.UserName : _getResource("NavigationRoot_Login");
            }
        }

        public Uri ProfileImageUri
        {
            get
            {
                return
                  loginService.CurrentSession.IsLoggedIn ? new Uri(loginService.CurrentSession.PhotoUrl) : null;
            }
        }

        public void RegisterCredentialsUX(ICredentialsPrompt ux)
        {
            _credentialsPrompt = ux;
        }
    }
}