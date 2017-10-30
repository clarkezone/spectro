using Realms;
using System;


namespace Spectro.DataModel
{
    public class NewsFeed : RealmObject
    {
        [PrimaryKey]
        public string UriKey { get; set; }

        public int Id { get; set; }

        public string Title { get; set; }

        public string IconUri { get; set; }
    }
}
