using GalaSoft.MvvmLight.Ioc;

using Microsoft.Practices.ServiceLocation;

using Spectro.Services;
using Spectro.Views;

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
            Register<NewsFeedListViewModel, NewsFeedList>();
            Register<NavigationRootViewModel, NavigationRoot>();
            Register<ProfileViewModel, ProfilePage>();
            Register<SettingsViewModel, SettingsPage>();
        }

        //public MainViewModel MainViewModel => ServiceLocator.Current.GetInstance<MainViewModel>();

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
