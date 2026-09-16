namespace Spectro.WinUI;

internal sealed class LoginSubmission
{
    private bool _running;

    public Task OnKeyDownAsync(
        bool isEnter, bool isLoginScreen, Action markHandled, Func<Task> submit)
    {
        if (!isEnter || !isLoginScreen) return Task.CompletedTask;
        markHandled();
        return submit();
    }

    public async Task RunAsync(bool canInteract, Func<Task> operation)
    {
        if (!canInteract || _running) return;
        _running = true;
        try { await operation(); }
        finally { _running = false; }
    }
}
