using Realms;

namespace Spectro.Core.DataModel
{
    public class Session : RealmObject
    {
        public int CurrentUserId
        {
            get => _currentUserId;
            set
            {
                _currentUserId = value;
                OnPropertyChanged(nameof(IsLoggedIn));
            }
        }

        private int _currentUserId;

        public string AuthCookieToken { get; set; }

        public string UserName { get; set; }

        public string PhotoUrl { get; set; }

        public bool IsLoggedIn => _currentUserId != 0;
    }
}
