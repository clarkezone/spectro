using Realms;
using System;
using System.Threading;

namespace Spectro.DataModel
{
    public  class DataModelManager
    {
        private static string _realmDbName = "";
        private static ThreadLocal<Realm> _instance;

        public static void Configure(string instanceName)
        {
            _realmDbName = instanceName;
        }

        public static Realm RealmInstance
        {
            get
            {
                if (string.IsNullOrEmpty(_realmDbName))
                {
                    throw new InvalidOperationException("No RealmName specified");
                }

                if (_instance == null)
                {
                    _instance = new ThreadLocal<Realm>();
                }

                if (_instance.Value == null)
                {
                    _instance.Value = Realm.GetInstance(_realmDbName);
                }

                return _instance.Value;
            }
        }
    }
}
