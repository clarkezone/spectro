using System.Threading.Tasks;

namespace Spectro.Core.Interfaces
{
    public interface IHandleNavigatedFrom
    {
        Task OnNavigatedFromAsync(NavigationServiceNavigationEventArgs eventArgs);
    }

    public interface IHandleNavigatedTo
    {
        Task OnNavigatedToAsync(NavigationServiceNavigationEventArgs eventArgs);
    }
}
