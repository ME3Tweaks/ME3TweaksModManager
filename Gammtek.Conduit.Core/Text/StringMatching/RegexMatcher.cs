using System;
using System.Text.RegularExpressions;

namespace Gammtek.Conduit.Text.StringMatching
{
	/// <summary>
	///     Implements string matching algorithm which tries to match regular expression pattern
	///     with input strings.
	/// </summary>
	public class RegexMatcher : IStringMatcher
	{
		/// <summary>
		///     Regular expression pattern against which input strings are matched.
		/// </summary>
		private string _pattern;

		/// <summary>
		///     Default constructor.
		/// </summary>
		public RegexMatcher() {}

		/// <summary>
		///     Constructor which initializes regular expression pattern against which input strings are matched.
		/// </summary>
		/// <param name="pattern">Regular expression pattern used to match input strings.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="pattern" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="pattern" /> is not a valid regular expression pattern.</exception>
		public RegexMatcher(string pattern)
		{
			_pattern = pattern;
		}

		/// <summary>
		///     Gets or sets regular expression pattern against which input strings are matched.
		/// </summary>
		/// <exception cref="System.ArgumentNullException">Attempted to set null value.</exception>
		/// <exception cref="ArgumentException">Attempted to set value which is not a valid regular expression pattern.</exception>
		public string Pattern
		{
			get { return _pattern ?? string.Empty; }
			set
			{
				TestPattern(value, true);
				_pattern = value;
			}
		}

		/// <summary>
		///     Tests whether <paramref name="valeu" /> can be matched with <see cref="Pattern" />.
		/// </summary>
		/// <param name="value">
		///     String which is tested using the regular expression pattern
		///     stored in the <see cref="Pattern" /> property.
		/// </param>
		/// <returns>
		///     true if <paramref name="value" /> can be matched using regular expression pattern
		///     set to <see cref="Pattern" /> property; otherwise false.
		/// </returns>
		/// <exception cref="System.InvalidOperationException"><see cref="Pattern" /> is null or empty.</exception>
		public bool IsMatch(string value)
		{
			if (string.IsNullOrEmpty(_pattern))
			{
				throw new InvalidOperationException("Cannot match strings before regular expression pattern is set.");
			}

			var reg = new Regex(_pattern);

			return reg.IsMatch(value);
		}

		/// <summary>
		///     Tests whether <paramref name="pattern" /> is a valid regular expression pattern.
		/// </summary>
		/// <param name="pattern">Pattern which should be tested.</param>
		/// <param name="throwException">
		///     Indicates whether exception should be throw if <paramref name="pattern" /> is not
		///     a valid regular expression pattern (true) or exception should not be thrown (false).
		/// </param>
		/// <returns>true if <paramref name="pattern" /> is a valid regular expression pattern; otherwise false.</returns>
		/// <exception cref="System.ArgumentNullException"><paramref name="pattern" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="pattern" /> is not a valid regular expression pattern.</exception>
		private bool TestPattern(string pattern, bool throwException)
		{
			var valid = true;

			try
			{
				var reg = new Regex(pattern);
			}
			catch (Exception ex)
			{
				valid = false;
				if (throwException)
				{
					throw new ArgumentException("pattern is not a valid regular expression pattern.", ex);
				}
			}

			return valid;
		}
	}
}
