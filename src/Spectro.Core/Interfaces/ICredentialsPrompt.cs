using System.Threading.Tasks;

namespace Spectro.Core.Interfaces
{
    public interface ICredentialsPrompt
    {
        Task<bool> PromptCredentials();
        void ShowProgress();
        void HideProgress();
        Task ShowError(string v);
        (string, string) GetUsernamePassword();
        bool HaveNetwork();
    }
}
