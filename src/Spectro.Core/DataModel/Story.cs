using Realms;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spectro.DataModel
{
    public class Story: RealmObject
    {
        [PrimaryKey]
        public string Id { get; set; }

        public string Title { get; set; }
        public int FeedId { get; internal set; }
        public NewsFeed Feed { get; internal set; }
        public int ReadStatus { get; internal set; }
        public string Content { get; internal set; }
    }
}
