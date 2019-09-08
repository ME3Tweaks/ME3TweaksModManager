using System;
using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.Extensions.Collections.Generic
{
	public static partial class EnumerableExtensions
	{
		public static int FindIndex<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			return FindIndex(source, 0, predicate);
		}

		public static int FindIndex<TSource>(this IEnumerable<TSource> source, int startIndex, Func<TSource, bool> predicate)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(source));
			}

			if (predicate == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(predicate));
			}

			var collection = source as ICollection<TSource> ?? source.ToList();

			if ((uint)startIndex > (uint)collection.Count)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(nameof(startIndex));
			}

			var idx = startIndex;

			foreach (var item in collection.Skip(startIndex))
			{
				if (predicate(item))
				{
					return idx;
				}

				idx++;
			}

			return -1;
		}

		public static int FindIndex<TSource>(this IEnumerable<TSource> source, int startIndex, int count, Func<TSource, bool> predicate)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(source));
			}

			if (predicate == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(predicate));
			}

			var collection = source as ICollection<TSource> ?? source.ToList();

			if ((uint)startIndex > (uint)collection.Count)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(nameof(startIndex));
			}

			if (count < 0 || startIndex > collection.Count - count)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
			}

			var idx = startIndex;

			foreach (var item in collection.Skip(startIndex).TakeWhile((item, index) => index < count))
			{
				if (predicate(item))
				{
					return idx;
				}

				idx++;
			}

			return -1;
		}

		public static int FindLastIndex<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(source));
			}

			var collection = source as ICollection<TSource> ?? source.ToList();

			return collection.FindLastIndex(collection.Count - 1, collection.Count, predicate);
		}

		public static int FindLastIndex<TSource>(this IEnumerable<TSource> source, int startIndex, Func<TSource, bool> predicate)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(source));
			}

			var collection = source as ICollection<TSource> ?? source.ToList();

			return collection.FindLastIndex(startIndex, startIndex + 1, predicate);
		}

		public static int FindLastIndex<TSource>(this IEnumerable<TSource> source, int startIndex, int count, Func<TSource, bool> predicate)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(source));
			}

			var collection = source as ICollection<TSource> ?? source.ToList();

			return collection.FindLastIndex(startIndex, count, predicate);
		}

		private static int FindLastIndex<TSource>([NotNull] this ICollection<TSource> source, int startIndex, int count,
			Func<TSource, bool> predicate)
		{
			if (predicate == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(predicate));
			}

			if (source.Count == 0)
			{
				if (startIndex != -1)
				{
					ThrowHelper.ThrowArgumentOutOfRangeException(nameof(startIndex));
				}
			}
			else
			{
				if ((uint)startIndex >= (uint)source.Count)
				{
					ThrowHelper.ThrowArgumentOutOfRangeException(nameof(startIndex));
				}
			}

			if (count < 0 || startIndex - source.Count + 1 < 0)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
			}

			var endIndex = startIndex - count;
			
			for (var i = startIndex; i > endIndex; i--)
			{
				if (predicate(source.ElementAt(i)))
				{
					return i;
				}
			}

			return -1;
		}

		public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource value, IEqualityComparer<TSource> comparer = null)
		{
			return IndexOf(source, value, 0, comparer);
		}

		public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource value, int startIndex, IEqualityComparer<TSource> comparer = null)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(source));
			}

			var collection = source as ICollection<TSource> ?? source.ToList();

			if ((uint)startIndex >= (uint)collection.Count)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(nameof(startIndex));
			}

			comparer = comparer ?? EqualityComparer<TSource>.Default;
			var idx = startIndex;

			foreach (var item in collection.Skip(startIndex))
			{
				if (comparer.Equals(item, value))
				{
					return idx;
				}

				idx++;
			}

			return -1;
		}

		public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource value, int startIndex, int count, IEqualityComparer<TSource> comparer = null)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(source));
			}

			var collection = source as ICollection<TSource> ?? source.ToList();

			if ((uint)startIndex >= (uint)collection.Count)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(nameof(startIndex));
			}

			comparer = comparer ?? EqualityComparer<TSource>.Default;
			var idx = startIndex;

			foreach (var item in collection.Skip(startIndex).TakeWhile((item, index) => index < count))
			{
				if (comparer.Equals(item, value))
				{
					return idx;
				}

				idx++;
			}

			return -1;
		}

		public static IEnumerable<TSource> TakeLast<TSource>(this IEnumerable<TSource> source, int count)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(source));
			}

			return TakeLastIterator(source, count);
		}

		private static IEnumerable<TSource> TakeLastIterator<TSource>(IEnumerable<TSource> source, int count)
		{
			if (count <= 0)
			{
				yield break;
			}

			var queue = new Queue<TSource>(count);

			foreach (var item in source)
			{
				if (queue.Count >= count)
				{
					queue.Dequeue();
				}

				queue.Enqueue(item);
			}

			while (queue.Count > 0)
			{
				yield return queue.Dequeue();
			}
		}

		public static IEnumerable<TSource> TakeLastWhile<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(source));
			}

			if (predicate == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(predicate));
			}

			return TakeLastWhileIterator(source, predicate);
		}

		private static IEnumerable<TSource> TakeLastWhileIterator<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			var queue = new Queue<TSource>();

			foreach (var item in source)
			{
				if (predicate(item))
				{
					queue.Enqueue(item);
				}
			}

			while (queue.Count > 0)
			{
				yield return queue.Dequeue();
			}
		}

		public static IEnumerable<TSource> TakeLastWhileTest<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			var buffer = new List<TSource>();

			foreach (var item in source)
			{
				if (predicate(item))
				{
					buffer.Add(item);
				}
				else
				{
					buffer.Clear();
				}
			}

			foreach (var item in buffer)
			{
				yield return item;
			}
		}

		public static IEnumerable<TSource> TakeLastWhile<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(source));
			}

			if (predicate == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(predicate));
			}

			return TakeLastWhileIterator(source, predicate);
		}

		private static IEnumerable<TSource> TakeLastWhileIterator<TSource>(IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
		{
			var index = -1;

			foreach (var element in source)
			{
				checked
				{
					index++;
				}

				if (!predicate(element, index))
				{
					break;
				}

				yield return element;
			}
		}
	}
}
