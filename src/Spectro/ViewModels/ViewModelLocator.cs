using GalaSoft.MvvmLight.Ioc;

using Microsoft.Practices.ServiceLocation;
using Spectro.Models.UWP;
using Spectro.Services;
using Spectro.Views;
using System.Collections;

namespace Spectro.ViewModels
{
    public class ViewModelLocator
    {
        NavigationServiceEx _navigationService = new NavigationServiceEx();

        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            SimpleIoc.Default.Register(() => _navigationService);
            Register<MainViewModel, MainPage>();

            //TODO: is there a better place to put this as it is a data not a navigation thing?  Eg where the default template initializes it's datasource?
            SimpleIoc.Default.Register<RealmAllNewsFeedsSource>();

            //Creating a new NewsFeedListViewModel blows up here.. that needs figuring out before we can use DI successfully
            //SimpleIoc.Default.Register(() => new NewsFeedListViewModel(SimpleIoc.Default.GetInstance<RealmAllNewsFeedsSource> as IList));
            SimpleIoc.Default.Register<NewsFeedListViewModel>();
            _navigationService.Configure(typeof(NewsFeedListViewModel).FullName, typeof(NewsFeedList));

            Register<NavigationRootViewModel, NavigationRoot>();
            Register<ProfileViewModel, ProfilePage>();
            Register<SettingsViewModel, SettingsPage>();
        }

        public SettingsViewModel SettingsViewModel => ServiceLocator.Current.GetInstance<SettingsViewModel>();

        public NavigationRootViewModel NavViewModel => ServiceLocator.Current.GetInstance<NavigationRootViewModel>();

        public NewsFeedListViewModel NewsList => ServiceLocator.Current.GetInstance<NewsFeedListViewModel>();

        public ProfileViewModel ProfileVM => ServiceLocator.Current.GetInstance<ProfileViewModel>();

        public void Register<VM, V>() where VM : class
        {
            SimpleIoc.Default.Register<VM>();
            _navigationService.Configure(typeof(VM).FullName, typeof(V));
        }
    }
}
