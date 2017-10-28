using Spectro.Core.Interfaces;

namespace Spectro.Services
{
    class LoginLogoutService : ILoginService
    {
        private bool loggedIn;

        public bool IsLoggedIn()
        {
            return loggedIn;
        }

        public void Login()
        {
            loggedIn = true;
        }

        public void Logout()
        {
            loggedIn = false;
        }
    }
}
