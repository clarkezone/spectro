using System;
using System.Reflection;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Helpers;

namespace Spectro.Core.Commands
{
    public class AsyncRelayCommand<T> : IAsyncCommand
    {
        private readonly WeakFunc<T, Task> _asyncExecute;
        private readonly WeakFunc<T, bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public AsyncRelayCommand(Func<T, Task> asyncExecute)
            : this(asyncExecute, null)
        {
        }

        public AsyncRelayCommand(Func<T, Task> asyncExecute, Func<T, bool> canExecute)
        {
            _asyncExecute = new WeakFunc<T, Task>(asyncExecute);

            if (canExecute != null)
                _canExecute = new WeakFunc<T, bool>(canExecute);
        }

        public bool CanExecute(object parameter)
        {
            if (_canExecute == null)
                return true;

            if (_canExecute.IsStatic || _canExecute.IsAlive)
            {
                if (parameter == null && typeof(T).GetTypeInfo().IsValueType)
                    return _canExecute.Execute(default(T));

                if (parameter == null || parameter is T)
                    return _canExecute.Execute((T)parameter);
            }

            return false;
        }

        public async void Execute(object parameter) => await ExecuteAsync(parameter);

        public async Task ExecuteAsync(object parameter)
        {
            var val = parameter;

            if (!CanExecute(val) || _asyncExecute == null || (!_asyncExecute.IsStatic && !_asyncExecute.IsAlive)) return;

            try
            {
                if (val == null)
                {
                    if (typeof(T).GetTypeInfo().IsValueType)
                        await _asyncExecute.Execute(default(T));
                    else
                    {
                        // ReSharper disable ExpressionIsAlwaysNull
                        await _asyncExecute.Execute((T) val);
                        // ReSharper restore ExpressionIsAlwaysNull
                    }
                }
                else
                    await _asyncExecute.Execute((T) val);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
