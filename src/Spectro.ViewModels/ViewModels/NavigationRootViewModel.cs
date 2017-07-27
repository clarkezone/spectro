using GalaSoft.MvvmLight;
using System.Collections.ObjectModel;
using System;

namespace Spectro.ViewModels
{
    public class NavigationRootViewModel : ViewModelBase
    {
        private ObservableCollection<object> _menuItems = new ObservableCollection<object>();

        public NavigationRootViewModel()
        {
            BuildMenuItems();
        }

        public ObservableCollection<object> NavigationMenuItems
        {
            get => _menuItems;
        }

        private void BuildMenuItems()
        {
            _menuItems.Add("One");
            _menuItems.Add("Two");
        }
    }
}