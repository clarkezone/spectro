using NewsBlurSharp;
using Spectro.Core.Interfaces;
using Spectro.DataModel;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Spectro.Core.Services
{
    class Synchronizer
    {
        private NewsBlurClient _api;
        private bool _isSynchronizing;
        private NewsBlurService _service;
        private object _syncLock = new object();

        public Synchronizer(NewsBlurClient api, NewsBlurService parent)
        {
            //TODO: this should be an interface
            _api = api;
            _service = parent;
        }

        public async Task StartSync(ICredentialsPrompt prompt)
        {
            //TODO: bail if not logged in
            //TODO: show progress dots
            lock (_syncLock)
            {
                if (_isSynchronizing)
                {
                    return;
                }

                _isSynchronizing = true;
            }
            prompt?.ShowProgress();
            await Task.Run(async () => {
                await Task.Delay(1000);

                var results = await _api.GetFeedsAsync(true);

                var trans = DataModelManager.RealmInstance.BeginWrite();

                foreach (var item in results.feeds.FeedItems)
                {
                    //TODO: dependency inject the realmness
                    var exists = DataModelManager.RealmInstance.All<NewsFeed>().Where(fe => fe.Id == item.id).FirstOrDefault();
                    if (exists == null)
                    {
                        NewsFeed nf = new NewsFeed() { Id = item.id,
                            UriKey = item.properties.feed_address,
                            Title = item.properties.feed_title, IconUri = item.properties.favicon_url };
                        DataModelManager.RealmInstance.Add(nf);
                    }
                }

                trans.Commit();

                lock (_syncLock)
                {
                    _isSynchronizing = false;
                }

                //TODO: dispatcherhelp stop progress
            });
        }

        internal void RegisterCredentialPrompt(ICredentialsPrompt prompt)
        {
            lock (_syncLock)
            {
                if (_isSynchronizing)
                {
                    prompt.ShowProgress();
                }
            }
        }
    }
}
