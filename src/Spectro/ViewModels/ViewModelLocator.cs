using Microsoft.Extensions.DependencyInjection;
using Spectro.Core.Interfaces;
using Spectro.Core.Services;
using Spectro.Services;
using NewsBlurSharp;

namespace Spectro.ViewModels
{
    public class ViewModelLocator
    {
        public static ServiceProvider ServiceProvider { get; private set; }

        public ViewModelLocator()
        {
            var services = new ServiceCollection();

            // Platform services
            services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
            services.AddSingleton<IDispatcherService, DispatcherService>();

            // Local services
            services.AddSingleton<INewsBlurClient>(sp => new NewsBlurClient());
            services.AddSingleton<ISynchronizer, Synchronizer>();
            services.AddSingleton<ITranslationService, TranslationService>();
            services.AddSingleton<ISpectroNavigationService, SpectroNavigationService>();
            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<IDataCacheService, RealmDataCacheService>();
            services.AddSingleton<IProgressService, ProgressService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IApplicationInformationService, ApplicationInformationService>();

            // ViewModels
            services.AddSingleton<NavigationRootViewModel>();
            services.AddSingleton<ProfileViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<NewsFeedListViewModel>();
            services.AddSingleton<LoginViewModel>();

            ServiceProvider = services.BuildServiceProvider();
        }

        public SettingsViewModel SettingsViewModel => ServiceProvider.GetRequiredService<SettingsViewModel>();

        public NavigationRootViewModel NavViewModel => ServiceProvider.GetRequiredService<NavigationRootViewModel>();

        public NewsFeedListViewModel NewsList => ServiceProvider.GetRequiredService<NewsFeedListViewModel>();

        public ProfileViewModel Profile => ServiceProvider.GetRequiredService<ProfileViewModel>();

        public LoginViewModel Login => ServiceProvider.GetRequiredService<LoginViewModel>();
    }
}
