namespace Gammtek.Conduit.Text.StringMatching
{
	/// <summary>
	///     Implements string matching algorithm which tries to match pattern containing wildcard characters
	///     with the given input string.
	/// </summary>
	public class WildcardMatcher : IStringMatcher
	{
		/// <summary>
		///     Default wildcard character used to map single character from input string.
		/// </summary>
		public const char DefaultSingleWildcard = '?';

		/// <summary>
		///     Default wildcard character used to map zero or more consecutive characters from input string.
		/// </summary>
		public const char DefaultMultipleWildcard = '*';

		/// <summary>
		///     Wildcard character used to match zero or more consecutive characters in the input string.
		///     By default this value is asterisk (*).
		/// </summary>
		private char _multipleWildcard;

		/// <summary>
		///     Pattern against which input strings are matched; may contain wildcard characters.
		/// </summary>
		private string _pattern;

		/// <summary>
		///     Default constructor.
		/// </summary>
		public WildcardMatcher()
			: this(null, DefaultSingleWildcard, DefaultMultipleWildcard) {}

		/// <summary>
		///     Constructor which initializes pattern against which input strings are matched.
		/// </summary>
		/// <param name="pattern">Pattern used to match input strings.</param>
		public WildcardMatcher(string pattern)
			: this(pattern, DefaultSingleWildcard, DefaultMultipleWildcard) {}

		/// <summary>
		///     Constructor which initializes pattern against which input strings are matched and
		///     wildcard characters used in string matching.
		/// </summary>
		/// <param name="pattern">Pattern against which input strings are matched.</param>
		/// <param name="singleWildcard">Wildcard character used to replace single character in input strings.</param>
		/// <param name="multipleWildcard">Wildcard character used to replace zero or more consecutive characters in input strings.</param>
		public WildcardMatcher(string pattern, char singleWildcard, char multipleWildcard)
		{
			_pattern = pattern;
			SingleWildcard = singleWildcard;
			_multipleWildcard = multipleWildcard;
		}

		/// <summary>
		///     Gets or sets wildcard character which is used to replace exactly one character
		///     in the input string (default is question mark - ?).
		/// </summary>
		public char SingleWildcard { get; set; }

		/// <summary>
		///     Gets or sets wildcard character which is used to replace zero or more characters
		///     in the input string (default is asterisk - *).
		/// </summary>
		public char MultipleWildcard
		{
			get { return _multipleWildcard; }
			set { _multipleWildcard = value; }
		}

		/// <summary>
		///     Gets or sets pattern against which input strings are mapped.
		///     Pattern may contain wildcard characters specified by <see cref="SingleWildcard" />
		///     and <see cref="MultipleWildcard" /> properties.
		///     Returns empty string if pattern has not been set or it was set to null value.
		/// </summary>
		public string Pattern
		{
			get { return _pattern ?? string.Empty; }
			set { _pattern = value; }
		}

		/// <summary>
		///     Tries to match <paramref name="value" /> against <see cref="Pattern" /> value stored in this instance.
		/// </summary>
		/// <param name="value">String which should be matched against the contained pattern.</param>
		/// <returns>true if <paramref name="value" /> can be matched with <see cref="Pattern" />; otherwise false.</returns>
		public bool IsMatch(string value)
		{
			var inputPosStack = new int[(value.Length + 1) * (Pattern.Length + 1)];
			// Stack containing input positions that should be tested for further matching
			var patternPosStack = new int[inputPosStack.Length]; // Stack containing pattern positions that should be tested for further matching
			var stackPos = -1; // Points to last occupied entry in stack; -1 indicates that stack is empty
			var pointTested = new bool[value.Length + 1, Pattern.Length + 1];
			// Each true value indicates that input position vs. pattern position has been tested

			var inputPos = 0; // Position in input matched up to the first multiple wildcard in pattern
			var patternPos = 0; // Position in pattern matched up to the first multiple wildcard in pattern

			if (_pattern == null)
			{
				_pattern = string.Empty;
			}

			// Match beginning of the string until first multiple wildcard in pattern
			while (inputPos < value.Length && patternPos < Pattern.Length && Pattern[patternPos] != MultipleWildcard &&
				   (value[inputPos] == Pattern[patternPos] || Pattern[patternPos] == SingleWildcard))
			{
				inputPos++;
				patternPos++;
			}

			// Push this position to stack if it points to end of pattern or to a general wildcard character
			if (patternPos == _pattern.Length || _pattern[patternPos] == _multipleWildcard)
			{
				pointTested[inputPos, patternPos] = true;
				inputPosStack[++stackPos] = inputPos;
				patternPosStack[stackPos] = patternPos;
			}

			var matched = false;

			// Repeat matching until either string is matched against the pattern or no more parts remain on stack to test
			while (stackPos >= 0 && !matched)
			{
				inputPos = inputPosStack[stackPos]; // Pop input and pattern positions from stack
				patternPos = patternPosStack[stackPos--]; // Matching will succeed if rest of the input string matches rest of the pattern

				if (inputPos == value.Length && patternPos == Pattern.Length)
				{
					matched = true; // Reached end of both pattern and input string, hence matching is successful
				}
				else if (patternPos == Pattern.Length - 1)
				{
					matched = true; // Current pattern character is multiple wildcard and it will match all the remaining characters in the input string
				}
				else
				{
					// First character in next pattern block is guaranteed to be multiple wildcard
					// So skip it and search for all matches in value string until next multiple wildcard character is reached in pattern

					for (var curInputStart = inputPos; curInputStart < value.Length; curInputStart++)
					{
						var curInputPos = curInputStart;
						var curPatternPos = patternPos + 1;

						while (curInputPos < value.Length && curPatternPos < Pattern.Length && Pattern[curPatternPos] != MultipleWildcard &&
							   (value[curInputPos] == Pattern[curPatternPos] || Pattern[curPatternPos] == SingleWildcard))
						{
							curInputPos++;
							curPatternPos++;
						}

						// If we have reached next multiple wildcard character in pattern without breaking the matching sequence, then we have another candidate for full match
						// This candidate should be pushed to stack for further processing
						// At the same time, pair (input position, pattern position) will be marked as tested, so that it will not be pushed to stack later again
						if (((curPatternPos == Pattern.Length && curInputPos == value.Length) ||
							 (curPatternPos < Pattern.Length && Pattern[curPatternPos] == MultipleWildcard))
							&& !pointTested[curInputPos, curPatternPos])
						{
							pointTested[curInputPos, curPatternPos] = true;
							inputPosStack[++stackPos] = curInputPos;
							patternPosStack[stackPos] = curPatternPos;
						}
					}
				}
			}

			return matched;
		}
	}
}
