using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using Microsoft.Practices.ServiceLocation;
using System;
using System.Diagnostics;

namespace Spectro.ViewModels
{
    public class NavigationRootViewModel : ViewModelBase
    {
        private RelayCommand<string> _navigateCommand;

        public NavigationRootViewModel()
        {
        }

        public RelayCommand<string> NavigateCommand
        {
            get
            {
                return _navigateCommand
                       ?? (_navigateCommand = new RelayCommand<string>(
                           p => {
                               Debug.WriteLine("Hit");
                              // ServiceLocator.Current.GetInstance<INavigationService>("NavigationService");
                           },
                           p => true));

                //ServiceLocator.Current.GetInstance<NavigationServiceEx>().Frame.

                //return _navigateCommand
                //       ?? (_navigateCommand = new RelayCommand<string>(
                //           p => _navigationService.NavigateTo(ViewModelLocator.SecondPageKey, p),
                //           p => !string.IsNullOrEmpty(p)));
            }
        }

       
    }
}