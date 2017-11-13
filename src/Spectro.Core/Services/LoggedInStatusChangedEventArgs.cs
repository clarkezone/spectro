using System;
using NewsBlurSharp.Model.Response;

namespace Spectro.Core.Services
{
    public class LoggedInStatusChangedEventArgs : EventArgs
    {
        public bool IsLoggedIn { get; }
        public UserProfile LoggedInUser { get; }

        public LoggedInStatusChangedEventArgs(bool isLoggedIn, UserProfile loggedInUser)
        {
            IsLoggedIn = isLoggedIn;
            LoggedInUser = loggedInUser;
        }
    }
}