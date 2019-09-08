using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Extension methods helpers on collection of paths.
	/// </summary>
	internal static class InternalPathsCollectionExtensions
	{
		/// <summary>
		///     Returns <i>true</i> if <paramref name="seq" /> contains <paramref name="path" />.
		/// </summary>
		/// <remarks>
		///     The method IPath.Equals(), overridden from System.Object, is used to test path equality.
		/// </remarks>
		/// <param name="seq">The sequence to search in.</param>
		/// <param name="path">The path to search for.</param>
		public static bool ContainsPath<T, K>(this IEnumerable<T> seq, K path) where T : class, IPath where K : class, IPath
		{
			Contract.Requires(seq != null, "seq must not be null");
			Contract.Requires(path != null, "path must not be null");
			foreach (var pathTmp in seq)
			{
				if (path.Equals(pathTmp))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		///     Determine if this collection1 and collection2 contain the same set of paths.
		/// </summary>
		/// <remarks>
		///     Collections can contain null paths.
		/// </remarks>
		/// <typeparam name="T">The path type, any interface type implementing IPath</typeparam>
		/// <param name="collection1">This collection of paths.</param>
		/// <param name="collection2">The other collection of paths.</param>
		/// <returns>
		///     true if collection2 contains the same set of path than this collection1.
		///     true also, if this collection1 and collection2 are both null.
		///     true also, if this collection1 and collection2 are both empty.
		/// </returns>
		public static bool ContainsSamePathsThan<T>(this ICollection<T> collection1, ICollection<T> collection2) where T : class, IPath
		{
			if (collection1 == null)
			{
				return collection2 == null;
			}
			if (collection2 == null)
			{
				return false;
			}
			if (collection1.Count != collection2.Count)
			{
				return false;
			}

			var hashet2 = new HashSet<T>(collection2);
			foreach (var path1 in collection1)
			{
				if (!hashet2.Contains(path1))
				{
					return false;
				}

				// Need to remove for the case there are not the same number of null paths.
				hashet2.Remove(path1);
			}
			return true;
		}

		/// <summary>
		///     Find the common root directory of all directories of this collection.
		/// </summary>
		/// <remarks>
		///     A return value indicates whether a common root directory has been found.
		///     A common root directory cannot be found if at least two directories path on two different drives are in this collection.
		///     If the collection contains at least one null path, a common root directory cannot be found.
		/// </remarks>
		/// <param name="collection">This collection of directories absolute paths.</param>
		/// <param name="commonRootDirectory">The common root directory if it has been found.</param>
		/// <returns>
		///     true if a common root directory has been found, else returns false.
		/// </returns>
		public static bool TryGetCommonRootDirectory(this ICollection<IAbsoluteDirectoryPath> collection, out IAbsoluteDirectoryPath commonRootDirectory)
		{
			Contract.Requires(collection != null, "collection must not be null");

			string prefix;
			if (!TryFindCommonPrefix(
				collection.Select(p => p == null ? null : p.ToString()),
				true,
				System.IO.Path.DirectorySeparatorChar,
				out prefix))
			{
				commonRootDirectory = null;
				return false;
			}
			Debug.Assert(prefix != null);
			//Debug.Assert(prefix.IsValidAbsoluteDirectoryPath());
			//commonRootDirectory = prefix.ToAbsoluteDirectoryPath();
			commonRootDirectory = null;
			return true;
		}

		internal static bool TryFindCommonPrefix(IEnumerable<string> collection, bool ignoreCase, char separatorChar, out string commonPrefix)
		{
			Debug.Assert(collection != null);
			commonPrefix = null;
			var array = collection.ToArray();
			if (array.Length == 0)
			{
				return false;
			}

			// If the list contains a path null -> no common commonPrefix
			foreach (var str in array)
			{
				if (str == null || str.Length == 0)
				{
					return false;
				}
			}

			var firstStr = array[0];

			//
			//  Case where all paths are identical
			//  or where only one path
			//
			var allStringsAreIdentical = true;
			foreach (var str in array)
			{
				if (string.Compare(firstStr, str, ignoreCase) != 0)
				{
					allStringsAreIdentical = false;
					break;
				}
			}
			if (allStringsAreIdentical)
			{
				commonPrefix = firstStr;
				return true;
			}

			//
			//  Build listOfSplittedPaths
			//
			var listOfSplittedStrings = new List<string[]>();
			var maxDeep = int.MaxValue;
			foreach (var str in array)
			{
				var strSplitted = str.Split(separatorChar);
				if (strSplitted.Length < maxDeep)
				{
					maxDeep = strSplitted.Length;
				}
				listOfSplittedStrings.Add(strSplitted);
			}
			Debug.Assert(maxDeep >= 1);

			//
			// Compute prefixSb!
			//
			var prefixSb = new StringBuilder();
			for (var i = 0; i < maxDeep; i++)
			{
				var current = listOfSplittedStrings[0][i];
				foreach (var strSplitted in listOfSplittedStrings)
				{
					if (string.Compare(strSplitted[i], current, ignoreCase) != 0)
					{
						// i==0 means that we have no common commonPrefix!!
						if (i == 0)
						{
							return false;
						}
						goto DONE_OK;
					}
				}
				if (i > 0)
				{
					prefixSb.Append(separatorChar);
				}
				prefixSb.Append(current);
			}
			DONE_OK:
			Debug.Assert(prefixSb.Length > 0);
			commonPrefix = prefixSb.ToString();
			return true;
		}
	}
}
