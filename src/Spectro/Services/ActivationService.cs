using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Spectro.Activation;

using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Spectro.Controls;
using Spectro.Core.Interfaces;
using Spectro.Core.Services;
using Spectro.Views;

namespace Spectro.Services
{
    //For more information on application activation see https://github.com/Microsoft/WindowsTemplateStudio/blob/master/docs/activation.md
    internal interface IActivationService
    {
        Task ActivateAsync(object activationArgs);
    }

    internal class ActivationService : IActivationService
    {
        private readonly ISpectroNavigationService _navigationService;
        private readonly IDataCacheService _dataCacheService;
        private readonly IAuthenticationService _authenticationService;
        private readonly IThemeService _themeService;

        public ActivationService(
            ISpectroNavigationService navigationService,
            IDataCacheService dataCacheService,
            IAuthenticationService authenticationService,
            IThemeService themeService)
        {
            _navigationService = navigationService;
            _dataCacheService = dataCacheService;
            _authenticationService = authenticationService;
            _themeService = themeService;
            _authenticationService.LoggedInStatusChanged += AuthenticationServiceOnLoggedInStatusChanged;
        }

        private async void AuthenticationServiceOnLoggedInStatusChanged(object sender, LoggedInStatusChangedEventArgs e)
        {
            if (!e.IsLoggedIn)
            {
                await CreateAndActivateFrame(typeof(NavigationRoot));
            }
        }

        public async Task ActivateAsync(object activationArgs)
        {
            if (IsInteractive(activationArgs))
            {
                // Initialize things like registering background task before the app is loaded
                await InitializeAsync();

                // Do not repeat app initialization when the Window already has content,
                // just ensure that the window is active
                if (Window.Current.Content == null)
                {
                    await CreateAndActivateFrame(typeof(NavigationRoot));
                }
            }

            var activationHandler = GetActivationHandlers()
                .FirstOrDefault(h => h.CanHandle(activationArgs));

            if (activationHandler != null)
            {
                await activationHandler.HandleAsync(activationArgs);
            }

            if (IsInteractive(activationArgs) && _authenticationService.IsLoggedIn)
            {
                var defaultHandler = new DefaultLaunchActivationHandler(typeof(NewsFeedList), _navigationService);
                if (defaultHandler.CanHandle(activationArgs))
                {
                    await defaultHandler.HandleAsync(activationArgs);
                }

                // Tasks after activation
                await StartupAsync();
            }
        }

        private async Task CreateAndActivateFrame(Type firstNavigation)
        {
            var frame = new Frame();
            _navigationService.RegisterFrame(frame);
            frame.Navigate(firstNavigation);

            // Create a Frame to act as the navigation context and navigate to the first page
            Window.Current.Content = frame;

            await _themeService.Initialize();

            // Ensure the current window is active
            Window.Current.Activate();
        }

        private async Task InitializeAsync()
        {
            await _dataCacheService.Startup();
        }

        private Task StartupAsync() => Task.CompletedTask;

        private IEnumerable<ActivationHandler> GetActivationHandlers()
        {

            yield break;
        }

        private bool IsInteractive(object args)
        {
            return args is IActivatedEventArgs;
        }
    }
}
