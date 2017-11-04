using System.Threading;
using System.Threading.Tasks;
using Realms;
using Spectro.DataModel;

namespace Spectro.Core.Interfaces
{
    public interface IDataCacheService
    {
        Task Startup();
    }

    public class DataCacheService : IDataCacheService
    {
        private const string RealmName = "NewsBlurStore";

        private ThreadLocal<Realm> _instance;

        private Realm Instance => _instance.Value;

        public Task Startup()
        {
            _instance = new ThreadLocal<Realm> { Value = Realm.GetInstance(RealmName) };
            DataModelManager.Configure(RealmName);

            return Task.CompletedTask;
        }
    }
}