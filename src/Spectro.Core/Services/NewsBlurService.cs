using NewsBlurSharp;
using Realms;
using Spectro.Core.DataModel;
using Spectro.Core.Interfaces;
using Spectro.DataModel;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Spectro.Core.Services
{
    public class NewsBlurService : INewsBlurService
    {
        private readonly ITranslationService _translationService;
        private readonly INewsBlurClient _newsBlurClient;
        private readonly ISynchronizer _synchronizer;
        private ICredentialsPrompt _prompt;

        public NewsBlurService(
            ITranslationService translationService,
            INewsBlurClient newsBlurClient,
            ISynchronizer synchronizer)
        {
            _translationService = translationService;
            _newsBlurClient = newsBlurClient;
            _synchronizer = synchronizer;
            var session = GetSession(DataModelManager.RealmInstance);
        }

        private void StartSync(Session session)
        {
            if (session.IsLoggedIn)
            {
                _newsBlurClient.SetCookieSessionId(session.AuthCookieToken);

                //TODO need initializer pattern from BuildCast
                var ignore = _synchronizer.StartSync();
            }
        }

        public Session CurrentSession
        {
            get
            {
                Session session = GetSession(DataModelManager.RealmInstance);
                return session;
            }
        }

        public async Task Login()
        {
            bool credentialsProvided = await _prompt.PromptCredentials();

            if (credentialsProvided)
            {
                var details = _prompt.GetUsernamePassword();

                if (string.IsNullOrEmpty(details.Username) || string.IsNullOrEmpty(details.Username))
                {
                    await _prompt.ShowError(_translationService.GetString("Login_EmptyUNPW"));
                    return;
                }

                _prompt.ShowProgress();

                var result = await _newsBlurClient.LoginAsync(details.Username, details.Password);

                if (result.IsSuccess)
                {
                    var profile = await _newsBlurClient.GetUserProfileAsync();
                    var instance = DataModelManager.RealmInstance;
                    var currentSession = GetSession(instance);
                    var trans = instance.BeginWrite();
                    currentSession.AuthCookieToken = result.AuthCookieToken;
                    currentSession.CurrentUserId = result.UserId;
                    currentSession.UserName = profile.UserProfile.Username;
                    currentSession.PhotoUrl = profile.UserProfile.PhotoUrl;
                    trans.Commit();
                    var ignore = _synchronizer.StartSync();
                    
                } else
                {
                    _prompt.HideProgress();
                    await _prompt.ShowError(_translationService.GetString("Login_Failed"));
                }
            }
        }

        public async Task Logout()
        {
            try
            {
                await _newsBlurClient.LogoutAsync();
            }
            catch (ArgumentNullException) { }

            var instance = DataModelManager.RealmInstance;
            var trans = instance.BeginWrite();
            var currentSession = GetSession(instance);
            currentSession.AuthCookieToken = string.Empty;
            currentSession.CurrentUserId = 0;
            trans.Commit();
        }

        private static Session GetSession(Realm instance)
        {
            Session session = instance.All<Session>().FirstOrDefault();
            if (session == null)
            {
                var trans = DataModelManager.RealmInstance.BeginWrite();
                session = new Session();
                instance.Add(session);
                trans.Commit();
            }

            return session;
        }

        public void RegisterCredentialPrompt(ICredentialsPrompt prompt)
        {
            _prompt = prompt;
            StartSync(CurrentSession);
        }
    }
}
