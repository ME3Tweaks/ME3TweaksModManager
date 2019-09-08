using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Provides a set of extension methods to optimize enumerables operations on set.
	/// </summary>
	internal static class InternalSetExtensions
	{
		//
		// Except
		//

		/// <summary>
		///     Produces the set difference of this <paramref name="seq" /> and <paramref name="hashset" />. This method is an optimized version of
		///     <i>Enumerable.Except&lt;T&gt;</i>.
		/// </summary>
		/// <typeparam name="T">The element type of the elements of the hashset and the sequence.</typeparam>
		/// <param name="seq">A sequence of elements whose elements that are not also in <paramref name="hashset" /> will be returned.</param>
		/// <param name="hashset">
		///     An hashset of elements whose elements that also occur in <paramref name="seq" /> will cause those elements to be removed from
		///     the returned sequence.
		/// </param>
		/// <returns>A sequence that contains the set difference of the elements of <paramref name="seq" /> and <paramref name="hashset" />.</returns>
		/// <remarks>This extension method has a <i>O(<paramref name="seq" />.Count)</i> time complexity.</remarks>
		public static IEnumerable<T> Except<T>(this IEnumerable<T> seq, HashSet<T> hashset) where T : class
		{
			Contract.Requires(seq != null, "seq must not be null");
			Contract.Requires(hashset != null, "hashset must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			foreach (var t in seq)
			{
				if (hashset.Contains(t))
				{
					continue;
				}
				yield return t;
			}
		}

		//
		// Intersect
		//

		/// <summary>
		///     Produces the set of elements, intersection of this <paramref name="hashset" /> and <paramref name="seq" />. This method is an optimized version
		///     of <i>Enumerable.Intersect&lt;T&gt;</i>.
		/// </summary>
		/// <typeparam name="T">The code element type of the elements of the hashset and the sequence.</typeparam>
		/// <param name="hashset">An hashset of elements whose distinct elements that also appear in <paramref name="seq" /> will be returned.</param>
		/// <param name="seq">A sequence of elements whose distinct elements that also appear in <paramref name="hashset" /> will be returned.</param>
		/// <returns>A sequence that contains the elements that form the set intersection of the hashset and the sequence.</returns>
		/// <remarks>This extension method has a <i>O(<paramref name="seq" />.Count)</i> time complexity.</remarks>
		public static IEnumerable<T> Intersect<T>(this HashSet<T> hashset, IEnumerable<T> seq)
		{
			Contract.Requires(hashset != null, "hashset must not be null");
			Contract.Requires(seq != null, "seq must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			return IntersectIterator(hashset, seq);
		}

		/// <summary>
		///     Produces the set of elements, intersection of this <paramref name="seq" /> and <paramref name="hashset" />. This method is an optimized version
		///     of <i>Enumerable.Intersect&lt;T&gt;</i>.
		/// </summary>
		/// <typeparam name="T">The code element type of the elements of the hashset and the sequence.</typeparam>
		/// <param name="hashset">An hashset of elements whose distinct elements that also appear in <paramref name="seq" /> will be returned.</param>
		/// <param name="seq">A sequence of elements whose distinct elements that also appear in <paramref name="hashset" /> will be returned.</param>
		/// <returns>A sequence that contains the elements that form the set intersection of the hashset and the sequence.</returns>
		/// <remarks>This extension method has a <i>O(<paramref name="seq" />.Count)</i> time complexity.</remarks>
		public static IEnumerable<T> Intersect<T>(this IEnumerable<T> seq, HashSet<T> hashset)
		{
			Contract.Requires(seq != null, "seq must not be null");
			Contract.Requires(hashset != null, "hashset must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			return IntersectIterator(hashset, seq);
		}

		/// <summary>
		///     Produces the set of elements, intersection of this <paramref name="hashset" /> and <paramref name="otherHashset" />. This method is an optimized
		///     version of <i>Enumerable.Intersect&lt;T&gt;</i>.
		/// </summary>
		/// <typeparam name="T">The code element type of the elements of the hashset and the sequence.</typeparam>
		/// <param name="hashset">An hashset of elements whose distinct elements that also appear in <paramref name="otherHashset" /> will be returned.</param>
		/// <param name="otherHashset">An hashset of elements whose distinct elements that also appear in <paramref name="hashset" /> will be returned.</param>
		/// <returns>A sequence that contains the elements that form the set intersection of both hashsets.</returns>
		/// <remarks>This extension method has a <i>O(<paramref name="otherHashset" />.Count)</i> time complexity.</remarks>
		public static IEnumerable<T> Intersect<T>(this HashSet<T> hashset, HashSet<T> otherHashset)
		{
			Contract.Requires(hashset != null, "hashset must not be null");
			Contract.Requires(otherHashset != null, "otherHashset must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			return IntersectIterator(hashset, otherHashset);
		}

		// Notes that there is no way to do Distinct with Hashset, since all elements are necessarily distinct in a hashset!

		//
		// Hashset operations!
		//

		/// <summary>
		///     Creates an hashset from a <i>IEnumerable&lt;T&gt;</i>.
		/// </summary>
		/// <typeparam name="T">The type of the elements of <paramref name="seq" />.</typeparam>
		/// <param name="seq">An <i>IEnumerable&lt;T&gt;</i> to create an hashset from.</param>
		/// <returns>An hashset that contains the elements from the input sequence.</returns>
		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> seq)
		{
			Contract.Requires(seq != null, "seq must not be null");
			Contract.Ensures(Contract.Result<HashSet<T>>() != null, "returned hashset object is not null");
			Debug.Assert(seq != null);
			return new HashSet<T>(seq);
		}

		//
		// Union
		//

		/// <summary>
		///     Produces the set of elements, union of this <paramref name="hashset" /> and <paramref name="seq" />. This method is an optimized version of
		///     <i>Enumerable.Union&lt;T&gt;</i>.
		/// </summary>
		/// <typeparam name="T">The code element type of the elements of the hashset and the sequence.</typeparam>
		/// <param name="hashset">An hashset of elements whose distinct elements form the first set for the union.</param>
		/// <param name="seq">A sequence of elements whose distinct elements form the second set for the union.</param>
		/// <returns>A sequence that contains the elements that form the set union of the hashset and the sequence, excluding duplicates.</returns>
		/// <remarks>This extension method has a <i>O(<paramref name="seq" />.Count)</i> time complexity.</remarks>
		public static IEnumerable<T> Union<T>(this HashSet<T> hashset, IEnumerable<T> seq)
		{
			Contract.Requires(hashset != null, "hashset must not be null");
			Contract.Requires(seq != null, "seq must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			return UnionIterator(hashset, seq);
		}

		/// <summary>
		///     Produces the set of elements, union of this <paramref name="seq" /> and <paramref name="hashset" />. This method is an optimized version of
		///     <i>Enumerable.Union&lt;T&gt;</i>.
		/// </summary>
		/// <typeparam name="T">The code element type of the elements of the hashset and the sequence.</typeparam>
		/// <param name="hashset">An hashset of elements whose distinct elements form the first set for the union.</param>
		/// <param name="seq">A sequence of elements whose distinct elements form the second set for the union.</param>
		/// <returns>A sequence that contains the elements that form the set union of the sequence and the hashset, excluding duplicates.</returns>
		/// <remarks>This extension method has a <i>O(<paramref name="seq" />.Count)</i> time complexity.</remarks>
		public static IEnumerable<T> Union<T>(this IEnumerable<T> seq, HashSet<T> hashset)
		{
			Contract.Requires(seq != null, "seq must not be null");
			Contract.Requires(hashset != null, "hashset must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			return UnionIterator(hashset, seq);
		}

		/// <summary>
		///     Produces the set of elements, union of this <paramref name="thisHashset" /> and <paramref name="otherHashset" />. This method is an optimized
		///     version of <i>Enumerable.Union&lt;T&gt;</i>.
		/// </summary>
		/// <typeparam name="T">The code element type of the elements of the hashset and the sequence.</typeparam>
		/// <param name="thisHashset">A sequence of elements whose distinct elements form the first set for the union.</param>
		/// <param name="otherHashset">An hashset of elements whose distinct elements form the second set for the union.</param>
		/// <returns>A sequence that contains the elements that form the set union of both hashsets, excluding duplicates.</returns>
		/// <remarks>This extension method has a <i>O(<paramref name="otherHashset" />.Count)</i> time complexity.</remarks>
		public static IEnumerable<T> Union<T>(this HashSet<T> thisHashset, HashSet<T> otherHashset)
		{
			Contract.Requires(thisHashset != null, "hashset must not be null");
			Contract.Requires(otherHashset != null, "otherHashset must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			return UnionIterator(thisHashset, otherHashset);
		}

		private static IEnumerable<T> IntersectIterator<T>(HashSet<T> hashset, IEnumerable<T> seq)
		{
			Debug.Assert(hashset != null);
			Debug.Assert(seq != null);
			foreach (var t in seq)
			{
				if (!hashset.Contains(t))
				{
					continue;
				}
				yield return t;
			}
		}

		private static IEnumerable<T> UnionIterator<T>(HashSet<T> hashset, IEnumerable<T> seq)
		{
			Debug.Assert(hashset != null);
			Debug.Assert(seq != null);

			foreach (var t in hashset)
			{
				yield return t;
			}
			foreach (var t in seq)
			{
				if (hashset.Contains(t))
				{
					continue;
				}
				yield return t;
			}
		}
	}
}
