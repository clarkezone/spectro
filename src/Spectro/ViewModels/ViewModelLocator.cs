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
            SimpleIoc.Default.Register<INewsBlurService, NewsBlurService>();
            SimpleIoc.Default.Register<ISpectroNavigationService, SpectroNavigationService>();
            SimpleIoc.Default.Register<IActivationService, ActivationService>();
            SimpleIoc.Default.Register<IAuthenticationService, AuthenticationService>();
            SimpleIoc.Default.Register<IDataCacheService, DataCacheService>();

            SimpleIoc.Default.Register<NavigationRootViewModel>();
            SimpleIoc.Default.Register<ProfileViewModel>();
            SimpleIoc.Default.Register<SettingsViewModel>();
            SimpleIoc.Default.Register<NewsFeedListViewModel>();
        }

        public SettingsViewModel SettingsViewModel => SimpleIoc.Default.GetInstance<SettingsViewModel>();

        public NavigationRootViewModel NavViewModel => SimpleIoc.Default.GetInstance<NavigationRootViewModel>();

        public NewsFeedListViewModel NewsList => SimpleIoc.Default.GetInstance<NewsFeedListViewModel>();

        public ProfileViewModel Profile => SimpleIoc.Default.GetInstance<ProfileViewModel>();
    }
}
