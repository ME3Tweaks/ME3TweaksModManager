using System;
using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.Extensions.Collections.Generic
{
	public static partial class EnumerableExtensions
	{
		public static void ForEach<TSource>(this IEnumerable<TSource> source, Action<TSource> action)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException("source");
			}

			if (action == null)
			{
				ThrowHelper.ThrowArgumentNullException("action");
			}

			foreach (var item in source)
			{
				action(item);
			}
		}

		public static IEnumerable<TSource> ForEach<TSource>(this IEnumerable<TSource> source, Action<TSource, int> action)
		{
			if (source == null)
			{
				ThrowHelper.ThrowArgumentNullException("source");
			}

			if (action == null)
			{
				ThrowHelper.ThrowArgumentNullException("action");
			}

			var i = 0;

			var forEach = source as IList<TSource> ?? source.ToList();

			foreach (var item in forEach)
			{
				action(item, i);
				i++;
			}

			return forEach;
		}
	}
}
