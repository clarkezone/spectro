using Realms;
using Spectro.Core.Services;
using Spectro.DataModel;
using Spectro.Helpers;
using Spectro.Services;
using Spectro.Views;
using System;
using System.Linq;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Practices.ServiceLocation;
using NewsBlurSharp;
using Spectro.Core.Interfaces;

namespace Spectro.ViewModels
{
    public class ViewModelLocator
    {
        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            Singleton<NavigationServiceEx>.Instance.Configure(typeof(NewsFeedListViewModel).FullName, typeof(NewsFeedList));

            SimpleIoc.Default.Register<INewsBlurClient>(() => new NewsBlurClient());
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

        public SettingsViewModel SettingsViewModel => ServiceLocator.Current.GetInstance<SettingsViewModel>();

        public NavigationRootViewModel NavViewModel => ServiceLocator.Current.GetInstance<NavigationRootViewModel>();

        public NewsFeedListViewModel NewsList => ServiceLocator.Current.GetInstance<NewsFeedListViewModel>();

        public ProfileViewModel Profile => ServiceLocator.Current.GetInstance<ProfileViewModel>();

        private void Register<VM, V>() where VM : class
        {
            //SimpleIoc.Default.Register<VM>();
            Singleton<NavigationServiceEx>.Instance.Configure(typeof(VM).FullName, typeof(V));
        }
    }
}
