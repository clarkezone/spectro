using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace Cimbalino.Toolkit.Services
{
    public class DispatcherService : IDispatcherService
    {
        public Task InvokeOnUiThreadAsync(Action action) => InvokeOnUiThreadAsync(action, false);

        public async Task InvokeOnUiThreadAsync(Action action, bool force)
        {
            var dispatcher = CoreApplication.MainView?.CoreWindow?.Dispatcher;
            if (dispatcher == null || (!force && dispatcher.HasThreadAccess))
            {
                action();
                return;
            }

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
        }

        public Task<T> InvokeOnUiThreadAsync<T>(Func<T> function) => InvokeOnUiThreadAsync(function, false);

        public async Task<T> InvokeOnUiThreadAsync<T>(Func<T> function, bool force)
        {
            T result = default;
            await InvokeOnUiThreadAsync(() => result = function(), force);
            return result;
        }

        public Task InvokeOnUiThreadAsync(Func<Task> asyncAction) => InvokeOnUiThreadAsync(asyncAction, false);

        public async Task InvokeOnUiThreadAsync(Func<Task> asyncAction, bool force)
        {
            var dispatcher = CoreApplication.MainView?.CoreWindow?.Dispatcher;
            if (dispatcher == null || (!force && dispatcher.HasThreadAccess))
            {
                await asyncAction();
                return;
            }

            var tcs = new TaskCompletionSource<bool>();
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    await asyncAction();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            await tcs.Task;
        }

        public Task<T> InvokeOnUiThreadAsync<T>(Func<Task<T>> asyncFunction) => InvokeOnUiThreadAsync(asyncFunction, false);

        public async Task<T> InvokeOnUiThreadAsync<T>(Func<Task<T>> asyncFunction, bool force)
        {
            T result = default;
            await InvokeOnUiThreadAsync(async () => result = await asyncFunction(), force);
            return result;
        }
    }
}
