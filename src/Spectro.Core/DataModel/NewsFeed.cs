using Realms;
using System;


namespace Spectro.DataModel
{
    public partial class NewsFeed : RealmObject
    {
        public string FeedUri { get; set; }

        [PrimaryKey]
        public int Id { get; set; }

        public int FeedId { get; set; }

        public string Title { get; set; }

        public string IconUri { get; set; }

        public int UnreadCount { get; set; }

        public DateTimeOffset LastStoryDateFromService { get; set; }

        public DateTimeOffset DownloadedLastStoryDate { get; set; }

        public bool Active { get; internal set; }
    }
}
