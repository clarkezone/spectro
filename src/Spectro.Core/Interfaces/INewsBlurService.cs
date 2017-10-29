using Spectro.Core.DataModel;
using System.Threading.Tasks;

namespace Spectro.Core.Interfaces
{
    public interface INewsBlurService
    {
        Session CurrentSession { get; }

        Task Login(ICredentialsPrompt prompt);

        Task Logout(ICredentialsPrompt prompt);
    }
}
