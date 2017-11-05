using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Realms;
using Spectro.DataModel;

namespace Spectro.Core.Interfaces
{
    public interface IDataCacheService
    {
        Task Startup();
        void BeginWrite();
        void Commit();
        void AddFeed(NewsFeed item, bool isUpdate = false);
        Task<List<Story>> GetStories(Expression<Func<Story, bool>> query);
        Task<List<NewsFeed>> GetNewsFeeds(Expression<Func<NewsFeed, bool>> query);
    }

    public class RealmDataCacheService : IDataCacheService
    {
        private const string RealmName = "NewsBlurStore";

        private ThreadLocal<Realm> _instance;
        private Transaction _transaction;

        private Realm Instance => _instance.Value;

        public Task Startup()
        {
            _instance = new ThreadLocal<Realm>(() => Realm.GetInstance(RealmName));

            return Task.CompletedTask;
        }

        public void BeginWrite() => _transaction = Instance.BeginWrite();

        public void Commit() => _transaction?.Commit();
        public void AddFeed(NewsFeed item, bool isUpdate = false)
        {
            Instance.Add(item, isUpdate);
        }

        public Task<List<Story>> GetStories(Expression<Func<Story, bool>> query)
        {
            var stories = Instance.All<Story>().Where(query);
            return Task.FromResult(stories.ToList());
        }

        public Task<List<NewsFeed>> GetNewsFeeds(Expression<Func<NewsFeed, bool>> query)
        {
            var feeds = Instance.All<NewsFeed>().Where(query);
            return Task.FromResult(feeds.ToList());
        }
    }
}