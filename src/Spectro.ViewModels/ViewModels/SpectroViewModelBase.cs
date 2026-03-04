using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Spectro.Core.Interfaces;

namespace Spectro.ViewModels
{
    public abstract class SpectroViewModelBase : ObservableObject, IHandleNavigatedFrom, IHandleNavigatedTo
    {
        public virtual Task OnNavigatedFromAsync(NavigationServiceNavigationEventArgs eventArgs)
            => Task.CompletedTask;

        public virtual Task OnNavigatedToAsync(NavigationServiceNavigationEventArgs eventArgs)
            => Task.CompletedTask;
    }
}
