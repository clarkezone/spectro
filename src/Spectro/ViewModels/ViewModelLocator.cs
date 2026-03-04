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

            // Platform services — explicit factory lambdas (AOT-safe, no reflection)
            services.AddSingleton<IApplicationSettingsService>(sp => new ApplicationSettingsService());
            services.AddSingleton<IDispatcherService>(sp => new DispatcherService());

            // Infrastructure services
            services.AddSingleton<INewsBlurClient>(sp => new NewsBlurClient());
            services.AddSingleton<ITranslationService>(sp => new TranslationService());
            services.AddSingleton<ISpectroNavigationService>(sp => new SpectroNavigationService());
            services.AddSingleton<IApplicationInformationService>(sp => new ApplicationInformationService());

            // Services with dependencies
            services.AddSingleton<IProgressService>(sp =>
                new ProgressService(sp.GetRequiredService<IDispatcherService>()));
            services.AddSingleton<IDataCacheService>(sp =>
                new RealmDataCacheService(sp.GetRequiredService<IDispatcherService>()));
            services.AddSingleton<IAuthenticationService>(sp =>
                new AuthenticationService(
                    sp.GetRequiredService<INewsBlurClient>(),
                    sp.GetRequiredService<IApplicationSettingsService>()));
            services.AddSingleton<IThemeService>(sp =>
                new ThemeService(sp.GetRequiredService<IApplicationSettingsService>()));
            services.AddSingleton<ISynchronizer>(sp =>
                new Synchronizer(
                    sp.GetRequiredService<INewsBlurClient>(),
                    sp.GetRequiredService<IProgressService>(),
                    sp.GetRequiredService<IAuthenticationService>(),
                    sp.GetRequiredService<IDataCacheService>()));
            services.AddSingleton<IActivationService>(sp =>
                new ActivationService(
                    sp.GetRequiredService<ISpectroNavigationService>(),
                    sp.GetRequiredService<IDataCacheService>(),
                    sp.GetRequiredService<IAuthenticationService>(),
                    sp.GetRequiredService<IThemeService>()));

            // ViewModels
            services.AddSingleton(sp =>
                new NavigationRootViewModel(
                    sp.GetRequiredService<ITranslationService>(),
                    sp.GetRequiredService<ISpectroNavigationService>(),
                    sp.GetRequiredService<IAuthenticationService>(),
                    sp.GetRequiredService<ISynchronizer>(),
                    sp.GetRequiredService<IProgressService>(),
                    sp.GetRequiredService<IApplicationInformationService>()));
            services.AddSingleton(sp => new ProfileViewModel());
            services.AddSingleton(sp =>
                new SettingsViewModel(
                    sp.GetRequiredService<IThemeService>(),
                    sp.GetRequiredService<IApplicationInformationService>()));
            services.AddSingleton(sp =>
                new NewsFeedListViewModel(
                    sp.GetRequiredService<IDataCacheService>()));
            services.AddSingleton(sp =>
                new LoginViewModel(
                    sp.GetRequiredService<IAuthenticationService>(),
                    sp.GetRequiredService<ISpectroNavigationService>()));

            ServiceProvider = services.BuildServiceProvider();
        }

        public SettingsViewModel SettingsViewModel => ServiceProvider.GetRequiredService<SettingsViewModel>();

        public NavigationRootViewModel NavViewModel => ServiceProvider.GetRequiredService<NavigationRootViewModel>();

        public NewsFeedListViewModel NewsList => ServiceProvider.GetRequiredService<NewsFeedListViewModel>();

        public ProfileViewModel Profile => ServiceProvider.GetRequiredService<ProfileViewModel>();

        public LoginViewModel Login => ServiceProvider.GetRequiredService<LoginViewModel>();
    }
}
