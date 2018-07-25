using Spectro.Services;
using Spectro.Helpers;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using GalaSoft.MvvmLight.Ioc;

namespace Spectro
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private IActivationService ActivationService => SimpleIoc.Default.GetInstance<IActivationService>();

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            FluentHelpers.SetupTitlebar();
            if (!e.PrelaunchActivated)
            {
                await ActivationService.ActivateAsync(e); 
            }
        }

        /// <summary>
        /// Invoked when the application is activated by some means other than normal launching.
        /// </summary>
        /// <param name="args">Event data for the event.</param>
        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await ActivationService.ActivateAsync(args);
        }
    }
}
