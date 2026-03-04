using System;
using System.Threading.Tasks;

namespace Spectro.Core.Commands
{
    public class AsyncRelayCommand<T> : IAsyncCommand
    {
        private readonly Func<T, Task> _asyncExecute;
        private readonly Func<T, bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public AsyncRelayCommand(Func<T, Task> asyncExecute)
            : this(asyncExecute, null)
        {
        }

        public AsyncRelayCommand(Func<T, Task> asyncExecute, Func<T, bool> canExecute)
        {
            _asyncExecute = asyncExecute ?? throw new ArgumentNullException(nameof(asyncExecute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_canExecute == null)
                return true;

            if (parameter == null && typeof(T).IsValueType)
                return _canExecute(default(T));

            if (parameter == null || parameter is T)
                return _canExecute((T)parameter);

            return false;
        }

        public async void Execute(object parameter) => await ExecuteAsync(parameter);

        public async Task ExecuteAsync(object parameter)
        {
            if (!CanExecute(parameter) || _asyncExecute == null) return;

            try
            {
                var val = parameter;
                if (val == null)
                {
                    if (typeof(T).IsValueType)
                        await _asyncExecute(default(T));
                    else
                        await _asyncExecute((T)val);
                }
                else
                    await _asyncExecute((T)val);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
