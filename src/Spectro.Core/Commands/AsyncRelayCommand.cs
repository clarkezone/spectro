using System;
using System.Threading.Tasks;

namespace Spectro.Core.Commands
{
    public class AsyncRelayCommand : IAsyncCommand
    {
        private readonly Func<Task> _asyncExecute;
        private readonly Func<bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public AsyncRelayCommand(Func<Task> asyncExecute)
            : this(asyncExecute, null)
        {
        }

        public AsyncRelayCommand(Func<Task> asyncExecute, Func<bool> canExecute)
        {
            _asyncExecute = asyncExecute ?? throw new ArgumentNullException(nameof(asyncExecute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public async void Execute(object parameter) => await ExecuteAsync(parameter);

        public async Task ExecuteAsync(object parameter)
        {
            if (!CanExecute(parameter) || _asyncExecute == null) return;

            try
            {
                await _asyncExecute();
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
