using Spectro.Core.Interfaces;
using Spectro.ViewModels;
using System;
using System.Diagnostics;
using System.Windows.Input;

namespace Spectro.Commands
{
    public class LoginLogoutCommand : ICommand
    {
        INewsBlurService loginService;
        NavigationRootViewModel viewModel;

        public LoginLogoutCommand(INewsBlurService service, NavigationRootViewModel model)
        {
            this.loginService = service;
            this.viewModel = model;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            if (this.loginService.CurrentSession.IsLoggedIn)
            {
                this.loginService.Logout();
            }
            else
            {
                this.loginService.Login();
            }
            
            viewModel.NotifyLoginButtonStateChanged();
        }
    }
}
