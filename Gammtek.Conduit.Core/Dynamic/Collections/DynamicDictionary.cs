using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;

namespace Gammtek.Conduit.Dynamic.Collections
{
	/// <summary>
	///     An implementation of a <see cref="IDictionary{TKey,TValue}" /> which uses dynamics to allow
	///     property accessors
	/// </summary>
	/// <remarks>
	///     <para>
	///         This class inherits from <see cref="System.Dynamic.DynamicObject" /> and
	///         <see cref="IDictionary{TKey,TValue}" /> (to give default dictionary features as well).
	///         It allows you to have a dictionary which you access the key store via standard dot-notation. This is exposed via extension methods
	///         for users to create.
	///     </para>
	///     <example>
	///         var dictionary = new Dictionary&lt;string, string&gt; { { "hello", "world" } };
	///         var dynamicDictionary = dictionary.AsDynamic();
	///         //access data
	///         var local = dynamicDictionary.hello;
	///         //create new key
	///         dynamicDictionary.newValue = "I'm new!";
	///     </example>
	/// </remarks>
	/// <typeparam name="TValue">The type of the value.</typeparam>
	public sealed class DynamicDictionary<TValue> : DynamicObject, IDictionary<string, TValue>
	{
		private readonly IDictionary<string, TValue> _dictionary;

		/// <summary>
		///     Initializes a new instance of the <see cref="DynamicDictionary{TValue}" /> class.
		/// </summary>
		public DynamicDictionary()
			: this(new Dictionary<string, TValue>()) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="DynamicDictionary{TValue}" /> class from a given dictionary
		/// </summary>
		/// <param name="dictionary">The dictionary.</param>
		public DynamicDictionary(IDictionary<string, TValue> dictionary)
		{
			//set the internal dictionary instance
			_dictionary = dictionary;
		}

		/// <summary>
		///     Adds the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Add(string key, TValue value)
		{
			_dictionary.Add(key, value);
		}

		/// <summary>
		///     Determines whether the specified key contains key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns>
		///     <c>true</c> if the specified key contains key; otherwise, <c>false</c>.
		/// </returns>
		public bool ContainsKey(string key)
		{
			return _dictionary.ContainsKey(key);
		}

		/// <summary>
		///     Gets the keys.
		/// </summary>
		/// <value>The keys.</value>
		public ICollection<string> Keys
		{
			get { return _dictionary.Keys; }
		}

		/// <summary>
		///     Removes the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public bool Remove(string key)
		{
			return _dictionary.Remove(key);
		}

		/// <summary>
		///     Tries the get value.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns></returns>
		public bool TryGetValue(string key, out TValue value)
		{
			return _dictionary.TryGetValue(key, out value);
		}

		/// <summary>
		///     Gets the values.
		/// </summary>
		/// <value>The values.</value>
		public ICollection<TValue> Values
		{
			get { return _dictionary.Values; }
		}

		/// <summary>
		///     Gets or sets the <see cref="TValue" /> with the specified key.
		/// </summary>
		/// <value></value>
		public TValue this[string key]
		{
			get { return _dictionary[key]; }
			set { _dictionary[key] = value; }
		}

		/// <summary>
		///     Adds the specified item.
		/// </summary>
		/// <param name="item">The item.</param>
		public void Add(KeyValuePair<string, TValue> item)
		{
			_dictionary.Add(item);
		}

		/// <summary>
		///     Clears this instance.
		/// </summary>
		public void Clear()
		{
			_dictionary.Clear();
		}

		/// <summary>
		///     Determines whether [contains] [the specified item].
		/// </summary>
		/// <param name="item">The item.</param>
		/// <returns>
		///     <c>true</c> if [contains] [the specified item]; otherwise, <c>false</c>.
		/// </returns>
		public bool Contains(KeyValuePair<string, TValue> item)
		{
			return _dictionary.Contains(item);
		}

		/// <summary>
		///     Copies to.
		/// </summary>
		/// <param name="array">The array.</param>
		/// <param name="arrayIndex">Index of the array.</param>
		public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
		{
			_dictionary.CopyTo(array, arrayIndex);
		}

		/// <summary>
		///     Gets the count.
		/// </summary>
		/// <value>The count.</value>
		public int Count
		{
			get { return _dictionary.Count; }
		}

		/// <summary>
		///     Gets a value indicating whether this instance is read only.
		/// </summary>
		/// <value>
		///     <c>true</c> if this instance is read only; otherwise, <c>false</c>.
		/// </value>
		public bool IsReadOnly
		{
			get { return _dictionary.IsReadOnly; }
		}

		/// <summary>
		///     Removes the specified item.
		/// </summary>
		/// <param name="item">The item.</param>
		/// <returns></returns>
		public bool Remove(KeyValuePair<string, TValue> item)
		{
			return _dictionary.Remove(item);
		}

		/// <summary>
		///     Gets the enumerator.
		/// </summary>
		/// <returns></returns>
		public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
		{
			return _dictionary.GetEnumerator();
		}

		/// <summary>
		///     Returns an enumerator that iterates through a collection.
		/// </summary>
		/// <returns>
		///     An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
		/// </returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public override bool TryDeleteMember(DeleteMemberBinder binder)
		{
			var key = binder.Name;

			if (!_dictionary.ContainsKey(key))
			{
				return false;
			}

			_dictionary.Remove(key);

			return true;

			//throw new KeyNotFoundException(string.Format("Key \"{0}\" was not found in the given dictionary", key));
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			var key = binder.Name;
			
			//check if the key exists in the dictionary
			if (_dictionary.ContainsKey(key))
			{
				//set it and return true to indicate its found
				result = _dictionary[key];

				return true;
			}

			//look into the base implementation, it might be there
			var found = base.TryGetMember(binder, out result);

			//if it wasn't found we'll raise an exception
			if (!found)
			{
				throw new KeyNotFoundException(string.Format("Key \"{0}\" was not found in the given dictionary", key));
			}

			return true;
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			if (IsReadOnly)
			{
				throw new InvalidOperationException("This dictionary instance is read-only, you cannot modify the data it contains");
			}

			var key = binder.Name;

			//check if the dictionary already has this key
			if (_dictionary.ContainsKey(key))
			{
				//it did so we can assign it to the new value
				_dictionary[key] = (TValue) value;

				return true;
			}

			//check the base for the property
			var found = base.TrySetMember(binder, value);

			//if it wasn't found then the user must have wanted a new key
			//we'll expect implicit casting here, and an exception will be raised
			//if it cannot explicitly cast
			if (!found)
			{
				_dictionary.Add(key, (TValue) value);
			}

			return true;
		}

		//public void Add(DynamicKeyValuePair<string, TValue> item)
		//{
		//    this.dictionary.Add(new KeyValuePair<string, TValue>(item.Key, item.Value));
		//}
	}
}
