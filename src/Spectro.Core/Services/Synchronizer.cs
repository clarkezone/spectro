using NewsBlurSharp;
using System.Threading.Tasks;

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
            _api = api;
            _service = parent;
        }

        public async Task StartSync()
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

            await Task.Run(async () => {
                await Task.Delay(5000);

                var results = await _api.GetFeedsAsync(true);

                //foreach (var item in results)
                //{

                //}
                lock (_syncLock)
                {
                    _isSynchronizing = false;
                }
            });
        }
    }
}
