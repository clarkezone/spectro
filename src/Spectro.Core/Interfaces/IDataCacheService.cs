using System.Threading;
using Realms;
using Spectro.DataModel;

namespace Spectro.Core.Interfaces
{
    public interface IDataCacheService
    {
    }

    public class DataCacheService : IDataCacheService
    {
        private const string RealmName = "NewsBlurStore";

        private readonly ThreadLocal<Realm> _instance;

        private Realm Instance => _instance.Value;

        public DataCacheService()
        {
            _instance = new ThreadLocal<Realm> { Value = Realm.GetInstance(RealmName) };
            DataModelManager.Configure(RealmName);
        }
    }
}