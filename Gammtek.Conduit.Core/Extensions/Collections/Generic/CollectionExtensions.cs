using System;
using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.Extensions.Collections.Generic
{
	/// <summary>
	///     Class that provides extension methods to Collection
	/// </summary>
	public static class CollectionExtensions
	{
		/// <summary>
		///     Add a range of items to a collection.
		/// </summary>
		/// <typeparam name="T">Type of objects within the collection.</typeparam>
		/// <param name="collection">The collection to add items to.</param>
		/// <param name="items">The items to add to the collection.</param>
		/// <returns>The collection.</returns>
		/// <exception cref="System.ArgumentNullException">
		///     An <see cref="System.ArgumentNullException" /> is thrown if <paramref name="collection" />
		///     or <paramref name="items" /> is <see langword="null" />.
		/// </exception>
		public static ICollection<T> AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
		{
			if (collection == null)
			{
				throw new ArgumentNullException(nameof(collection));
			}

			if (items == null)
			{
				throw new ArgumentNullException(nameof(items));
			}

			foreach (var each in items)
			{
				collection.Add(each);
			}

			return collection;
		}

		public static int RemoveAll<TSource>(this ICollection<TSource> source, TSource value)
		{
			return RemoveAll(source, value, null);
		}

		public static int RemoveAll<TSource>(this ICollection<TSource> source, TSource value, IEqualityComparer<TSource> comparer)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			comparer = comparer ?? EqualityComparer<TSource>.Default;

			var removedItems = source.Where(item => comparer.Equals(value, item)).ToList();

			foreach (var item in removedItems)
			{
				source.Remove(item);
			}

			return removedItems.Count;
		}

		public static int RemoveAll<TSource>(this ICollection<TSource> source, Func<TSource, bool> predicate)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			if (predicate == null)
			{
				throw new ArgumentNullException(nameof(predicate));
			}

			var removedItems = source.Where(predicate).ToList();

			foreach (var item in removedItems)
			{
				source.Remove(item);
			}

			return removedItems.Count;
		}
	}
}
