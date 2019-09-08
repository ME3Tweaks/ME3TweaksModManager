using System;
using System.Collections.Generic;
using System.Text;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Stateless static class which exposes methods used to help format strings.
	///     This class is internal and cannot be used directly.
	///     It is rather used by other classes declared in the SysExpand.Text.Formatting
	///     namespace to help format strings.
	/// </summary>
	internal static class FormatInfoUtils
	{
		/// <summary>
		///     Default maximum allowed length of the single-lined string produced by verbose format providers.
		/// </summary>
		public static readonly int DefaultMaxFormattedLength = 80;

		/// <summary>
		///     Default string shown when null value should be represented.
		/// </summary>
		public static readonly string DefaultNullFormatted = "null";

		/// <summary>
		///     Maximum depth applied to formatting if no other depth is specified.
		/// </summary>
		public static readonly int DefaultMaximumDepth = 5;

		/// <summary>
		///     String used to report circular reference when formatting string.
		/// </summary>
		public static readonly string InfiniteLoopReport = "<circular reference>";

		/// <summary>
		///     Combines two values of the maximum allowed formatted string length.
		/// </summary>
		/// <param name="length1">First maximum formatted string length; negative value indicates infinite allowed length.</param>
		/// <param name="length2">Second maximum formatted string length; negative value indicates infinite allowed length.</param>
		/// <returns>
		///     Value which is the smaller of the two given lengths where negative input value indicates infinite maximum allowed length;
		///     -1 if both <paramref name="length1" /> and <paramref name="length2" /> are negative.
		/// </returns>
		public static int CombineMaxFormattedLengths(int length1, int length2)
		{
			if (length1 < -1)
			{
				length1 = -1;
			}

			if (length2 < -1)
			{
				length2 = -1;
			}

			var length = length1;
			if (length < 0)
			{
				length = length2;
			}
			else if (length2 >= 0 && length2 < length)
			{
				length = length2;
			}

			return length;
		}

		/// <summary>
		///     Copies formatting-related property values from one verbose formatter to another one.
		///     Use this method to change active formatter so that it formats string in the way
		///     in which other formatter would do, but preserving other settings, like indentation details,
		///     maximum depth when recursively iterating through contents of the object etc.
		/// </summary>
		/// <param name="src">Formatter from which formatting property values are copied.</param>
		/// <param name="dest">Formatter into which formatting property values are copied.</param>
		/// <returns>Reference to <paramref name="dest" /> which is the updated verbose formatter.</returns>
		public static VerboseFormatInfoBase CopyFormatting(VerboseFormatInfoBase src, VerboseFormatInfoBase dest)
		{
			dest.IsMultiLinedFormat = src.IsMultiLinedFormat;
			dest.FieldDelimiter = src.FieldDelimiter;
			dest.LinePrefix = src.LinePrefix;
			dest.IndentationString = src.IndentationString;
			dest.RightMostIndentationString = src.RightMostIndentationString;
			dest.LastIndentationString = src.LastIndentationString;
			dest.LastRightMostIndentationString = src.LastRightMostIndentationString;

			return dest;
		}

		/// <summary>
		///     Creates new instance of verbose formatter which formats single-lined strings.
		/// </summary>
		/// <returns>New instance of verbose formatter which can be used to format single-lined strings.</returns>
		public static VerboseFormatInfoBase CreateSingleLinedFormatter()
		{
			return new VerboseFormatInfo(); // By default verbose formatters are single-lined
		}

		/// <summary>
		///     Creates new instance of verbose formatter which can be applied to format multi-lined string without any decorations.
		///     This format is applicable to objets of medium complexity. Indentation is performed using multiple whitespace characters.
		/// </summary>
		/// <returns>
		///     New instance of verbose formatter which can be used to format multi-lined strings
		///     in which indentation is performed using whitespace characters.
		/// </returns>
		public static VerboseFormatInfoBase CreateMultiLinedFormatter()
		{
			var vfi = new VerboseFormatInfo();

			vfi.FieldDelimiter = Environment.NewLine;
			vfi.IsMultiLinedFormat = true;

			vfi.IndentationString = vfi.RightMostIndentationString = vfi.LastIndentationString = vfi.LastRightMostIndentationString = "    ";
			vfi.MaximumFormattedLength = DefaultMaxFormattedLength;

			return vfi;
		}

		/// <summary>
		///     Creates new instance of verbose formatter which can be applied to format multi-lined strings without any decorations.
		///     This format is applicable to objects of medium complexity. Indentation is performed using horizontal tab characters.
		/// </summary>
		/// <returns>
		///     New instance of verbose formatter which can be used to format multi-lined strings
		///     with indentation implemented using horizontal tab characters.
		/// </returns>
		public static VerboseFormatInfoBase CreateTabbedMultiLinedFormatter()
		{
			var vfi = CreateMultiLinedFormatter();
			vfi.IndentationString = vfi.RightMostIndentationString = vfi.LastIndentationString = vfi.LastRightMostIndentationString = "\t";

			return vfi;
		}

		/// <summary>
		///     Creates new instance of verbose formatter which can be applied to format multi-lined string which is decorated
		///     as to resemble the look of a tree structure. This format is applicable to complex objects.
		/// </summary>
		/// <returns>New instance of verbose formatter which can be used to format tree-like multi-lined strings.</returns>
		public static VerboseFormatInfoBase CreateTreeMultiLinedFromatter()
		{
			var vfi = CreateMultiLinedFormatter();

			vfi.IndentationString = " |   ";
			vfi.RightMostIndentationString = " |-- ";
			vfi.LastIndentationString = " |   ";
			vfi.LastRightMostIndentationString = " +-- ";

			return vfi;
		}

		/// <summary>
		///     Creates new instance of verbose formatter which can be applied to format simplest possible strings.
		///     Only the first level of depth is presented in the formatted string and format is single-lined.
		///     This format is applicable to objects of unpredictable complexity, which should be presented by strings of limited length.
		/// </summary>
		/// <returns>New instance of verbose formatter which can be applied to format shortest possible single-lined strings.</returns>
		public static VerboseFormatInfoBase CreateSimpleFormatter()
		{
			var vfi = CreateSingleLinedFormatter();
			vfi.MaximumDepth = 1;
			return vfi;
		}

		/// <summary>
		///     Attempts to format string which represents given object using series of verbose format providers. Operation ends in success
		///     when the first available format provider successfully formats the string. Operation ends in failure if none of the format providers
		///     is able to successfully format the string. Use this method to implement two-pass formatting, where in first pass single-lined
		///     formatting is attempted with restricted destination string length and in second pass multi-lined formatting without length
		///     restrictions is performed.
		/// </summary>
		/// <param name="sb">String builder to which formatted string is appended.</param>
		/// <param name="format">Format string containing formatting specifications.</param>
		/// <param name="arg">An object to format.</param>
		/// <param name="formatProviders">
		///     Series of format providers which are contacted one at a time until the first performs
		///     formatting successfully.
		/// </param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter. Formatting will fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters will ignore this parameter.
		///     Negative value indicates that formatter has unlimited space available.
		///     On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if representation of <paramref name="arg" /> has been successfully appended to <paramref name="sb" />
		///     within given number of allowed characters using any of the given format providers; otherwise false.
		/// </returns>
		public static bool BestTryFormat(StringBuilder sb, string format, object arg, IEnumerable<VerboseFormatInfoBase> formatProviders,
			ref int maxLength)
		{
			var success = false;

			foreach (var formatProvider in formatProviders)
			{
				var startingLength = sb.Length;
				var prevMaxLength = maxLength;

				formatProvider.CombineMaximumFormattedLength(maxLength);
				var combinedMaxLength = formatProvider.MaximumFormattedLength;

				success = formatProvider.Format(sb, format, arg, formatProvider, ref combinedMaxLength);

				if (!success)
				{
					sb.Length = startingLength;
					maxLength = prevMaxLength;
				}
				else
				{
					if (maxLength >= 0)
					{
						maxLength -= sb.Length - startingLength;
					}
					break;
				}
			}
			return success;
		}

		/// <summary>
		///     Tries to append string to the string builder within given amount of characters allowed. Always succeeds in multi-lined formatters.
		/// </summary>
		/// <param name="formatter">Formatter which has requested character to be appended.</param>
		/// <param name="sb">String builder to which string should be appended.</param>
		/// <param name="s">String which should be appended.</param>
		/// <param name="success">
		///     Indicates whether appending operations this far have been successful (true)
		///     or some of the previous append operations has already failed (false). If false, appending will not be attempted.
		/// </param>
		/// <param name="maxLength">
		///     On input contains maximum number of characters that are allowed to be appended to the string builder.
		///     On output indicates remaining character positions allowed before appending will fail.
		/// </param>
		/// <returns>
		///     true if appending operation was successful, i.e. allowed number of characters
		///     has not been breached; otherwise false.
		/// </returns>
		public static bool TryAppendString(VerboseFormatInfoBase formatter, StringBuilder sb, string s, bool success, ref int maxLength)
		{
			if (s == null)
			{
				s = string.Empty;
			}

			if (success && (formatter.IsMultiLinedFormat || maxLength < 0 || maxLength >= s.Length))
			{
				sb.Append(s);
				if (maxLength > 0)
				{
					maxLength -= s.Length;
				}
			}
			else
			{
				success = false;
			}

			return success;
		}

		/// <summary>
		///     Tries to append character to the string builder within given amount of characters allowed. Always succeeds in multi-lined formatters.
		/// </summary>
		/// <param name="formatter">Formatter which has requested character to be appended.</param>
		/// <param name="sb">String builder to which string should be appended.</param>
		/// <param name="c">Character which should be appended.</param>
		/// <param name="success">
		///     Indicates whether appending operations this far have been successful (true)
		///     or some of the previous append operations has already failed (false). If false, appending will not be attempted.
		/// </param>
		/// <param name="maxLength">
		///     On input contains maximum number of characters that are allowed to be appended to the string builder.
		///     On output indicates remaining character positions allowed before appending will fail.
		/// </param>
		/// <returns>true if appending operation was successful within given number of allowed characters; otherwise false.</returns>
		public static bool TryAppendChar(VerboseFormatInfoBase formatter, StringBuilder sb, char c, bool success, ref int maxLength)
		{
			if (success && (formatter.IsMultiLinedFormat || maxLength < 0 || maxLength > 0))
			{
				sb.Append(c);
				if (maxLength > 0)
				{
					maxLength--;
				}
			}
			else
			{
				success = false;
			}

			return success;
		}

		/// <summary>
		///     Tries to append single white space if string builder doesn't end with space or new line character.
		///     Use this method to separate successive items appended to string builder.
		/// </summary>
		/// <param name="formatter">Formatter which has requested character to be appended.</param>
		/// <param name="sb">String builder to which whitespace is appended.</param>
		/// <param name="success">
		///     Indicates whether appending operations this far have been successful (true)
		///     or some of the previous append operations has already failed (false). If false, appending will not be attempted.
		/// </param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter. Formatting will fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters will ignore this parameter.
		///     Negative value indicates that formatter has unlimited space available. On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if space has been successfully appended to <paramref name="sb" /> within given number of allowed characters
		///     or there was no need to append space; otherwise false.
		/// </returns>
		public static bool TryAppendSpaceIfNeeded(VerboseFormatInfoBase formatter, StringBuilder sb, bool success, ref int maxLength)
		{
			if (success)
			{
				var originalLength = sb.Length;

				var c = (sb.Length > 0 ? sb[sb.Length - 1] : '\0');
				if (c != '\r' && c != '\n' && c != ' ' && c != '\t')
				{
					success = TryAppendChar(formatter, sb, ' ', success, ref maxLength);
				}

				if (!success)
				{
					sb.Length = originalLength;
				}
			}

			return success;
		}

		/// <summary>
		///     Appends information that specified argument produces infinite loop to the given string builder.
		/// </summary>
		/// <param name="sb">String builder to which information about the argument is appended.</param>
		/// <param name="arg">
		///     Value which has already been appended to <paramref name="sb" /> in the current line of
		///     references and should not be visited again or infinite loop would be formed.
		/// </param>
		/// <param name="argName">Optional name of the instance which is reported, if available.</param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter. Formatting will fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters will ignore this parameter.
		///     Negative value indicates that formatter has unlimited space available. On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if contents have been successfully appended to <paramref name="sb" />
		///     within given number of allowed characters; otherwise false.
		/// </returns>
		public static bool ReportInfiniteLoop(StringBuilder sb, object arg, string argName, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			if (arg != null)
			{
				var dataType = arg.GetType();
				var vfi = CreateSingleLinedFormatter();

				success = success && vfi.AppendFriendlyTypeName(dataType, arg, sb, ref maxLength);

				if (!string.IsNullOrEmpty(argName))
				{
					if (sb.Length > originalLength)
					{
						success = TryAppendChar(vfi, sb, ' ', success, ref maxLength);
					}
					success = TryAppendString(vfi, sb, argName, success, ref maxLength);
				}

				if (sb.Length > originalLength)
				{
					success = TryAppendChar(vfi, sb, ' ', success, ref maxLength);
				}
				success = TryAppendString(vfi, sb, InfiniteLoopReport, success, ref maxLength);
			}

			if (!success)
			{
				sb.Length = originalLength;
			}

			return success;
		}
	}
}
