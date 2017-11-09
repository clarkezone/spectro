using System.Threading.Tasks;
using System.Windows.Input;

namespace Spectro.Core.Commands
{
    public interface IAsyncCommand : ICommand
    {
        Task ExecuteAsync(object parameter);
    }
}