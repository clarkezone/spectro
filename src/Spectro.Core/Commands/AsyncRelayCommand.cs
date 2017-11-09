using System;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Helpers;

namespace Spectro.Core.Commands
{
    public class AsyncRelayCommand : IAsyncCommand
    {
        private readonly WeakFunc<Task> _asyncExecute;
        private readonly WeakFunc<bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public AsyncRelayCommand(Func<Task> asyncExecute)
            : this(asyncExecute, null)
        {
        }

        public AsyncRelayCommand(Func<Task> asyncExecute, Func<bool> canExecute)
        {
            _asyncExecute = new WeakFunc<Task>(asyncExecute);

            if (canExecute != null)
                _canExecute = new WeakFunc<bool>(canExecute);
        }

        public bool CanExecute(object parameter) => _canExecute == null || (_canExecute.IsStatic || _canExecute.IsAlive) && _canExecute.Execute();

        public async void Execute(object parameter) => await ExecuteAsync(parameter);

        public async Task ExecuteAsync(object parameter)
        {
            if (!CanExecute(parameter) || _asyncExecute == null || (!_asyncExecute.IsStatic && !_asyncExecute.IsAlive)) return;

            try
            {
                await _asyncExecute.Execute();
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
