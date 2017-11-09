using System;
using System.Threading.Tasks;
using NewsBlurSharp.Model.Response;
using Spectro.Core.Services;

namespace Spectro.Core.Interfaces
{
    public interface IAuthenticationService
    {
        event EventHandler<LoggedInStatusChangedEventArgs> LoggedInStatusChanged; 
        bool IsLoggedIn { get; }
        UserProfile LoggedInUser { get; }
        Task<bool> Login(string username, string password);
        Task Logout();
    }
}