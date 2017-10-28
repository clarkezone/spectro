using Spectro.Commands;
using Spectro.Core.Interfaces;
using System.ComponentModel;
using System.Diagnostics;

namespace Spectro.ViewModels
{
    public class NavigationRootViewModel : INotifyPropertyChanged
    {
        private ILoginService loginService;
        private LoginLogoutCommand loginCommand;

        public NavigationRootViewModel(ILoginService service)
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
                  loginService.IsLoggedIn() ? "Logout" : "Login";
            }
        }

    }
}