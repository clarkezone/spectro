using System;
using System.Threading.Tasks;
using Spectro.Core.Commands;
using Spectro.Core.Interfaces;

namespace Spectro.ViewModels
{
    public class LoginViewModel : SpectroViewModelBase
    {
        private readonly IAuthenticationService _authenticationService;

        private string _username;
        private string _password;

        private bool _isLoggingIn;

        public LoginViewModel(
            IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }

        public string Username
        {
            get => _username;
            set
            {
                if (Set(ref _username, value))
                {
                    RaisePropertyChanged(nameof(CanLogIn));
                    LoginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string Password
        {
            get => _password;
            set => Set(ref _password, value);
        }

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set
            {
                if (Set(ref _isLoggingIn, value))
                {
                    RaisePropertyChanged(nameof(CanLogIn));
                    LoginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanLogIn => !string.IsNullOrWhiteSpace(Username)
                                && !IsLoggingIn;

        public AsyncRelayCommand LoginCommand => new AsyncRelayCommand(Login);

        private async Task Login()
        {
            if (!CanLogIn)
            {
                return;
            }

            try
            {
                IsLoggingIn = true;

                if (await _authenticationService.Login(Username, Password))
                {
                    ClearDown();
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        private void ClearDown()
        {
            Username = Password = null;
        }
    }
}
