using System;
using System.Threading.Tasks;
using Spectro.Core.Commands;
using Spectro.Core.Interfaces;

namespace Spectro.ViewModels
{
    public class LoginViewModel : SpectroViewModelBase
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly ISpectroNavigationService _navigationService;

        private string _username;
        private string _password;

        private bool _isLoggingIn;

        public LoginViewModel(
            IAuthenticationService authenticationService,
            ISpectroNavigationService navigationService)
        {
            _authenticationService = authenticationService;
            _navigationService = navigationService;
        }

        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                {
                    OnPropertyChanged(nameof(CanLogIn));
                    LoginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set
            {
                if (SetProperty(ref _isLoggingIn, value))
                {
                    OnPropertyChanged(nameof(CanLogIn));
                    LoginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanLogIn => !string.IsNullOrWhiteSpace(Username)
                                && !IsLoggingIn;

        private AsyncRelayCommand _loginCommand;
        public AsyncRelayCommand LoginCommand => _loginCommand ??= new AsyncRelayCommand(Login);

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
                    _navigationService.NavigateToRootNavigation(null);
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
