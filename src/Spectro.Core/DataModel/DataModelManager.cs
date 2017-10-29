using Realms;
using System;
using System.Threading;

namespace Spectro.DataModel
{
    public  class DataModelManager
    {
        private static string REALMDBNAME = "";
        private static ThreadLocal<Realm> instance;

        public static void Configure(string instanceName)
        {
            REALMDBNAME = instanceName;
        }

        public static Realm RealmInstance
        {
            get
            {
                if (string.IsNullOrEmpty(REALMDBNAME))
                {
                    throw new InvalidOperationException("No RealmName specified");
                }

                if (instance == null)
                {
                    instance = new ThreadLocal<Realm>();
                }

                if (instance.Value == null)
                {
                    instance.Value = Realm.GetInstance(REALMDBNAME);
                }

                return instance.Value;
            }
        }
    }
}
