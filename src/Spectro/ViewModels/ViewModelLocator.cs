using Cimbalino.Toolkit.Services;
using Spectro.Core.Services;
using Spectro.Services;
using GalaSoft.MvvmLight.Ioc;
using NewsBlurSharp;
using Spectro.Core.Interfaces;

namespace Spectro.ViewModels
{
    public class ViewModelLocator
    {
        public ViewModelLocator()
        {
            // Cimbalino Services
            SimpleIoc.Default.Register<IApplicationSettingsService, ApplicationSettingsService>();

            // Local services
            SimpleIoc.Default.Register<INewsBlurClient>(() => new NewsBlurClient());
            SimpleIoc.Default.Register<ISynchronizer, Synchronizer>();
            SimpleIoc.Default.Register<ITranslationService, TranslationService>();
            SimpleIoc.Default.Register<ISpectroNavigationService, SpectroNavigationService>();
            SimpleIoc.Default.Register<IActivationService, ActivationService>();
            SimpleIoc.Default.Register<IAuthenticationService, AuthenticationService>();
            SimpleIoc.Default.Register<IDataCacheService, DataCacheService>();
            SimpleIoc.Default.Register<IDispatcherService, DispatcherService>();
            SimpleIoc.Default.Register<IProgressService, ProgressService>();
            SimpleIoc.Default.Register<IThemeService, ThemeService>();

            SimpleIoc.Default.Register<NavigationRootViewModel>();
            SimpleIoc.Default.Register<ProfileViewModel>();
            SimpleIoc.Default.Register<SettingsViewModel>();
            SimpleIoc.Default.Register<NewsFeedListViewModel>();
            SimpleIoc.Default.Register<LoginViewModel>();
        }

        public SettingsViewModel SettingsViewModel => Get<SettingsViewModel>();

        public NavigationRootViewModel NavViewModel => Get<NavigationRootViewModel>();

        public NewsFeedListViewModel NewsList => Get<NewsFeedListViewModel>();

        public ProfileViewModel Profile => Get<ProfileViewModel>();

        public LoginViewModel Login => Get<LoginViewModel>();

        private static T Get<T>() => SimpleIoc.Default.GetInstance<T>();
    }
}
