namespace Gammtek.Conduit.Text.StringMatching
{
	/// <summary>
	///     Default string matcher which ignores pattern and treats all input strings as matched.
	/// </summary>
	public class DefaultStringMatcher : IStringMatcher
	{
		/// <summary>
		///     Gets or sets pattern as proposed by IStringMatcher interface.
		///     Returns empty strings and ignores value setting.
		/// </summary>
		public string Pattern
		{
			get { return string.Empty; }
			set { }
		}

		/// <summary>
		///     Returns true indicating that all input strings are considered matched.
		/// </summary>
		/// <param name="value">
		///     Value which should be matched against the pattern.
		///     Ignored because this class treats all input strings as successfully matched.
		/// </param>
		/// <returns>Always returns true.</returns>
		public bool IsMatch(string value)
		{
			return true;
		}
	}
}
