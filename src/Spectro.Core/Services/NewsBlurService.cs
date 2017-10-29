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
        private NewsBlurClient _api = new NewsBlurClient();

        public NewsBlurService(string RealmName)
        {
            DataModelManager.Configure(RealmName);
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
            string username = "";
            string password = "";
            var result = await _api.LoginAsync(username, password);

            if (result.IsSuccess)
            {
                var instance = DataModelManager.RealmInstance;
                var trans = instance.BeginWrite();
                var currentSession = GetSession(instance);
                currentSession.AuthCookieToken = result.AuthCookieToken;
                currentSession.CurrentUserId = result.UserId;
            }
        }

        public async Task Logout()
        {
            await _api.LogoutAsync();

            var instance = DataModelManager.RealmInstance;
            var trans = instance.BeginWrite();
            var currentSession = GetSession(instance);
            currentSession.AuthCookieToken = string.Empty;
            currentSession.CurrentUserId = 0;
        }

        private static Session GetSession(Realm instance)
        {
            Session session = instance.All<Session>().FirstOrDefault();
            if (session == null)
            {
                var trans = DataModelManager.RealmInstance.BeginWrite();
                session = new Session();
                trans.Commit();
            }

            return session;
        }
    }
}
