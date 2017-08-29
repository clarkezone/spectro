using Realms;
using System;


namespace Spectro.Models
{
    public class NewsFeed : RealmObject
    {
        [PrimaryKey]
        public string UriKey { get; set; }

        public string Title { get; set; }

        public string Descrtiption { get; set; }

    }
}
