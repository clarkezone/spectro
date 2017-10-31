using Realms;
using Spectro.Core.Services;
using Spectro.DataModel;
using Spectro.Helpers;
using Spectro.Services;
using Spectro.Views;
using System;
using System.Linq;

namespace Spectro.ViewModels
{
    public class ViewModelLocator
    {
        const string REALMNAME = "NewsBlurStore";
        SettingsViewModel _settingsviewmodel;
        NavigationRootViewModel _navigationRootViewModel;
        NewsFeedListViewModel _newsFeedListViewmodel;
        ProfileViewModel _profileViewModel;

        public ViewModelLocator()
        {
            Singleton<NavigationServiceEx>.Instance.Configure(typeof(NewsFeedListViewModel).FullName, typeof(NewsFeedList));

            Register<NavigationRootViewModel, NavigationRoot>();
            Register<ProfileViewModel, ProfilePage>();
            Register<SettingsViewModel, SettingsPage>();
        }

        public SettingsViewModel SettingsViewModel
        {
            get
            {
                if (_settingsviewmodel == null)
                {
                    _settingsviewmodel = new SettingsViewModel();

                    _profileViewModel = new ProfileViewModel();
                }
                return _settingsviewmodel;
            }
        }

        public NavigationRootViewModel NavViewModel {

            get
            {
                if (_navigationRootViewModel == null)
                {
                    Func<string, string> resourceLookup = (what) => { return ResourceExtensions.GetLocalized(what); };
                    _navigationRootViewModel = new NavigationRootViewModel(new NewsBlurService(REALMNAME, resourceLookup), resourceLookup);
                }
                return _navigationRootViewModel;
            }
        }

        public NewsFeedListViewModel NewsList
        {
            get
            {
                if (_newsFeedListViewmodel == null)
                {
                    _newsFeedListViewmodel = new NewsFeedListViewModel(
                        DataModelManager.RealmInstance.All<NewsFeed>().Where(it=>it.UnreadCount > 0).OrderBy(ob=>ob.Title).AsRealmCollection());
                }
                return _newsFeedListViewmodel;
            }
        }

        public ProfileViewModel ProfileVM => _profileViewModel;

        private void Register<VM, V>() where VM : class
        {
            //SimpleIoc.Default.Register<VM>();
            Singleton<NavigationServiceEx>.Instance.Configure(typeof(VM).FullName, typeof(V));
        }
    }
}
