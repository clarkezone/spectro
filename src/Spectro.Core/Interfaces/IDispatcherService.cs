using System;
using System.Threading.Tasks;

namespace Spectro.Core.Interfaces
{
    public interface IDispatcherService
    {
        Task InvokeOnUiThreadAsync(Action action);
        Task InvokeOnUiThreadAsync(Action action, bool force);
        Task<T> InvokeOnUiThreadAsync<T>(Func<T> function);
        Task<T> InvokeOnUiThreadAsync<T>(Func<T> function, bool force);
        Task InvokeOnUiThreadAsync(Func<Task> asyncAction);
        Task InvokeOnUiThreadAsync(Func<Task> asyncAction, bool force);
        Task<T> InvokeOnUiThreadAsync<T>(Func<Task<T>> asyncFunction);
        Task<T> InvokeOnUiThreadAsync<T>(Func<Task<T>> asyncFunction, bool force);
    }
}
