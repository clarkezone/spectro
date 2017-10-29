using Spectro.Commands;
using Spectro.Core.Interfaces;
using System.ComponentModel;
using System.Diagnostics;
using System;

namespace Spectro.ViewModels
{
    public class NavigationRootViewModel : INotifyPropertyChanged
    {
        private INewsBlurService loginService;
        private LoginLogoutCommand loginCommand;
        private ICredentialsPrompt _credentialsPrompt;

        public NavigationRootViewModel(INewsBlurService service)
        {
            this.loginService = service;
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

        internal void NotifyLoginButtonStateChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoginButtonText)));
        }

        public string LoginButtonText
        {
            get
            {
                return
                  loginService.CurrentSession.IsLoggedIn ? "Logout" : "Login";
            }
        }

        public void RegisterCredentialsUX(ICredentialsPrompt ux)
        {
            _credentialsPrompt = ux;
        }
    }
}