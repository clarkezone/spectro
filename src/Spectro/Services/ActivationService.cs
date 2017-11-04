using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Spectro.Activation;

using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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

        public ActivationService(ISpectroNavigationService navigationService)
        {
            _navigationService = navigationService;
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
                    var frame = new Frame();
                    frame.Navigate(typeof(NavigationRoot));
                    // Create a Frame to act as the navigation context and navigate to the first page
                    Window.Current.Content = frame;
                }
            }

            var activationHandler = GetActivationHandlers()
                                                .FirstOrDefault(h => h.CanHandle(activationArgs));

            if (activationHandler != null)
            {
                await activationHandler.HandleAsync(activationArgs);
            }

            if (IsInteractive(activationArgs))
            {
                var defaultHandler = new DefaultLaunchActivationHandler(typeof(NewsFeedList), _navigationService);
                if (defaultHandler.CanHandle(activationArgs))
                {
                    await defaultHandler.HandleAsync(activationArgs);
                }

                // Ensure the current window is active
                Window.Current.Activate();

                // Tasks after activation
                await StartupAsync();
            }
        }

        private async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        private async Task StartupAsync()
        {
            await Task.CompletedTask;
        }

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
