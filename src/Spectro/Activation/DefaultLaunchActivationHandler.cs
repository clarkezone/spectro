using System;
using System.Threading.Tasks;

using Spectro.Services;

using Windows.ApplicationModel.Activation;
using Spectro.Core.Interfaces;
using Spectro.Core.Services;
using Spectro.Helpers;

namespace Spectro.Activation
{
    internal class DefaultLaunchActivationHandler : ActivationHandler<LaunchActivatedEventArgs>
    {
        private readonly ISpectroNavigationService _navigationService;
        private readonly Type _navElement;
    
        public DefaultLaunchActivationHandler(Type navElement, ISpectroNavigationService navigationService)
        {
            _navigationService = navigationService;
            _navElement = navElement;
        }
    
        protected override async Task HandleInternalAsync(LaunchActivatedEventArgs args)
        {
            // When the navigation stack isn't restored navigate to the first page,
            // configuring the new page by passing required information as a navigation
            // parameter
            _navigationService.Navigate(_navElement, args.Arguments);

            await Task.CompletedTask;
        }

        protected override bool CanHandleInternal(LaunchActivatedEventArgs args)
        {
            // None of the ActivationHandlers has handled the app activation
            return true; // NavigationService.Frame.Content == null;
        }
    }
}
