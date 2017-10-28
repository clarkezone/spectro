using System;
using System.Collections.Generic;
using System.Text;

namespace Spectro.Core.Interfaces
{
    public interface ILoginService
    {
        bool IsLoggedIn();

        void Login();

        void Logout();
    }
}
