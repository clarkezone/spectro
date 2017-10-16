using System.Diagnostics;

namespace Spectro.ViewModels
{
    public class NavigationRootViewModel 
    {
        //private RelayCommand<string> _navigateCommand;

        public NavigationRootViewModel()
        {
        }

        //public RelayCommand<string> NavigateCommand
        //{
        //    get
        //    {
        //        return _navigateCommand
        //               ?? (_navigateCommand = new RelayCommand<string>(
        //                   p => {
        //                       Debug.WriteLine("Hit");
        //                      // ServiceLocator.Current.GetInstance<INavigationService>("NavigationService");
        //                   },
        //                   p => true));

        //        //ServiceLocator.Current.GetInstance<NavigationServiceEx>().Frame.

        //        //return _navigateCommand
        //        //       ?? (_navigateCommand = new RelayCommand<string>(
        //        //           p => _navigationService.NavigateTo(ViewModelLocator.SecondPageKey, p),
        //        //           p => !string.IsNullOrEmpty(p)));
        //    }
        //}

       
    }
}