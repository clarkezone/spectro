using Spectro.DataModel;
using Spectro.ViewModels;
using Realms;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Windows.UI.Xaml.Data;

namespace Spectro.Models.UWP
{
    //This is UWP specific due to IITemsRangeInfo, ISelectionInfo, may be possible to refactor into platform specific pieces.

    public class RealmAllNewsFeedsSource : INotifyCollectionChanged, IList, IItemsRangeInfo, ISelectionInfo
    {
        Realm r;
        IQueryable<NewsFeed> query;

        public RealmAllNewsFeedsSource()
        {
            r = DataModelManager.RealmInstance;
            query = r.All<NewsFeed>().OrderBy(ob => ob.Title);
        }

        #region IItemsRangeInfo
        public void RangesChanged(ItemIndexRange visibleRange, IReadOnlyList<ItemIndexRange> trackedItems)
        {
            Debug.WriteLine($"{visibleRange.FirstIndex} + {visibleRange.LastIndex}");
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
        #endregion

        List<NewsFeed> cache = null;

        #region IList
        public object this[int index]
        {
            get
            {
                //TODO: this should be a factory or some other binding
                return new NewsFeedViewModel() { Episode = query.ElementAt(index) };
                //return query.ElementAt(index);

                // we really need skip / take.  The following code proves that read perf is an issue

                if (cache == null)
                {
                    //cache = query.Take(200).ToList();
                    cache = query.ToList();
                }
                return cache[index];
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public bool IsFixedSize => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        public int Count => query.Count();

        public bool IsSynchronized => throw new NotImplementedException();

        public int Add(object value)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(object value)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public int IndexOf(object value)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, object value)
        {
            throw new NotImplementedException();
        }

        public void Remove(object value)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        public object SyncRoot => throw new NotImplementedException();
        #endregion

        #region INotifyCollectionChanged
        public event NotifyCollectionChangedEventHandler CollectionChanged;


        #endregion

        #region ISelectionInfo
        public void SelectRange(ItemIndexRange itemIndexRange)
        {
            throw new NotImplementedException();
        }

        public void DeselectRange(ItemIndexRange itemIndexRange)
        {
            throw new NotImplementedException();
        }

        public bool IsSelected(int index)
        {
            return false;
        }

        public IReadOnlyList<ItemIndexRange> GetSelectedRanges()
        {
            return null;
        }
        #endregion 
    }
}
