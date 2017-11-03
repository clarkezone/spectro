using Spectro.Core.Services;
using Spectro.Helpers;
using Spectro.Services;
using Spectro.Views;
using GalaSoft.MvvmLight.Ioc;
using NewsBlurSharp;
using Spectro.Core.Interfaces;

namespace Spectro.ViewModels
{
    public class ViewModelLocator
    {
        public ViewModelLocator()
        {
            Singleton<NavigationServiceEx>.Instance.Configure(typeof(NewsFeedListViewModel).FullName, typeof(NewsFeedList));

            SimpleIoc.Default.Register<INewsBlurClient>(() => new NewsBlurClient());
            SimpleIoc.Default.Register<ISynchronizer, Synchronizer>();
            SimpleIoc.Default.Register<ITranslationService, TranslationService>();
            SimpleIoc.Default.Register<INewsBlurService, NewsBlurService>();

            SimpleIoc.Default.Register<NavigationRootViewModel>();
            SimpleIoc.Default.Register<ProfileViewModel>();
            SimpleIoc.Default.Register<SettingsViewModel>();
            SimpleIoc.Default.Register<NewsFeedListViewModel>();

            Register<NavigationRootViewModel, NavigationRoot>();
            Register<ProfileViewModel, ProfilePage>();
            Register<SettingsViewModel, SettingsPage>();
        }

        public SettingsViewModel SettingsViewModel => SimpleIoc.Default.GetInstance<SettingsViewModel>();

        public NavigationRootViewModel NavViewModel => SimpleIoc.Default.GetInstance<NavigationRootViewModel>();

        public NewsFeedListViewModel NewsList => SimpleIoc.Default.GetInstance<NewsFeedListViewModel>();

        public ProfileViewModel Profile => SimpleIoc.Default.GetInstance<ProfileViewModel>();

        private void Register<VM, V>() where VM : class
        {
            //SimpleIoc.Default.Register<VM>();
            Singleton<NavigationServiceEx>.Instance.Configure(typeof(VM).FullName, typeof(V));
        }
    }
}
