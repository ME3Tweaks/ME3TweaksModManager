using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Provides a set of extension methods dedicated to strings.
	/// </summary>
	internal static class InternalStringExtensions
	{
		//
		// EndsWithAny operation
		//

		/// <summary>Gets a value indicating whether this string ends with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible end string for <paramref name="thisString" />.</param>
		public static bool EndsWithAny(this string thisString, string str0, string str1)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			return thisString.EndsWith(str0) || thisString.EndsWith(str1);
		}

		/// <summary>Gets a value indicating whether this string ends with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible end string for <paramref name="thisString" />.</param>
		public static bool EndsWithAny(this string thisString, string str0, string str1, string str2)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			return thisString.EndsWith(str0) || thisString.EndsWith(str1) || thisString.EndsWith(str2);
		}

		/// <summary>Gets a value indicating whether this string ends with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str3">One of the possible end string for <paramref name="thisString" />.</param>
		public static bool EndsWithAny(this string thisString, string str0, string str1, string str2, string str3)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			Contract.Requires(str3 != null, "str3 must not be null");
			return thisString.EndsWith(str0) || thisString.EndsWith(str1) || thisString.EndsWith(str2) || thisString.EndsWith(str3);
		}

		/// <summary>Gets a value indicating whether this string ends with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str3">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str4">One of the possible end string for <paramref name="thisString" />.</param>
		public static bool EndsWithAny(this string thisString, string str0, string str1, string str2, string str3, string str4)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			Contract.Requires(str3 != null, "str3 must not be null");
			Contract.Requires(str4 != null, "str4 must not be null");
			return thisString.EndsWith(str0) || thisString.EndsWith(str1) || thisString.EndsWith(str2) || thisString.EndsWith(str3)
				   || thisString.EndsWith(str4);
		}

		/// <summary>Gets a value indicating whether this string ends with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str3">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="str4">One of the possible end string for <paramref name="thisString" />.</param>
		/// <param name="array">An array containing possible end strings for <paramref name="thisString" />.</param>
		/// <exception cref="NullReferenceException">null string reference in <paramref name="array" /> not accepted.</exception>
		public static bool EndsWithAny(this string thisString, string str0, string str1, string str2, string str3, string str4, params string[] array)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			Contract.Requires(str3 != null, "str3 must not be null");
			Contract.Requires(str4 != null, "str4 must not be null");
			Contract.Requires(array != null, "strs must not be null");
			if (thisString.EndsWith(str0) || thisString.EndsWith(str1) || thisString.EndsWith(str2) || thisString.EndsWith(str3) || thisString.EndsWith(str4))
			{
				return true;
			}
			var length = array.Length;
			for (var i = 0; i < length; i++)
			{
				var str = array[i];
				if (str == null)
				{
					throw new NullReferenceException("null string reference in array not accepted.");
				}
				if (thisString.EndsWith(array[i]))
				{
					return true;
				}
			}
			return false;
		}

		//
		// EqualsAny
		//

		/// <summary>Gets a value indicating whether this string is equal <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible value for <paramref name="thisString" />.</param>
		public static bool EqualsAny(this string thisString, string str0, string str1)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			return thisString == str0 || thisString == str1;
		}

		/// <summary>Gets a value indicating whether this string is equal <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible value for <paramref name="thisString" />.</param>
		public static bool EqualsAny(this string thisString, string str0, string str1, string str2)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			return thisString == str0 || thisString == str1 || thisString == str2;
		}

		/// <summary>Gets a value indicating whether this string is equal <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str3">One of the possible value for <paramref name="thisString" />.</param>
		public static bool EqualsAny(this string thisString, string str0, string str1, string str2, string str3)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			Contract.Requires(str3 != null, "str3 must not be null");
			return thisString == str0 || thisString == str1 || thisString == str2 || thisString == str3;
		}

		/// <summary>Gets a value indicating whether this string is equal <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str3">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str4">One of the possible value for <paramref name="thisString" />.</param>
		public static bool EqualsAny(this string thisString, string str0, string str1, string str2, string str3, string str4)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			Contract.Requires(str3 != null, "str3 must not be null");
			Contract.Requires(str4 != null, "str4 must not be null");
			return thisString == str0 || thisString == str1 || thisString == str2 || thisString == str3 || thisString == str4;
		}

		/// <summary>Gets a value indicating whether this string is equal <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str3">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="str4">One of the possible value for <paramref name="thisString" />.</param>
		/// <param name="array">An array containing possible values for <paramref name="thisString" />.</param>
		public static bool EqualsAny(this string thisString, string str0, string str1, string str2, string str3, string str4, params string[] array)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			Contract.Requires(str3 != null, "str3 must not be null");
			Contract.Requires(str4 != null, "str4 must not be null");
			Contract.Requires(array != null, "strs must not be null");
			if (thisString == str0 || thisString == str1 || thisString == str2 || thisString == str3 || thisString == str4)
			{
				return true;
			}
			var length = array.Length;
			for (var i = 0; i < length; i++)
			{
				if (thisString == array[i])
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Gets a value indicating whether <paramref name="hashset" /> contains the string <paramref name="thisString" />.</summary>
		/// <remarks>
		///     This <see cref="EqualsAny(string,string,string)" /> overload can be used to check the value of <paramref name="thisString" /> against a
		///     large number of strings, in a constant time.
		/// </remarks>
		/// <param name="thisString">This string.</param>
		/// <param name="hashset">An hashset containg possible values for this <paramref name="thisString" />.</param>
		public static bool EqualsAny(this string thisString, HashSet<string> hashset)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(hashset != null, "hashset must not be null");
			return hashset.Contains(thisString);
		}

		//
		// StartsWithAny operation
		//

		/// <summary>Gets a value indicating whether this string starts with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible start string for <paramref name="thisString" />.</param>
		public static bool StartsWithAny(this string thisString, string str0, string str1)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			return thisString.StartsWith(str0) || thisString.StartsWith(str1);
		}

		/// <summary>Gets a value indicating whether this string starts with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible start string for <paramref name="thisString" />.</param>
		public static bool StartsWithAny(this string thisString, string str0, string str1, string str2)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			return thisString.StartsWith(str0) || thisString.StartsWith(str1) || thisString.StartsWith(str2);
		}

		/// <summary>Gets a value indicating whether this string starts with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str3">One of the possible start string for <paramref name="thisString" />.</param>
		public static bool StartsWithAny(this string thisString, string str0, string str1, string str2, string str3)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			Contract.Requires(str3 != null, "str3 must not be null");
			return thisString.StartsWith(str0) || thisString.StartsWith(str1) || thisString.StartsWith(str2) || thisString.StartsWith(str3);
		}

		/// <summary>Gets a value indicating whether this string starts with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str3">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str4">One of the possible start string for <paramref name="thisString" />.</param>
		public static bool StartsWithAny(this string thisString, string str0, string str1, string str2, string str3, string str4)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			Contract.Requires(str3 != null, "str3 must not be null");
			Contract.Requires(str4 != null, "str4 must not be null");
			return thisString.StartsWith(str0) || thisString.StartsWith(str1) || thisString.StartsWith(str2) || thisString.StartsWith(str3)
				   || thisString.StartsWith(str4);
		}

		/// <summary>Gets a value indicating whether this string starts with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="thisString">This string.</param>
		/// <param name="str0">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str1">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str2">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str3">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="str4">One of the possible start string for <paramref name="thisString" />.</param>
		/// <param name="array">An array containing possible start strings for <paramref name="thisString" />.</param>
		/// <exception cref="NullReferenceException">null string reference in <paramref name="array" /> not accepted.</exception>
		public static bool StartsWithAny(this string thisString, string str0, string str1, string str2, string str3, string str4, params string[] array)
		{
			Contract.Requires(thisString != null, "str must not be null");
			Contract.Requires(str0 != null, "str0 must not be null");
			Contract.Requires(str1 != null, "str1 must not be null");
			Contract.Requires(str2 != null, "str2 must not be null");
			Contract.Requires(str3 != null, "str3 must not be null");
			Contract.Requires(str4 != null, "str4 must not be null");
			Contract.Requires(array != null, "strs must not be null");
			if (thisString.StartsWith(str0) || thisString.StartsWith(str1) || thisString.StartsWith(str2) || thisString.StartsWith(str3)
				|| thisString.StartsWith(str4))
			{
				return true;
			}
			var length = array.Length;
			for (var i = 0; i < length; i++)
			{
				var str = array[i];
				if (str == null)
				{
					throw new NullReferenceException("null string reference in array not accepted.");
				}
				if (thisString.StartsWith(array[i]))
				{
					return true;
				}
			}
			return false;
		}
	}
}
