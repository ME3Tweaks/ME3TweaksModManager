namespace Gammtek.Conduit.Text.StringMatching
{
	/// <summary>
	///     Implements IStringMatcher interface in such way that string matches the pattern
	///     only if it is exactly the same as the pattern.
	/// </summary>
	public class ExactMatcher : IStringMatcher
	{
		/// <summary>
		///     Pattern with which input string must be equal to be matched.
		/// </summary>
		private string _pattern;

		/// <summary>
		///     Default constructor.
		/// </summary>
		public ExactMatcher() {}

		/// <summary>
		///     Constructor which initializes search pattern.
		/// </summary>
		/// <param name="pattern">Pattern against which input strings are matched.</param>
		public ExactMatcher(string pattern)
		{
			_pattern = pattern;
		}

		/// <summary>
		///     Gets or sets pattern to which input strings are compared.
		///     Returns empty set if pattern is not set or is set to null value.
		/// </summary>
		public string Pattern
		{
			get { return _pattern ?? string.Empty; }
			set { _pattern = value; }
		}

		/// <summary>
		///     Tests whether <paramref name="value" /> equals stored pattern or not.
		/// </summary>
		/// <param name="value">String which is compared with stored pattern. Value null is treated as empty string.</param>
		/// <returns>true if <paramref name="value" /> is equal to <see cref="Pattern" />; otherwise false.</returns>
		public bool IsMatch(string value)
		{
			return Pattern == (value ?? string.Empty);
		}
	}
}
