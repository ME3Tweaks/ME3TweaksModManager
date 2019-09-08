using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Data;

namespace Gammtek.Conduit.Collections.ObjectModel
{
	[Serializable]
	public class ObservableDictionary<TKey, TValue> :
		IDictionary<TKey, TValue>,
		ICollection<KeyValuePair<TKey, TValue>>,
		IEnumerable<KeyValuePair<TKey, TValue>>,
		IDictionary,
		ICollection,
		IEnumerable,
		ISerializable,
		IDeserializationCallback,
		INotifyCollectionChanged,
		INotifyPropertyChanged
	{
		protected KeyedDictionaryEntryCollection<TKey> KeyedEntryCollection;
		private readonly Dictionary<TKey, TValue> _dictionaryCache = new Dictionary<TKey, TValue>();

		[NonSerialized]
		private readonly SerializationInfo _siInfo;

		private int _countCache;
		private int _dictionaryCacheVersion;
		private int _version;

		public ObservableDictionary()
		{
			KeyedEntryCollection = new KeyedDictionaryEntryCollection<TKey>();
		}

		public ObservableDictionary(IDictionary<TKey, TValue> dictionary)
		{
			KeyedEntryCollection = new KeyedDictionaryEntryCollection<TKey>();

			foreach (var entry in dictionary)
			{
				DoAddEntry(entry.Key, entry.Value);
			}
		}

		public ObservableDictionary(IEqualityComparer<TKey> comparer)
		{
			KeyedEntryCollection = new KeyedDictionaryEntryCollection<TKey>(comparer);
		}

		public ObservableDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
		{
			KeyedEntryCollection = new KeyedDictionaryEntryCollection<TKey>(comparer);

			foreach (var entry in dictionary)
			{
				DoAddEntry(entry.Key, entry.Value);
			}
		}

		protected ObservableDictionary(SerializationInfo info, StreamingContext context)
		{
			_siInfo = info;
		}

		protected virtual event NotifyCollectionChangedEventHandler CollectionChanged;

		protected virtual event PropertyChangedEventHandler PropertyChanged;

		event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
		{
			add { CollectionChanged += value; }
			remove { CollectionChanged -= value; }
		}

		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
		{
			add { PropertyChanged += value; }
			remove { PropertyChanged -= value; }
		}

		public IEqualityComparer<TKey> Comparer
		{
			get { return KeyedEntryCollection.Comparer; }
		}

		public int Count
		{
			get { return KeyedEntryCollection.Count; }
		}

		public Dictionary<TKey, TValue>.KeyCollection Keys
		{
			get { return TrueDictionary.Keys; }
		}

		public Dictionary<TKey, TValue>.ValueCollection Values
		{
			get { return TrueDictionary.Values; }
		}

		int ICollection<KeyValuePair<TKey, TValue>>.Count
		{
			get { return KeyedEntryCollection.Count; }
		}

		int ICollection.Count
		{
			get { return KeyedEntryCollection.Count; }
		}

		bool IDictionary.IsFixedSize
		{
			get { return false; }
		}

		bool IDictionary.IsReadOnly
		{
			get { return false; }
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
		{
			get { return false; }
		}

		bool ICollection.IsSynchronized
		{
			get { return ((ICollection) KeyedEntryCollection).IsSynchronized; }
		}

		ICollection<TKey> IDictionary<TKey, TValue>.Keys
		{
			get { return Keys; }
		}

		ICollection IDictionary.Keys
		{
			get { return Keys; }
		}

		object ICollection.SyncRoot
		{
			get { return ((ICollection) KeyedEntryCollection).SyncRoot; }
		}

		private Dictionary<TKey, TValue> TrueDictionary
		{
			get
			{
				if (_dictionaryCacheVersion != _version)
				{
					_dictionaryCache.Clear();
					foreach (var entry in KeyedEntryCollection)
					{
						_dictionaryCache.Add((TKey) entry.Key, (TValue) entry.Value);
					}
					_dictionaryCacheVersion = _version;
				}
				return _dictionaryCache;
			}
		}

		ICollection<TValue> IDictionary<TKey, TValue>.Values
		{
			get { return Values; }
		}

		ICollection IDictionary.Values
		{
			get { return Values; }
		}

		public TValue this[TKey key]
		{
			get { return (TValue) KeyedEntryCollection[key].Value; }
			set
			{
				DoSetEntry(key, value);
				OnPropertyChanged(Binding.IndexerName);
			}
		}

		TValue IDictionary<TKey, TValue>.this[TKey key]
		{
			get { return (TValue) KeyedEntryCollection[key].Value; }
			set { DoSetEntry(key, value); }
		}

		object IDictionary.this[object key]
		{
			get { return KeyedEntryCollection[(TKey) key].Value; }
			set { DoSetEntry((TKey) key, (TValue) value); }
		}

		public void Add(TKey key, TValue value)
		{
			DoAddEntry(key, value);
		}

		public void Clear()
		{
			DoClearEntries();
		}

		public bool ContainsKey(TKey key)
		{
			return KeyedEntryCollection.Contains(key);
		}

		public bool ContainsValue(TValue value)
		{
			return TrueDictionary.ContainsValue(value);
		}

		public IEnumerator GetEnumerator()
		{
			return new Enumerator<TKey, TValue>(this, false);
		}

		public bool Remove(TKey key)
		{
			return DoRemoveEntry(key);
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			var result = KeyedEntryCollection.Contains(key);
			value = result ? (TValue) KeyedEntryCollection[key].Value : default(TValue);
			return result;
		}

		protected virtual bool AddEntry(TKey key, TValue value)
		{
			KeyedEntryCollection.Add(new DictionaryEntry(key, value));
			return true;
		}

		protected virtual bool ClearEntries()
		{
			// check whether there are entries to clear
			var result = (Count > 0);
			if (result)
			{
				// if so, clear the dictionary
				KeyedEntryCollection.Clear();
			}
			return result;
		}

		protected int GetIndexAndEntryForKey(TKey key, out DictionaryEntry entry)
		{
			entry = new DictionaryEntry();
			var index = -1;
			if (KeyedEntryCollection.Contains(key))
			{
				entry = KeyedEntryCollection[key];
				index = KeyedEntryCollection.IndexOf(entry);
			}
			return index;
		}

		protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
		{
			if (CollectionChanged != null)
			{
				CollectionChanged(this, args);
			}
		}

		protected virtual void OnPropertyChanged(string name)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(name));
			}
		}

		protected virtual bool RemoveEntry(TKey key)
		{
			// remove the entry
			return KeyedEntryCollection.Remove(key);
		}

		protected virtual bool SetEntry(TKey key, TValue value)
		{
			var keyExists = KeyedEntryCollection.Contains(key);

			// if identical key/value pair already exists, nothing to do
			if (keyExists && value.Equals((TValue) KeyedEntryCollection[key].Value))
			{
				return false;
			}

			// otherwise, remove the existing entry
			if (keyExists)
			{
				KeyedEntryCollection.Remove(key);
			}

			// add the new entry
			KeyedEntryCollection.Add(new DictionaryEntry(key, value));

			return true;
		}

		private void DoAddEntry(TKey key, TValue value)
		{
			if (AddEntry(key, value))
			{
				_version++;

				DictionaryEntry entry;
				var index = GetIndexAndEntryForKey(key, out entry);
				FireEntryAddedNotifications(entry, index);
			}
		}

		private void DoClearEntries()
		{
			if (ClearEntries())
			{
				_version++;
				FireResetNotifications();
			}
		}

		private bool DoRemoveEntry(TKey key)
		{
			DictionaryEntry entry;
			var index = GetIndexAndEntryForKey(key, out entry);

			var result = RemoveEntry(key);
			if (result)
			{
				_version++;
				if (index > -1)
				{
					FireEntryRemovedNotifications(entry, index);
				}
			}

			return result;
		}

		private void DoSetEntry(TKey key, TValue value)
		{
			DictionaryEntry entry;
			var index = GetIndexAndEntryForKey(key, out entry);

			if (SetEntry(key, value))
			{
				_version++;

				// if prior entry existed for this key, fire the removed notifications
				if (index > -1)
				{
					FireEntryRemovedNotifications(entry, index);

					// force the property change notifications to fire for the modified entry
					_countCache--;
				}

				// then fire the added notifications
				index = GetIndexAndEntryForKey(key, out entry);
				FireEntryAddedNotifications(entry, index);
			}
		}

		private void FireEntryAddedNotifications(DictionaryEntry entry, int index)
		{
			// fire the relevant PropertyChanged notifications
			FirePropertyChangedNotifications();

			// fire CollectionChanged notification
			if (index > -1)
			{
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
					new KeyValuePair<TKey, TValue>((TKey) entry.Key, (TValue) entry.Value), index));
			}
			else
			{
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}

		private void FireEntryRemovedNotifications(DictionaryEntry entry, int index)
		{
			// fire the relevant PropertyChanged notifications
			FirePropertyChangedNotifications();

			// fire CollectionChanged notification
			if (index > -1)
			{
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
					new KeyValuePair<TKey, TValue>((TKey) entry.Key, (TValue) entry.Value), index));
			}
			else
			{
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}

		private void FirePropertyChangedNotifications()
		{
			if (Count != _countCache)
			{
				_countCache = Count;
				OnPropertyChanged("Count");
				OnPropertyChanged("Item[]");
				OnPropertyChanged("Keys");
				OnPropertyChanged("Values");
			}
		}

		private void FireResetNotifications()
		{
			// fire the relevant PropertyChanged notifications
			FirePropertyChangedNotifications();

			// fire CollectionChanged notification
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		void ICollection.CopyTo(Array array, int index)
		{
			((ICollection) KeyedEntryCollection).CopyTo(array, index);
		}

		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> kvp)
		{
			DoAddEntry(kvp.Key, kvp.Value);
		}

		void ICollection<KeyValuePair<TKey, TValue>>.Clear()
		{
			DoClearEntries();
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> kvp)
		{
			return KeyedEntryCollection.Contains(kvp.Key);
		}

		void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
		{
			if (array == null)
			{
				throw new ArgumentNullException(nameof(array), "CopyTo() failed:  array parameter was null");
			}
			if ((index < 0) || (index > array.Length))
			{
				throw new ArgumentOutOfRangeException("CopyTo() failed:  index parameter was outside the bounds of the supplied array");
			}
			if ((array.Length - index) < KeyedEntryCollection.Count)
			{
				throw new ArgumentException("CopyTo() failed:  supplied array was too small");
			}

			foreach (var entry in KeyedEntryCollection)
			{
				array[index++] = new KeyValuePair<TKey, TValue>((TKey) entry.Key, (TValue) entry.Value);
			}
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> kvp)
		{
			return DoRemoveEntry(kvp.Key);
		}

		public virtual void OnDeserialization(object sender)
		{
			if (_siInfo != null)
			{
				var entries = (Collection<DictionaryEntry>)
					_siInfo.GetValue("entries", typeof (Collection<DictionaryEntry>));
				foreach (var entry in entries)
				{
					AddEntry((TKey) entry.Key, (TValue) entry.Value);
				}
			}
		}

		void IDictionary.Add(object key, object value)
		{
			DoAddEntry((TKey) key, (TValue) value);
		}

		void IDictionary.Clear()
		{
			DoClearEntries();
		}

		bool IDictionary.Contains(object key)
		{
			return KeyedEntryCollection.Contains((TKey) key);
		}

		IDictionaryEnumerator IDictionary.GetEnumerator()
		{
			return new Enumerator<TKey, TValue>(this, true);
		}

		void IDictionary.Remove(object key)
		{
			DoRemoveEntry((TKey) key);
		}

		void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
		{
			DoAddEntry(key, value);
		}

		bool IDictionary<TKey, TValue>.ContainsKey(TKey key)
		{
			return KeyedEntryCollection.Contains(key);
		}

		bool IDictionary<TKey, TValue>.Remove(TKey key)
		{
			return DoRemoveEntry(key);
		}

		bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value)
		{
			return TryGetValue(key, out value);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
		{
			return new Enumerator<TKey, TValue>(this, false);
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
			{
				throw new ArgumentNullException(nameof(info));
			}

			var entries = new Collection<DictionaryEntry>();
			foreach (var entry in KeyedEntryCollection)
			{
				entries.Add(entry);
			}
			info.AddValue("entries", entries);
		}

		[Serializable]
		[StructLayout(LayoutKind.Sequential)]
		public struct Enumerator<TKey, TValue> : IEnumerator<KeyValuePair<TKey, TValue>>, IDictionaryEnumerator
		{
			internal Enumerator(ObservableDictionary<TKey, TValue> dictionary, bool isDictionaryEntryEnumerator)
			{
				_dictionary = dictionary;
				_version = dictionary._version;
				_index = -1;
				_isDictionaryEntryEnumerator = isDictionaryEntryEnumerator;
				_current = new KeyValuePair<TKey, TValue>();
			}

			public KeyValuePair<TKey, TValue> Current
			{
				get
				{
					ValidateCurrent();

					return _current;
				}
			}

			public void Dispose() {}

			public bool MoveNext()
			{
				ValidateVersion();
				_index++;

				if (_index < _dictionary.KeyedEntryCollection.Count)
				{
					_current = new KeyValuePair<TKey, TValue>((TKey) _dictionary.KeyedEntryCollection[_index].Key,
						(TValue) _dictionary.KeyedEntryCollection[_index].Value);
					return true;
				}

				_index = -2;
				_current = new KeyValuePair<TKey, TValue>();
				return false;
			}

			private void ValidateCurrent()
			{
				if (_index == -1)
				{
					throw new InvalidOperationException("The enumerator has not been started.");
				}
				if (_index == -2)
				{
					throw new InvalidOperationException("The enumerator has reached the end of the collection.");
				}
			}

			private void ValidateVersion()
			{
				if (_version != _dictionary._version)
				{
					throw new InvalidOperationException("The enumerator is not valid because the dictionary changed.");
				}
			}

			object IEnumerator.Current
			{
				get
				{
					ValidateCurrent();
					if (_isDictionaryEntryEnumerator)
					{
						return new DictionaryEntry(_current.Key, _current.Value);
					}
					return new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
				}
			}

			void IEnumerator.Reset()
			{
				ValidateVersion();
				_index = -1;
				_current = new KeyValuePair<TKey, TValue>();
			}

			DictionaryEntry IDictionaryEnumerator.Entry
			{
				get
				{
					ValidateCurrent();
					return new DictionaryEntry(_current.Key, _current.Value);
				}
			}

			object IDictionaryEnumerator.Key
			{
				get
				{
					ValidateCurrent();
					return _current.Key;
				}
			}

			object IDictionaryEnumerator.Value
			{
				get
				{
					ValidateCurrent();
					return _current.Value;
				}
			}

			private readonly ObservableDictionary<TKey, TValue> _dictionary;
			private readonly int _version;
			private int _index;
			private KeyValuePair<TKey, TValue> _current;
			private readonly bool _isDictionaryEntryEnumerator;
		}

		protected class KeyedDictionaryEntryCollection<TKey> : KeyedCollection<TKey, DictionaryEntry>
		{
			public KeyedDictionaryEntryCollection() {}

			public KeyedDictionaryEntryCollection(IEqualityComparer<TKey> comparer)
				: base(comparer) {}

			protected override TKey GetKeyForItem(DictionaryEntry entry)
			{
				return (TKey) entry.Key;
			}
		}
	}
}
