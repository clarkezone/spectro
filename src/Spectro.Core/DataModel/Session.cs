using Realms;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spectro.Core.DataModel
{
    public class Session : RealmObject
    {
        public int CurrentUserId
        {
            get { return CurrentUserId_; }

            set { CurrentUserId_ = value;
                OnPropertyChanged(nameof(IsLoggedIn));
            }
        }

        public int CurrentUserId_ { get; set; }

        public string AuthCookieToken { get; set; }

        public bool IsLoggedIn
        {
            get
            {
                return CurrentUserId_ != 0;
            }
        }
    }
}
