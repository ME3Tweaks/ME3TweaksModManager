namespace Gammtek.Conduit.Text.StringMatching
{
	/// <summary>
	///     Types implementing this interface can be used to test whether
	///     string matches given pattern when particular matching rules are applied.
	/// </summary>
	public interface IStringMatcher
	{
		/// <summary>
		///     Pattern against which strings are matched.
		/// </summary>
		string Pattern { get; set; }

		/// <summary>
		///     Tests whether <paramref name="value" /> is matched by the pattern specified
		///     by the <see cref="Pattern" /> property.
		/// </summary>
		/// <param name="value">Value which is tested for matching with pattern stored in this instance.</param>
		/// <returns>true if <paramref name="value" /> can be matched with <see cref="Pattern" />; otherwise false.</returns>
		bool IsMatch(string value);
	}
}
