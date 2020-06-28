
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MassEffectModManagerCore.ui
{
    [Localizable(false)]
    public class ObservableCollectionExtended<T> : ObservableCollection<T>
    {
        //INotifyPropertyChanged inherited from ObservableCollection<T>
        #region INotifyPropertyChanged

        protected override event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangedEventHandler PublicPropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            PublicPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged

        public bool Any => this.Any();

        /// <summary> 
        /// Adds the elements of the specified collection to the end of the ObservableCollection(Of T). 
        /// </summary> 
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            int oldcount = Count;
            foreach (var i in collection) Items.Add(i);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            if (oldcount != Count)
            {
                OnPropertyChanged(nameof(Any));
                OnPropertyChanged(nameof(Count));
            }
        }

        /// <summary> 
        /// Removes the first occurence of each item in the specified collection from ObservableCollection(Of T). 
        /// </summary> 
        public void RemoveRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            // ReSharper disable once PossibleUnintendedReferenceComparison
            if (collection == Items) throw new Exception(@"Cannot remove range of same collection");
            int oldcount = Count;
            //Todo: catch reachspec crash when changing size
            foreach (var i in collection) Items.Remove(i);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            if (oldcount != Count)
            {
                OnPropertyChanged(nameof(Any));
                OnPropertyChanged(nameof(Count));
            }
        }

        /// <summary> 
        /// Removes all items then raises collection changed event
        /// </summary> 
        public void ClearEx()
        {
            int oldcount = Count;
            Items.Clear();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            if (oldcount != Count)
            {
                OnPropertyChanged(nameof(Any));
                OnPropertyChanged(nameof(Count));
            }

        }

        /// <summary> 
        /// Clears the current collection and replaces it with the specified item. 
        /// </summary> 
        public void Replace(T item)
        {
            ReplaceAll(new[] { item });
        }

        /// <summary> 
        /// Replaces all elements in existing collection with specified collection of the ObservableCollection(Of T). 
        /// </summary> 
        public void ReplaceAll(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            int oldcount = Count;
            Items.Clear();
            foreach (var i in collection) Items.Add(i);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            if (oldcount != Count)
            {
                OnPropertyChanged(nameof(Any));
                OnPropertyChanged(nameof(Count));
            }
        }

        #region Sorting

        /// <summary>
        /// Sorts the items of the collection in ascending order according to a key.
        /// </summary>
        /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector"/>.</typeparam>
        /// <param name="keySelector">A function to extract a key from an item.</param>
        public void Sort<TKey>(Func<T, TKey> keySelector)
        {
            InternalSort(Items.OrderBy(keySelector));
        }

        /// <summary>
        /// Sorts the items of the collection in descending order according to a key.
        /// </summary>
        /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector"/>.</typeparam>
        /// <param name="keySelector">A function to extract a key from an item.</param>
        public void SortDescending<TKey>(Func<T, TKey> keySelector)
        {
            InternalSort(Items.OrderByDescending(keySelector));
        }

        /// <summary>
        /// Sorts the items of the collection in ascending order according to a key.
        /// </summary>
        /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector"/>.</typeparam>
        /// <param name="keySelector">A function to extract a key from an item.</param>
        /// <param name="comparer">An <see cref="IComparer{T}"/> to compare keys.</param>
        public void Sort<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer)
        {
            InternalSort(Items.OrderBy(keySelector, comparer));
        }

        /// <summary>
        /// Moves the items of the collection so that their orders are the same as those of the items provided.
        /// </summary>
        /// <param name="sortedItems">An <see cref="IEnumerable{T}"/> to provide item orders.</param>
        private void InternalSort(IEnumerable<T> sortedItems)
        {
            var sortedItemsList = sortedItems.ToList();

            foreach (var item in sortedItemsList)
            {
                Move(IndexOf(item), sortedItemsList.IndexOf(item));
            }
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private int _bindableCount;
        public int BindableCount
        {
            get { return Count; }
            private set
            {
                if (_bindableCount != Count)
                {
                    _bindableCount = Count;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Used to force property to raise event changed. Used when refreshing language in application
        /// </summary>
        public void RaiseBindableCountChanged()
        {
            OnPropertyChanged(nameof(BindableCount));
        }

        #endregion // Sorting

        /// <summary> 
        /// Initializes a new instance of the System.Collections.ObjectModel.ObservableCollection(Of T) class. 
        /// </summary> 
        public ObservableCollectionExtended() : base()
        {
            CollectionChanged += (a, b) =>
            {
                BindableCount = Count;
                OnPropertyChanged(nameof(Any));
            };
        }

        /// <summary> 
        /// Initializes a new instance of the System.Collections.ObjectModel.ObservableCollection(Of T) class that contains elements copied from the specified collection. 
        /// </summary> 
        /// <param name="collection">collection: The collection from which the elements are copied.</param> 
        /// <exception cref="System.ArgumentNullException">The collection parameter cannot be null.</exception> 
        public ObservableCollectionExtended(IEnumerable<T> collection)
            : base(collection)
        {
            CollectionChanged += (a, b) =>
            {
                BindableCount = Count;
                OnPropertyChanged(nameof(Any));
            };
        }
    }
}
