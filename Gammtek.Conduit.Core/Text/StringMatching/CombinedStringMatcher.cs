using System;
using System.Collections.Generic;
using System.Text;

namespace Gammtek.Conduit.Text.StringMatching
{
	/// <summary>
	///     Combines multiple string matchers. Input string is matched if matched by any of the contained matchers.
	/// </summary>
	public class CombinedStringMatcher : IStringMatcher
	{
		/// <summary>
		///     List of contained matchers.
		/// </summary>
		private readonly List<IStringMatcher> _matchers;

		/// <summary>
		///     Default constructor.
		/// </summary>
		public CombinedStringMatcher()
		{
			_matchers = new List<IStringMatcher>();
		}

		/// <summary>
		///     Constructor which initializes list of contained string matchers.
		/// </summary>
		/// <param name="matchers">Array of matchers that should be added to this matcher.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="matchers" /> is null or contains null reference.</exception>
		public CombinedStringMatcher(IStringMatcher[] matchers)
			: this()
		{
			AddRange(matchers);
		}

		/// <summary>
		///     Gets combined patterns of contained matchers. Throws exception if attempted to set pattern.
		/// </summary>
		/// <exception cref="System.InvalidOperationException">Attempted to set pattern.</exception>
		public string Pattern
		{
			get
			{
				var sb = new StringBuilder();
				var first = true;

				foreach (var matcher in _matchers)
				{
					sb.AppendFormat("{0}({1})", first ? string.Empty : " OR ", matcher.Pattern);
					first = false;
				}

				return sb.ToString();
			}
			set { throw new InvalidOperationException("Cannot set pattern on CombinedStringMatcher."); }
		}

		/// <summary>
		///     Tests whether specified string can be matched using any of the contained matchers.
		/// </summary>
		/// <param name="value">String which is tested against contained matchers.</param>
		/// <returns>true if any of the contained matchers can match <paramref name="value" />; otherwise false.</returns>
		public bool IsMatch(string value)
		{
			var matched = false;

			foreach (var matcher in _matchers)
			{
				if (matcher.IsMatch(value))
				{
					matched = true;
					break;
				}
			}

			return matched;
		}

		/// <summary>
		///     Adds new matcher to the list of contained matchers.
		/// </summary>
		/// <param name="matcher">New matcher to add to the list.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="matcher" /> is null.</exception>
		public void Add(IStringMatcher matcher)
		{
			if (matcher == null)
			{
				throw new ArgumentNullException("Cannot add null reference string matcher.");
			}

			_matchers.Add(matcher);
		}

		/// <summary>
		///     Adds multiple matchers to the set of contained matchers.
		/// </summary>
		/// <param name="matchers">Array of matchers that should be added.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="matchers" /> is null or contains null reference.</exception>
		public void AddRange(IStringMatcher[] matchers)
		{
			if (matchers == null)
			{
				throw new ArgumentNullException("Array of matchers cannot be null.");
			}

			for (var i = 0; i < matchers.Length; i++)
			{
				Add(matchers[i]); // May throw new System.ArgumentNullException
			}
		}
	}
}
