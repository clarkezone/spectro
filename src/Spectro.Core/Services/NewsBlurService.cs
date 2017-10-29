﻿using NewsBlurSharp;
using Realms;
using Spectro.Core.DataModel;
using Spectro.Core.Interfaces;
using Spectro.DataModel;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Spectro.Core.Services
{
    public class NewsBlurService : INewsBlurService
    {
        private NewsBlurClient _api = new NewsBlurClient();
        Func<string, string> _getResource;

        public NewsBlurService(string RealmName, Func<string, string> getResource)
        {
            DataModelManager.Configure(RealmName);
            _getResource = getResource;
            GetSession(DataModelManager.RealmInstance);
        }

        public Session CurrentSession
        {
            get
            {
                Session session = GetSession(DataModelManager.RealmInstance);
                return session;
            }
        }

        public async Task Login(ICredentialsPrompt prompt)
        {
            bool credentialsProvided = await prompt.PromptCredentials();

            if (credentialsProvided)
            {
                var details = prompt.GetUsernamePassword();

                if (string.IsNullOrEmpty(details.Item1) || string.IsNullOrEmpty(details.Item1))
                {
                    await prompt.ShowError(_getResource("Login_EmptyUNPW"));
                    return;
                }

                prompt.ShowProgress();

                var result = await _api.LoginAsync(details.Item1, details.Item2);

                if (result.IsSuccess)
                {
                    var profile = await _api.GetUserProfileAsync();
                    var instance = DataModelManager.RealmInstance;
                    var currentSession = GetSession(instance);
                    var trans = instance.BeginWrite();
                    currentSession.AuthCookieToken = result.AuthCookieToken;
                    currentSession.CurrentUserId = result.UserId;
                    currentSession.UserName = profile.user_profile.username;
                    currentSession.PhotoUrl = profile.user_profile.photo_url;
                    trans.Commit();
                    prompt.HideProgress();
                } else
                {
                    prompt.HideProgress();
                    await prompt.ShowError(_getResource("Login_Failed"));
                }
            }
        }

        public async Task Logout(ICredentialsPrompt prompt)
        {
            try
            {
                await _api.LogoutAsync();
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
    }
}