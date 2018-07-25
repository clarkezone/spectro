using System;
using Cimbalino.Toolkit.Services;
using Spectro.Core.Interfaces;

namespace Spectro.Core.Services
{
    public class ProgressService : IProgressService
    {
        private readonly IDispatcherService _dispatcherService;

        public ProgressService(IDispatcherService dispatcherService)
        {
            _dispatcherService = dispatcherService;
        }

        public bool ProgressIsVisible { get; private set; }

        public event EventHandler ProgressStatusChanged;

        public void HideProgress() => SetProgress(false);

        public void ShowProgress() => SetProgress(true);

        private void SetProgress(bool value)
        {
            ProgressIsVisible = value;
            _dispatcherService.InvokeOnUiThreadAsync(() => ProgressStatusChanged?.Invoke(this, EventArgs.Empty));
        }
    }
}