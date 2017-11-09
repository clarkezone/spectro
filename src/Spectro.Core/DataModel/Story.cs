using Realms;
using Spectro.DataModel;

namespace Spectro.Core.DataModel
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
        public string Author { get; internal set; }
        public string TimeStamp { get; internal set; }
        public string ListImage { get; internal set; }
        public string Summary { get; internal set; }
    }
}
