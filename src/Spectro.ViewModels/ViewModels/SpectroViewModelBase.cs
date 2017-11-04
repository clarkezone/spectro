using System.Threading.Tasks;
using Cimbalino.Toolkit.Handlers;
using Cimbalino.Toolkit.Services;
using GalaSoft.MvvmLight;

namespace Spectro.ViewModels
{
    public abstract class SpectroViewModelBase : ViewModelBase, IHandleNavigatedFrom, IHandleNavigatedTo
    {
        public virtual Task OnNavigatedFromAsync(NavigationServiceNavigationEventArgs eventArgs)
            => Task.CompletedTask;

        public virtual Task OnNavigatedToAsync(NavigationServiceNavigationEventArgs eventArgs)
            => Task.CompletedTask;
    }
}
