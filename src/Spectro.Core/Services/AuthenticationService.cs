using System;
using System.Threading.Tasks;
using Cimbalino.Toolkit.Services;
using NewsBlurSharp;
using NewsBlurSharp.Model.Response;
using Newtonsoft.Json;
using Spectro.Core.Interfaces;

namespace Spectro.Core.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private const string UserProfileKey = "UserProfileKey";
        private const string AuthenticationDetailsKey = "AuthenticationDetailsKey";

        private readonly INewsBlurClient _newsBlurClient;
        private readonly IApplicationSettingsService _applicationSettingsService;
        public event EventHandler<LoggedInStatusChangedEventArgs> LoggedInStatusChanged;

        public AuthenticationService(
            INewsBlurClient newsBlurClient,
            IApplicationSettingsService applicationSettingsService)
        {
            _newsBlurClient = newsBlurClient;
            _applicationSettingsService = applicationSettingsService;

            GetAuthenticationDetails();
            GetUserProfile();
        }

        public bool IsLoggedIn => LoggedInUser != null;
        public UserProfile LoggedInUser { get; private set; }
        public async Task<bool> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            try
            {
                var result = await _newsBlurClient.LoginAsync(username, password);
                if (result.IsSuccess)
                {
                    var profileResponse = await _newsBlurClient.GetUserProfileAsync();
                    SetUserProfile(profileResponse.UserProfile);
                    SetAuthenticationDetails(result.AuthCookieToken);
                }

                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task Logout()
        {
            try
            {
                await _newsBlurClient.LogoutAsync();
            }
            catch (Exception) { }

            SetUserProfile(null);
            SetAuthenticationDetails(null);
        }

        private void SetUserProfile(UserProfile userProfile)
        {
            LoggedInUser = userProfile;
            LoggedInStatusChanged?.Invoke(this, new LoggedInStatusChangedEventArgs(IsLoggedIn, LoggedInUser));

            if (userProfile == null)
            {
                _applicationSettingsService.Roaming.Remove(UserProfileKey);
            }
            else
            {
                var json = JsonConvert.SerializeObject(userProfile);
                _applicationSettingsService.Roaming.Set(UserProfileKey, json);
            }
        }

        private void GetUserProfile()
        {
            var json = _applicationSettingsService.Roaming.Get(UserProfileKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                var profile = JsonConvert.DeserializeObject<UserProfile>(json);
                SetUserProfile(profile);
            }
        }

        private void SetAuthenticationDetails(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _applicationSettingsService.Roaming.Remove(AuthenticationDetailsKey);
            }
            else
            {
                // TODO: Should probably do some encryption here really
                _applicationSettingsService.Roaming.Set(AuthenticationDetailsKey, token);
            }
        }

        private void GetAuthenticationDetails()
        {
            var token = _applicationSettingsService.Roaming.Get(AuthenticationDetailsKey, string.Empty);
            _newsBlurClient.SetCookieSessionId(token);
        }
    }
}