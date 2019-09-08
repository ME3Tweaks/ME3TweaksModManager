using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Provides a set of extension methods dedicated to enumerables.
	/// </summary>
	internal static class InternalEnumerableExtensions
	{
		/// <summary>
		///     Append <paramref name="element" /> at the end of <paramref name="seq" />.
		/// </summary>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="seq">A sequence of elements.</param>
		/// <param name="element">The element to append.</param>
		/// <remarks>This extension method has a <i>constant</i> time complexity.</remarks>
		public static IEnumerable<T> Append<T>(this IEnumerable<T> seq, T element)
		{
			Contract.Requires(seq != null, "seq must not be null");

			//Contract.Requires(element != null, "element must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>().Any(), "returned sequence contains at least element");
			foreach (var elem in seq)
			{
				yield return elem;
			}
			yield return element;
		}

		/// <summary>
		///     Append <paramref name="element1" /> and <paramref name="element1" /> at the end of <paramref name="seq" />.
		/// </summary>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="seq">A sequence of elements.</param>
		/// <param name="element1">The element to append first.</param>
		/// <param name="element2">The element to append after <paramref name="element1" />.</param>
		/// <remarks>This extension method has a <i>constant</i> time complexity.</remarks>
		public static IEnumerable<T> Append<T>(this IEnumerable<T> seq, T element1, T element2)
		{
			Contract.Requires(seq != null, "seq must not be null");

			//Contract.Requires(element != null, "element must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>().Any(), "returned sequence contains at least element");
			foreach (var elem in seq)
			{
				yield return elem;
			}
			yield return element1;
			yield return element2;
		}

		/// <summary>
		///     Append <paramref name="element1" />, <paramref name="element2" /> and <paramref name="element3" /> at the end of <paramref name="seq" />.
		/// </summary>
		/// <remarks>To append more than 3 elements to <paramref name="seq" />, use the <i>.Concat(new [] { a, b, c, d ... } )</i> syntax.</remarks>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="seq">A sequence of elements.</param>
		/// <param name="element1">The element to append first.</param>
		/// <param name="element2">The element to append after <paramref name="element1" />.</param>
		/// <param name="element3">The element to append after <paramref name="element2" />.</param>
		/// <remarks>This extension method has a <i>constant</i> time complexity.</remarks>
		public static IEnumerable<T> Append<T>(this IEnumerable<T> seq, T element1, T element2, T element3)
		{
			Contract.Requires(seq != null, "seq must not be null");

			//Contract.Requires(element != null, "element must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>().Any(), "returned sequence contains at least element");
			foreach (var elem in seq)
			{
				yield return elem;
			}
			yield return element1;
			yield return element2;
			yield return element3;
		}

		// Don't do this method that would change the meaning of Concat() everywhere!!!
		/*///<summary>
      ///Gets an enumerable object that contains both <paramref name="element"/> and <paramref name="otherElement"/>, in this order.
      ///</summary>
      ///<typeparam name="TElement">The type of <paramref name="element"/>.</typeparam>
      ///<param name="element">The first element in the returned enumerable.</param>
      ///<param name="otherElement">The second element in the returned enumerable.</param>
      public static IEnumerable<TElement> Concat<TElement>(this TElement element, TElement otherElement) {
         Contract.Ensures(Contract.Result<IEnumerable<TElement>>() != null, "returned enumerable object is not null");
         return new[] { element, otherElement };
      }*/

		/// <summary>
		///     Gets an enumerable object that contains first <paramref name="element" /> and then elements of <paramref name="elements" />, in this order.
		/// </summary>
		/// <typeparam name="TElement">The type of <paramref name="element" />.</typeparam>
		/// <param name="element">The first element in the returned enumerable.</param>
		/// <param name="elements">The last elements in the returned enumerable.</param>
		public static IEnumerable<TElement> Concat<TElement>(this TElement element, IEnumerable<TElement> elements)
		{
			Contract.Ensures(Contract.Result<IEnumerable<TElement>>() != null, "returned enumerable object is not null");
			return new[] { element }.Concat(elements);
		}

		/// <summary>
		///     Gets an enumerable object that contains the elements of <paramref name="elements" /> and then <paramref name="element" />, in this order.
		/// </summary>
		/// <typeparam name="TElement">The type of <paramref name="element" />.</typeparam>
		/// <param name="elements">The first elements in the returned enumerable.</param>
		/// <param name="element">The last element in the returned enumerable.</param>
		public static IEnumerable<TElement> Concat<TElement>(this IEnumerable<TElement> elements, TElement element)
		{
			Contract.Ensures(Contract.Result<IEnumerable<TElement>>() != null, "returned enumerable object is not null");
			return elements.Concat(new[] { element });
		}

		/// <summary>
		///     Produces the set <paramref name="seq" /> excluding <paramref name="elementExcluded" />. The equality test relies on the <i>Equals()</i> method.
		/// </summary>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="seq">A sequence of elements.</param>
		/// <param name="elementExcluded">The element excluded.</param>
		/// <returns>A sequence that contains the set difference of the elements of <paramref name="seq" /> minus <paramref name="elementExcluded" />.</returns>
		/// <remarks>This extension method has a <i>constant</i> time complexity.</remarks>
		public static IEnumerable<T> Except<T>(this IEnumerable<T> seq, T elementExcluded)
		{
			Contract.Requires(seq != null, "seq must not be null");

			//Contract.Requires(elementExcluded != null, "elementExcluded must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			foreach (var element in seq)
			{
				if (elementExcluded.Equals(element))
				{
					continue;
				}
				yield return element;
			}
		}

		/// <summary>
		///     Determines the index of a specific item in <paramref name="readOnlyList" />.
		/// </summary>
		/// <typeparam name="T">The type parameter of the items in the read-only list.</typeparam>
		/// <param name="readOnlyList"></param>
		/// <param name="value">The object to locate in <paramref name="readOnlyList" />.</param>
		/// <returns>The index of value if found in the list; otherwise, -1.</returns>
		/// <remarks>This method uses the EqualityComparer$lt;T&gt;.Default.Equals() method on <paramref name="value" /> to determine whether item exists</remarks>
		public static int IndexOf<T>(this IReadOnlyList<T> readOnlyList, T value)
		{
			Contract.Requires(readOnlyList != null, "readOnlyList must not be null");
			var count = readOnlyList.Count;
			var equalityComparer = EqualityComparer<T>.Default;
			for (var i = 0; i < count; i++)
			{
				var current = readOnlyList[i];
				if (equalityComparer.Equals(current, value))
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		///     Gets an enumerable object that contains only the single element <paramref name="element" />.
		/// </summary>
		/// <typeparam name="TElement">The type of <paramref name="element" />.</typeparam>
		/// <param name="element">The single element in the returned enumerable.</param>
		public static IEnumerable<TElement> ToEnumerable<TElement>(this TElement element)
		{
			Contract.Ensures(Contract.Result<IEnumerable<TElement>>() != null, "returned enumerable object is not null");

			// This implementation is faster in all case than:   yield return elem;
			return new[] { element };
		}

		/// <summary>
		///     Creates a <see cref="Lookup{TKey,TElement}" /> from an <see cref="IEnumerable{T}" /> according to a specified key selector function.
		///     <b>The funtion can return zero, one or several keys for an element.</b>.
		/// </summary>
		/// <remarks>
		///     The difference with
		///     <seealso cref="Enumerable.ToLookup{TSource,TKey}(System.Collections.Generic.IEnumerable{TSource},System.Func{TSource,TKey})" /> is that an
		///     element can have zero, one or several keys.<br />
		///     Hence an element can be contained in several groups, if it has several keys.
		/// </remarks>
		/// <typeparam name="T">The type of the elements of <paramref name="seq" />.</typeparam>
		/// <typeparam name="TKey">The type of the keys in the result <see cref="Lookup{TKey,TElement}" />.</typeparam>
		/// <param name="seq">An <i>IEnumerable&lt;T&gt;</i> to create an hashset from.</param>
		/// <param name="func">A function to extract a sequence of keys from each element.</param>
		/// <returns>An hashset that contains the elements from the input sequence.</returns>
		/// <seealso cref="Enumerable.ToLookup{TSource,TKey}(System.Collections.Generic.IEnumerable{TSource},System.Func{TSource,TKey})" />
		public static ILookup<TKey, T> ToMultiKeyLookup<T, TKey>(this IEnumerable<T> seq, Func<T, IEnumerable<TKey>> func)
		{
			Contract.Requires(seq != null, "seq must not be null");
			Contract.Requires(func != null, "func must not be null");
			Contract.Ensures(Contract.Result<ILookup<TKey, T>>() != null, "returned lookup object is not null");
			Debug.Assert(seq != null);
			var dicoLookup = new DicoLookup<TKey, T>();
			foreach (var elem in seq)
			{
				var keys = func(elem);
				if (keys == null)
				{
					continue;
				}
				foreach (var key in keys)
				{
					IEnumerable<T> seqTmp;
					if (!dicoLookup.TryGetValue(key, out seqTmp))
					{
						// A single sequence is added first, and if several elements share the same key we transform the single sequence into a list.
						// This is an optimisation because a single sequence is cheaper than a List<T>,
						// and we hope that most keys are not not shared by several elements.
						dicoLookup.Add(key, elem.ToEnumerable());
						continue;
					}
					var list = seqTmp as List<T>;
					if (list != null)
					{
						list.Add(elem);
						continue;
					}
					list = new List<T> { seqTmp.First(), elem };
					dicoLookup[key] = list;
				}
			}
			return dicoLookup;
		}

		/// <summary>
		///     Creates a <see cref="IReadOnlyCollection{T}" /> cloned collection around <paramref name="collection" />.
		/// </summary>
		/// <typeparam name="T">The type parameter of the items in the collection.</typeparam>
		/// <param name="collection">A collection object.</param>
		public static IReadOnlyCollection<T> ToReadOnlyClonedCollection<T>(this IEnumerable<T> collection)
		{
			Contract.Requires(collection != null, "collection must not be null");
			Contract.Ensures(Contract.Result<IReadOnlyCollection<T>>() != null, "returned read-only collection is not null");
			Contract.Ensures(Contract.Result<IReadOnlyCollection<T>>().Count == collection.Count(),
				"returned read-only collection has the same number of elements as collection");
			return new CollectionReadOnlyWrapper<T>(collection.ToArray());
		}

		/// <summary>
		///     Creates a <see cref="IReadOnlyCollection{T}" /> cloned collection around <paramref name="list" />.
		/// </summary>
		/// <typeparam name="T">The type parameter of the items in the list.</typeparam>
		/// <param name="list">A list object.</param>
		public static IReadOnlyList<T> ToReadOnlyClonedList<T>(this IEnumerable<T> list)
		{
			Contract.Requires(list != null, "collection must not be null");
			Contract.Ensures(Contract.Result<IReadOnlyList<T>>() != null, "returned read-only list is not null");
			Contract.Ensures(Contract.Result<IReadOnlyList<T>>().Count == list.Count(), "returned read-only list has the same number of elements as list");
			return new ListReadOnlyWrapper<T>(list.ToArray());
		}

		//-----------------------------------------------
		//
		// ReadOnly list and Collection
		// Idea taken from http://stackoverflow.com/questions/343466/does-dot-net-have-an-interface-like-ienumerable-with-a-count-property
		//
		//-----------------------------------------------

		/// <summary>
		///     Creates a <see cref="IReadOnlyCollection{T}" /> wrapper collection around <paramref name="collection" />.
		/// </summary>
		/// <typeparam name="T">The type parameter of the items in the collection.</typeparam>
		/// <param name="collection">A collection object.</param>
		public static IReadOnlyCollection<T> ToReadOnlyWrappedCollection<T>(this ICollection<T> collection)
		{
			Contract.Requires(collection != null, "collection must not be null");
			Contract.Ensures(Contract.Result<IReadOnlyCollection<T>>() != null, "returned read-only collection is not null");
			Contract.Ensures(Contract.Result<IReadOnlyCollection<T>>().Count == collection.Count,
				"returned read-only collection has the same number of elements as collection");
			return new CollectionReadOnlyWrapper<T>(collection);
		}

		/// <summary>
		///     Creates a <see cref="IReadOnlyCollection{T}" /> wrapper collection around <paramref name="list" />.
		/// </summary>
		/// <typeparam name="T">The type parameter of the items in the list.</typeparam>
		/// <param name="list">A list object.</param>
		public static IReadOnlyList<T> ToReadOnlyWrappedList<T>(this IList<T> list)
		{
			Contract.Requires(list != null, "collection must not be null");
			Contract.Ensures(Contract.Result<IReadOnlyList<T>>() != null, "returned read-only list is not null");
			Contract.Ensures(Contract.Result<IReadOnlyList<T>>().Count == list.Count, "returned read-only list has the same number of elements as list");
			return new ListReadOnlyWrapper<T>(list);
		}

		/// <summary>
		///     Add a pair of <paramref name="key" /> and <paramref name="value" /> to <paramref name="dico" />, only if <paramref name="dico" /> doesn't already
		///     contain the <paramref name="key" />.
		/// </summary>
		/// <returns>
		///     <i>false</i> if the <paramref name="dico" /> already contains the <paramref name="key" />. Else add the pair <paramref name="key" /> and
		///     <paramref name="value" /> and returns <i>true</i>.
		/// </returns>
		/// <param name="dico">This dictionary.</param>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dico, TKey key, TValue value)
		{
			Contract.Requires(dico != null, "dico must not be null");
			if (dico.ContainsKey(key))
			{
				return false;
			}
			dico.Add(key, value);
			return true;
		}

		private class CollectionReadOnlyWrapper<T> : IReadOnlyCollection<T>
		{
			private readonly ICollection<T> m_Collection;

			internal CollectionReadOnlyWrapper(ICollection<T> collection)
			{
				Debug.Assert(collection != null);
				m_Collection = collection;
			}

			public int Count => m_Collection.Count;

			public IEnumerator<T> GetEnumerator()
			{
				return m_Collection.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return ((IEnumerable) m_Collection).GetEnumerator();
			}
		}

		private sealed class DicoLookup<TKey, T> : Dictionary<TKey, IEnumerable<T>>, ILookup<TKey, T>
		{
			// HACK: 17Aug2012: these two are needed to avoid complex Code Contract warning!
			public new int Count => base.Count;

			public new IEnumerable<T> this[TKey key]
			{
				get { return base[key]; }
				set { base[key] = value; }
			}

			public new IEnumerator<IGrouping<TKey, T>> GetEnumerator()
			{
				var dico = this as Dictionary<TKey, IEnumerable<T>>;
				foreach (var pair in dico)
				{
					yield return new Grouping(pair.Key, pair.Value);
				}
			}

			bool ILookup<TKey, T>.Contains(TKey key)
			{
				return ContainsKey(key);
			}

			private sealed class Grouping : IGrouping<TKey, T>
			{
				private readonly IEnumerable<T> m_Seq;

				internal Grouping(TKey key, IEnumerable<T> seq)
				{
					Debug.Assert(seq != null);
					Key = key;
					m_Seq = seq;
				}

				public TKey Key { get; }

				public IEnumerator<T> GetEnumerator()
				{
					return m_Seq.GetEnumerator();
				}

				IEnumerator IEnumerable.GetEnumerator()
				{
					return GetEnumerator();
				}
			}
		}

		private sealed class ListReadOnlyWrapper<T> : CollectionReadOnlyWrapper<T>, IReadOnlyList<T>
		{
			private readonly IList<T> m_List;

			internal ListReadOnlyWrapper(IList<T> list)
				: base(list)
			{
				Debug.Assert(list != null);
				m_List = list;
			}

			public T this[int index] => m_List[index];
		}
	}
}
