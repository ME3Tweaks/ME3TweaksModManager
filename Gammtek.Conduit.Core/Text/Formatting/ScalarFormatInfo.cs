using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Provides formatting services for scalar data types and for strings.
	///     These types include numeric types, Boolean, characters, date time, enumerations and strings.
	///     VerboseFormatInfo delegates calls to this class when one of the listed types is formatted.
	///     This class is internal and cannot be used directly.
	/// </summary>
	internal class ScalarFormatInfo : VerboseFormatInfoBase
	{
		/// <summary>
		///     Lists all data types that are supported by this format provider.
		/// </summary>
		private static readonly Type[] SupportedTypes =
		{
			typeof (SByte), typeof (Byte),
			typeof (Int16), typeof (UInt16),
			typeof (Int32), typeof (UInt32),
			typeof (Int64), typeof (UInt64),
			typeof (float), typeof (Double), typeof (Decimal),
			typeof (char), typeof (String),
			typeof (Boolean), typeof (DateTime)
		};

		/// <summary>
		///     Minimum number of characters that should be occupied by the formatted value.
		/// </summary>
		private int _fieldLength;

		/// <summary>
		///     Default constructor.
		/// </summary>
		public ScalarFormatInfo() {}

		/// <summary>
		///     Copy constructor which copies only values that are common to all classes derived from VerboseFormatInfoBase.
		/// </summary>
		/// <param name="other">Instance from which contents is copied to new instance of this class.</param>
		public ScalarFormatInfo(VerboseFormatInfoBase other)
			: base(other) {}

		/// <summary>
		///     Copy constructor which initializes new instance and copies common values from another instance of this class.
		/// </summary>
		/// <param name="other">Instance from which common values should be copied.</param>
		public ScalarFormatInfo(ScalarFormatInfo other)
			: base(other)
		{
			_fieldLength = other._fieldLength;
		}

		/// <summary>
		///     Gets or sets minimum number of characters that should be used when formatting the value.
		///     Ignored if set to non-positive value.
		/// </summary>
		internal int FieldLength
		{
			get { return _fieldLength; }
			set { _fieldLength = (value < 0 ? 0 : value); }
		}

		/// <summary>
		///     Converts the value of a specified object to an equivalent string representation using
		///     specified format and culture-specific formatting information.
		/// </summary>
		/// <param name="sb">String builder to which formatted string should be appended.</param>
		/// <param name="format">Format string containing formatting specifications.</param>
		/// <param name="arg">An object to format.</param>
		/// <param name="formatProvider">An object that supplies format information about the current instance.</param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter. Formatting will fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters will ignore this parameter.
		///     Negative value indicates that formatter has unlimited space available. On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if representation of <paramref name="arg" /> has been successfully appended to <paramref name="sb" />
		///     within given number of allowed characters; otherwise false.
		/// </returns>
		internal override bool Format(StringBuilder sb, string format, object arg, IFormatProvider formatProvider, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			if (PushCurrentObject(arg))
			{
				if (ShowDataType)
				{
					success = success && AppendInstanceTypeName(arg, sb, ref maxLength);
				}

				if (!string.IsNullOrEmpty(InstanceName))
				{
					var name = string.Format("{0}{1}{2}={2}", sb.Length > originalLength ? " " : "", InstanceName, IsMultiLinedFormat ? " " : "");
					success = FormatInfoUtils.TryAppendString(this, sb, name, success, ref maxLength);
				}
				else if (sb.Length > originalLength)
				{
					success = FormatInfoUtils.TryAppendChar(this, sb, ' ', success, ref maxLength);
				}

				var argType = GetInstanceType(arg);

				if (arg == null)
				{
					success = FormatInfoUtils.TryAppendString(this, sb, FormatInfoUtils.DefaultNullFormatted, success, ref maxLength);
				}
				else if (arg is char)
				{
					success = success && FormatChar(sb, (char) arg, true, false, ref maxLength);
				}
				else if (arg is string)
				{
					success = success && FormatString(sb, (string) arg, true, ref maxLength);
				}
				else if (arg is Boolean)
				{
					success = FormatInfoUtils.TryAppendString(this, sb, ((bool) arg) ? "true" : "false", success, ref maxLength);
				}
				else if (argType.IsEnum)
				{
					var plainName = Enum.GetName(argType, arg);

					if (plainName != null)
					{
						success = FormatInfoUtils.TryAppendString(this, sb, plainName, success, ref maxLength);
					}
					else
					{
						success = success && AppendEnumFlags((Enum) arg, sb, ref maxLength); // arg is an OR-ed combination of values
					}
				}
				else
				{
					success = FormatInfoUtils.TryAppendString(this, sb, arg.ToString(), success, ref maxLength);
				}

				while (success && sb.Length - originalLength < _fieldLength)
				{
					success = FormatInfoUtils.TryAppendChar(this, sb, ' ', success, ref maxLength);
				}

				PopCurrentObject();
			}
			else
			{
				success = success && FormatInfoUtils.ReportInfiniteLoop(sb, arg, InstanceName, ref maxLength);
			}

			if (!success)
			{
				sb.Length = originalLength;
			}

			return success;
		}

		/// <summary>
		///     Appends formatted presentation of a given character to the given string builder.
		/// </summary>
		/// <param name="sb">String builder to which formatted character is appended.</param>
		/// <param name="c">Character which should be appended to the string builder.</param>
		/// <param name="quote">Indicates whether quotation marks should be placed around the character (true) or not (false).</param>
		/// <param name="stringEscaping">
		///     Indicates whether escaping rules for strings should be applied (true) or only simple escaping
		///     should be applied (false). String escaping includes double quotes and backslash characters escaping.
		/// </param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter. Formatting will fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters will ignore this parameter.
		///     Negative value indicates that formatter has unlimited space available. On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if representation of <paramref name="arg" /> has been successfully appended to <paramref name="sb" />
		///     within given number of allowed characters; otherwise false.
		/// </returns>
		private bool FormatChar(StringBuilder sb, char c, bool quote, bool stringEscaping, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			var quotationMark = (quote ? "'" : string.Empty);

			if (c == '\n')
			{
				success = FormatInfoUtils.TryAppendString(this, sb, string.Format("{0}\\n{0}", quotationMark), success, ref maxLength);
			}
			else if (c == '\r')
			{
				success = FormatInfoUtils.TryAppendString(this, sb, string.Format("{0}\\r{0}", quotationMark), success, ref maxLength);
			}
			else if (c == '\t')
			{
				success = FormatInfoUtils.TryAppendString(this, sb, string.Format("{0}\\t{0}", quotationMark), success, ref maxLength);
			}
			else if (c < ' ')
			{
				success = FormatInfoUtils.TryAppendString(this, sb, string.Format("0x{0:X2}", (int) c), success, ref maxLength);
			}
			else if (!stringEscaping && c == '\'')
			{
				success = FormatInfoUtils.TryAppendString(this, sb, string.Format("{0}\\'{0}", quotationMark), success, ref maxLength);
			}
			else if (stringEscaping && c == '\\')
			{
				success = FormatInfoUtils.TryAppendString(this, sb, string.Format("{0}\\\\{0}", quotationMark), success, ref maxLength);
			}
			else if (stringEscaping && c == '"')
			{
				success = FormatInfoUtils.TryAppendString(this, sb, string.Format("{0}\\\"{0}", quotationMark), success, ref maxLength);
			}
			else
			{
				success = FormatInfoUtils.TryAppendString(this, sb, string.Format("{1}{0}{1}", c, quotationMark), success, ref maxLength);
			}

			if (!success)
			{
				sb.Length = originalLength;
			}

			return success;
		}

		/// <summary>
		///     Appends formatted string to the given string builder. String will be fully escaped.
		/// </summary>
		/// <param name="sb">String builder to which string should be appended.</param>
		/// <param name="s">String which should be formatted and appended to the string builder.</param>
		/// <param name="quote">Indicates whether formatted string should be enclosed in double quotation marks (true) or not (false).</param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter. Formatting will fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters will ignore this parameter.
		///     Negative value indicates that formatter has unlimited space available. On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if representation of <paramref name="arg" /> has been successfully appended to <paramref name="sb" />
		///     within given number of allowed characters; otherwise false.
		/// </returns>
		private bool FormatString(StringBuilder sb, string s, bool quote, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			if (quote)
			{
				success = FormatInfoUtils.TryAppendChar(this, sb, '"', success, ref maxLength);
			}

			for (var i = 0; success && i < s.Length; i++)
			{
				success = FormatChar(sb, s[i], false, true, ref maxLength);
			}

			if (quote)
			{
				success = FormatInfoUtils.TryAppendChar(this, sb, '"', success, ref maxLength);
			}

			if (!success)
			{
				sb.Length = originalLength;
			}

			return success;
		}

		/// <summary>
		///     Appends names of enumeration constants that are present in the OR-ed value.
		/// </summary>
		/// <param name="value">Enumeration value which is possibly a combination of multiple OR-ed values.</param>
		/// <param name="sb">String builder to which enumeration value names should be appended.</param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter. Formatting will fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters will ignore this parameter.
		///     Negative value indicates that formatter has unlimited space available. On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if representation of <paramref name="arg" /> has been successfully appended to <paramref name="sb" />
		///     within given number of allowed characters; otherwise false.
		/// </returns>
		private bool AppendEnumFlags(Enum value, StringBuilder sb, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			var enumType = value.GetType();
			var underlyingType = Enum.GetUnderlyingType(enumType);

			// The following code tries to recover flags in a flagged enum value
			// in .NET 4.0 this can be done much easier using Enum.HasFlag instance-level method, but with some unpredictable outcomes
			// e.g. if enumeration defines Left, Right and Both=Left | Right; then Both might be shown as Left | Right | Both which is avoided by this function

			var isInt64 = false;
			var isUint64 = false;
			Int64 int64Value = 0;
			UInt64 uint64Value = 0;

			if (underlyingType == typeof (sbyte) || underlyingType == typeof (Int16) || underlyingType == typeof (Int32) ||
				underlyingType == typeof (Int64))
			{
				isInt64 = true;
				int64Value = ((IConvertible) value).ToInt64(null);
			}
			else if (underlyingType == typeof (byte) || underlyingType == typeof (UInt16) || underlyingType == typeof (UInt32) ||
					 underlyingType == typeof (UInt64))
			{
				isUint64 = true;
				uint64Value = ((IConvertible) value).ToUInt64(null);
			}

			Int64 constructedInt64Value = 0;
			UInt64 constructedUint64Value = 0;

			var values = Enum.GetValues(enumType);
			var constructionComplete = false;
			var flags = new List<object>(); // Enumeration values that are incorporated in OR-ed value

			foreach (var simpleValue in values)
			{
				if (isInt64)
				{
					var curInt64Value = ((IConvertible) simpleValue).ToInt64(null);

					if ((int64Value & curInt64Value) == curInt64Value && (constructedInt64Value & curInt64Value) != curInt64Value)
					{
						// simpleValue is part of the resulting OR-ed value and is not included in currently constructed value (or is not completely included)

						constructedInt64Value = constructedInt64Value | curInt64Value;
						flags.Add(simpleValue);

						if (constructedInt64Value == int64Value)
						{
							constructionComplete = true;
							break;
						}
					}
				}
				else if (isUint64)
				{
					var curUint64Value = ((IConvertible) simpleValue).ToUInt64(null);

					if ((uint64Value & curUint64Value) == curUint64Value && (constructedUint64Value & curUint64Value) != curUint64Value)
					{
						// simpleValue is part of the resulting OR-ed value and is not included in currently constructed value (or is not completely included)

						constructedUint64Value = constructedUint64Value | curUint64Value;
						flags.Add(simpleValue);

						if (constructedUint64Value == uint64Value)
						{
							constructionComplete = true;
							break;
						}
					}
				}
			}

			if (!constructionComplete)
			{
				success = FormatInfoUtils.TryAppendString(this, sb, value.ToString(), success, ref maxLength);
				// Append it as number or whatever Enum.ToString() makes out of this value
			}
			else
			{
				var isFirst = true;

				foreach (var simpleValue in flags)
				{
					if (!isFirst)
					{
						success = FormatInfoUtils.TryAppendChar(this, sb, '+', success, ref maxLength);
					}
					isFirst = false;

					success = FormatInfoUtils.TryAppendString(this, sb, Enum.GetName(enumType, simpleValue), success, ref maxLength);

					if (!success)
					{
						break;
					}
				}
			}

			if (!success)
			{
				sb.Length = originalLength;
			}

			return success;
		}

		/// <summary>
		///     Appends user friendly name for the given data type and appends it to string builder.
		/// </summary>
		/// <param name="type">Data type for which user friendly name is requested.</param>
		/// <param name="instance">
		///     Instance for which friendly type name is appended.
		///     Use this argument to gather additional information which might not be available from the type information.
		///     This argument may be null if instance is not available.
		/// </param>
		/// <param name="sb">String builder to which data type name should be appended.</param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter. Formatting will fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters will ignore this parameter.
		///     Negative value indicates that formatter has unlimited space available.
		///     On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if representation of <paramref name="arg" /> has been successfully appended to <paramref name="sb" />
		///     within given number of allowed characters; otherwise false.
		/// </returns>
		internal override bool AppendFriendlyTypeName(Type type, object instance, StringBuilder sb, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			if (type == typeof (SByte))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "sbyte", success, ref maxLength);
			}
			else if (type == typeof (Byte))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "byte", success, ref maxLength);
			}
			else if (type == typeof (Int16))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "short", success, ref maxLength);
			}
			else if (type == typeof (UInt16))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "ushort", success, ref maxLength);
			}
			else if (type == typeof (Int32))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "int", success, ref maxLength);
			}
			else if (type == typeof (UInt32))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "uint", success, ref maxLength);
			}
			else if (type == typeof (Int64))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "long", success, ref maxLength);
			}
			else if (type == typeof (UInt64))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "ulong", success, ref maxLength);
			}
			else if (type == typeof (float))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "float", success, ref maxLength);
			}
			else if (type == typeof (Double))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "double", success, ref maxLength);
			}
			else if (type == typeof (Decimal))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "decimal", success, ref maxLength);
			}
			else if (type == typeof (char))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "char", success, ref maxLength);
			}
			else if (type == typeof (String))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "string", success, ref maxLength);
			}
			else if (type == typeof (Boolean))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "bool", success, ref maxLength);
			}
			else if (type == typeof (DateTime))
			{
				success = FormatInfoUtils.TryAppendString(this, sb, "DateTime", success, ref maxLength);
			}
			else if (type.IsEnum)
			{
				success = FormatInfoUtils.TryAppendString(this, sb, type.Name, success, ref maxLength);
			}
			else
			{
				success = success && base.AppendFriendlyTypeName(type, instance, sb, ref maxLength);
			}

			if (!success)
			{
				sb.Length = originalLength;
			}

			return success;
		}

		/// <summary>
		///     Gets value indicating whether this formatter can format string which represents instance of given data type.
		/// </summary>
		/// <param name="dataType">Data type for which this format provider is tested.</param>
		/// <returns>true if this format provider can format strings that represent instances of given data type; otherwise false.</returns>
		internal override bool IsFormatApplicable(Type dataType)
		{
			return dataType != null && !dataType.IsArray && (dataType.IsEnum || Array.IndexOf(SupportedTypes, dataType) >= 0);
		}

		/// <summary>
		///     Creates deep copy of this instance.
		/// </summary>
		/// <returns>New instance which is identical to this instance.</returns>
		public override object Clone()
		{
			return new ScalarFormatInfo(this);
		}

		/// <summary>
		///     Gets total length required to format specified value.
		/// </summary>
		/// <param name="value">Value which should be presented in the formatted string.</param>
		/// <returns>Number specifying total number of characters required to store the specified value.</returns>
		internal int GetValueLength(object value)
		{
			var sb = new StringBuilder();
			return GetValueLength(value, sb);
		}

		/// <summary>
		///     Gets total length required to format specified value.
		/// </summary>
		/// <param name="value">Value which should be presented in the formatted string.</param>
		/// <param name="sb">
		///     String builder which can be conveniently used to format value.
		///     This function will return the string builder into present state on output.
		/// </param>
		/// <returns>Total number of characters required to store the specified value.</returns>
		internal int GetValueLength(object value, StringBuilder sb)
		{
			var pos = sb.Length;

			var prevFieldLength = _fieldLength;
			_fieldLength = 0; // This prevents Format method from padding the field with white spaces

			Format(sb, null, value, this);

			_fieldLength = prevFieldLength;

			var length = sb.Length - pos;
			sb.Length = pos;

			return length;
		}

		/// <summary>
		///     Gets maximum length required to format any value from the given enumerator.
		/// </summary>
		/// <param name="enumerator">Enumerator which can be used to iterate through values for which maximum length is calculated.</param>
		/// <returns>Largest number of characters required to format any of the values returned by the given enumerator.</returns>
		internal int GetMaxValueLength(IEnumerator enumerator)
		{
			var sb = new StringBuilder();
			var length = GetMaxValueLength(enumerator, sb);
			return length;
		}

		/// <summary>
		///     Gets maximum length required to format any value from the given enumerator.
		/// </summary>
		/// <param name="enumerator">Enumerator which can be used to iterate through values for which maximum length is calculated.</param>
		/// <param name="sb">
		///     String builder which can be conveniently used to format value.
		///     This function will return the string builder into present state on output.
		/// </param>
		/// <returns>Largest number of characters required to format any of the values returned by the given enumerator.</returns>
		internal int GetMaxValueLength(IEnumerator enumerator, StringBuilder sb)
		{
			var length = 0;

			while (enumerator.MoveNext())
			{
				length = Math.Max(length, GetValueLength(enumerator.Current, sb));
			}

			return length;
		}
	}
}
