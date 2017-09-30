using Realms;
using Spectro.DataModel;
using System.Linq;

namespace Spectro.Core.DataModel
{
    public class DataRoot
    {
        public static IQueryable<NewsFeed> GetAllStuff(Realm instance)
        {
            var query = instance.All<NewsFeed>().OrderBy(ob => ob.Title);
            return query;
        }
    }
}
