using System;

namespace Spectro.Core.Interfaces
{
    public interface IProgressService
    {
        event EventHandler ProgressStatusChanged;
        void ShowProgress();
        void HideProgress();
        bool ProgressIsVisible { get; }
    }
}
