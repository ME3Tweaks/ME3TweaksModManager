using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Format provider applied to format strings that compactly represent single-dimensional arrays.
	///     Array can be represented compactly if it has one dimension and all elements are of simple type,
	///     i.e. ScalarFormatInfo format provider is applicable to them.
	///     VerboseFormatInfo delegates calls to tihs class when formatting single-dimensional arrays of simple types.
	///     This class is internal and cannot be used directly.
	/// </summary>
	internal class CompactArrayFormatInfo : VerboseFormatInfoBase, IEnumerable<object>
	{
		/// <summary>
		///     Array currently being formatted.
		/// </summary>
		private Array _currentInstance;

		/// <summary>
		///     Length of string available to each of the values formatted.
		///     Used only in multi-lined formats in order to align values.
		/// </summary>
		private int _fieldLength;

		/// <summary>
		///     Values of all indices preceding the last index when this format provider is applied to multi-dimensional array.
		/// </summary>
		private int[] _headingIndices;

		/// <summary>
		///     Maximum length of the line when array is formatted in multi-lined string.
		///     Value zero indicates infinite length.
		/// </summary>
		private int _maxLineLength;

		/// <summary>
		///     Indicates whether this format provider should only show array items (true)
		///     or complete representation of the array, including type (false).
		/// </summary>
		private bool _showItemsOnly;

		/// <summary>
		///     Indicates whether last dimension of the multi-dimensional array should be shown (true)
		///     or array to which this format provider is applied is a single-dimension array (false).
		/// </summary>
		private bool _showLastDimension;

		/// <summary>
		///     Default constructor.
		/// </summary>
		public CompactArrayFormatInfo()
		{
			InitializeDefaultValues();
		}

		/// <summary>
		///     Copy constructor which copies values that are common for all verbose format providers.
		/// </summary>
		/// <param name="other">Instance from which values should be copied.</param>
		public CompactArrayFormatInfo(VerboseFormatInfoBase other)
			: base(other)
		{
			InitializeDefaultValues();
		}

		/// <summary>
		///     Copy constructor which creates identical copy of the given instance.
		/// </summary>
		/// <param name="other">Instance from which values should be copied.</param>
		public CompactArrayFormatInfo(CompactArrayFormatInfo other)
			: base(other)
		{
			InitializeDefaultValues();

			_showItemsOnly = other._showItemsOnly;
			_showLastDimension = other._showLastDimension;
			HeadingIndices = other.HeadingIndices; // This operation copies array; it should not be the simple assignment
			_fieldLength = other._fieldLength;
			_maxLineLength = other._maxLineLength;
		}

		/// <summary>
		///     Gets or sets value indicating whether only items should be shown in the formatted string (true)
		///     or complete information about the array should be shown, including array type declaration (false).
		/// </summary>
		internal bool ShowItemsOnly
		{
			get { return _showItemsOnly; }
			set { _showItemsOnly = value; }
		}

		/// <summary>
		///     Indicates whether this format provider should show only the last dimension of the multi-dimensional array (true)
		///     or it will be applied to single-dimensional array (false). This property must be set to true whenever this formatter
		///     is used to format mutlidimensional arrays.
		/// </summary>
		internal bool ShowLastDimension
		{
			get { return _showLastDimension; }
			set { _showLastDimension = value; }
		}

		/// <summary>
		///     Gets or sets array of values of all indices except the last one, when this format provider is applied
		///     to multi-dimensional array. Ignored when working with single-dimensional arrays.
		/// </summary>
		internal int[] HeadingIndices
		{
			get { return (_headingIndices == null ? new int[0] : _headingIndices); }
			set
			{
				if (value == null)
				{
					_headingIndices = null;
				}
				else
				{
					_headingIndices = new int[value.Length];
					Array.Copy(value, _headingIndices, value.Length);
				}
			}
		}

		/// <summary>
		///     Gets or sets value indicating minimum length used to format each of the contained values.
		///     This value is used in multi-lined formats and it helps align values accross lines.
		///     Ignored in single-lined formats.
		/// </summary>
		internal int FieldLength
		{
			get { return _fieldLength; }
			set { _fieldLength = (value < 0 ? 0 : value); }
		}

		/// <summary>
		///     Gets or sets maximum allowed length of line when formatting array in a multi-lined string.
		///     Ignored in single-lined formats.
		/// </summary>
		internal int MaximumLineLength
		{
			get { return _maxLineLength; }
			set { _maxLineLength = (value < 0 ? 0 : value); }
		}

		/// <summary>
		///     Gets enumerator that can be used to iterate through the array.
		/// </summary>
		/// <returns>Enumerator object that can be used to iterate through array members.</returns>
		public IEnumerator<object> GetEnumerator()
		{
			var enumerator = new CompactArrayFormatEnumerator(_currentInstance, _headingIndices);
			return enumerator;
		}

		/// <summary>
		///     Gets enumerator that can be used to iterate through the array.
		/// </summary>
		/// <returns>Enumerator object that can be used to iterate through array members.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>
		///     Initializes default values in this instance.
		/// </summary>
		private void InitializeDefaultValues()
		{
			_maxLineLength = FormatInfoUtils.DefaultMaxFormattedLength;
		}

		/// <summary>
		///     Creates deep copy of this instance.
		/// </summary>
		/// <returns>New instance of this class which is identical to current instance.</returns>
		public override object Clone()
		{
			return new CompactArrayFormatInfo(this);
		}

		/// <summary>
		///     Gets value indicating whether current format provider is applicable to format strings
		///     that represent instances of given data type. This format provider is applicable if
		///     data type is an array of values to which ScalarFormatInfo format provider is applicable.
		///     This format provider can also be applied to last dimension of a multi-dimensional array
		///     if specified so by the internal properties.
		/// </summary>
		/// <param name="dataType">Data type for which current format provider is tested.</param>
		/// <returns>
		///     true if current format provider is capable to format string representing instance
		///     of given data type; otherwise false.
		/// </returns>
		internal override bool IsFormatApplicable(Type dataType)
		{
			var applicable = false;

			if (dataType != null && dataType.IsArray)
			{
				if (_showLastDimension || dataType.GetArrayRank() == 1)
				{
					var elementType = dataType.GetElementType();
					var sfi = new ScalarFormatInfo(this);

					if (sfi.IsFormatApplicable(elementType))
					{
						applicable = true;
					}
				}
			}

			return applicable;
		}

		/// <summary>
		///     Iterates through values that will be formatted in order to determine whether
		///     field length should be set to some specific value in order to align values better.
		/// </summary>
		/// <param name="sb">
		///     String builder which can be conveniently used to format value.
		///     This function will return the string builder into present state on output.
		/// </param>
		private void UpdateFieldLength(StringBuilder sb)
		{
			if (IsMultiLinedFormat && _fieldLength <= 0 && _maxLineLength > 0)
			{
				// Field length will be updated only in multi-lined formats and only if not already set to positive value from outside.

				var maxLength = 0;
				var totalLength = 0;

				var enumerator = GetEnumerator();
				var sfi = new ScalarFormatInfo(this);
				sfi.FieldLength = 0;
				sfi.ShowDataType = false;
				sfi.ShowInstanceName = false;

				var isFirstField = true;

				while (enumerator.MoveNext())
				{
					var fieldLength = sfi.GetValueLength(enumerator.Current, sb);

					maxLength = Math.Max(maxLength, fieldLength);
					totalLength += (!isFirstField ? 1 : 0) + fieldLength; // Each but the first value is prefixed with a white space of length 1
					isFirstField = false;
				}

				if (totalLength > _maxLineLength)
				{
					_fieldLength = maxLength; // Field length is set only if total contents of the array will breach the maximum allowed line length
				}
				// and consequently values should be aligned
			}
		}

		/// <summary>
		///     Converts the value of a specified array to an equivalent string representation using
		///     specified format and culture-specific formatting information.
		/// </summary>
		/// <param name="sb">String builder to which formatted string should be appended.</param>
		/// <param name="format">A format string containing formatting specifications.</param>
		/// <param name="arg">An object to format.</param>
		/// <param name="formatProvider">An object that supplies format information about the current instance.</param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter. Formatting will fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters will ignore this parameter.
		///     Negative value indicates that formatter has unlimited space available.
		/// </param>
		/// <returns>
		///     true if representation of <paramref name="arg" /> has been successfully appended to <paramref name="sb" />
		///     within given number of allowed characters; otherwise false.
		/// </returns>
		internal override bool Format(StringBuilder sb, string format, object arg, IFormatProvider formatProvider, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			var array = (Array) arg;
			var rank = array.Rank;

			if (rank > 1 || PushCurrentObject(arg))
			{
				_currentInstance = array;

				var prevFieldLength = _fieldLength;
				UpdateFieldLength(sb); // Autonomously updates field length if that will help better format values from the array

				if (!_showItemsOnly && ShowDataType)
				{
					success = success && AppendInstanceTypeName(arg, sb, ref maxLength);
					success = FormatInfoUtils.TryAppendSpaceIfNeeded(this, sb, success, ref maxLength);
					success = FormatInfoUtils.TryAppendString(this, sb, FirstContainedValuePrefix, success, ref maxLength);
				}

				var enumerator = GetEnumerator();

				var sfi = new ScalarFormatInfo(this);
				sfi.ShowDataType = false;
				sfi.ShowInstanceName = false;
				sfi.FieldLength = _fieldLength;

				var linePrefixLength = GetCurrentLineLength(sb);
				var lineStartPos = sb.Length;
				var isFirstValueInLine = true;
				var repeatLastValue = false;

				while (success && (repeatLastValue || enumerator.MoveNext()))
				{
					// At every position it might happen that line should be broken and formatting continued on the following line
					var prevLineLength = sb.Length - lineStartPos;

					string delimiter = null;
					if (isFirstValueInLine)
					{
						delimiter = "";
					}
					else if (IsMultiLinedFormat)
					{
						delimiter = " ";
					}
					else
					{
						delimiter = FieldDelimiter;
					}

					success = FormatInfoUtils.TryAppendString(this, sb, delimiter, success, ref maxLength);
					success = success && sfi.Format(sb, null, enumerator.Current, sfi, ref maxLength);
					isFirstValueInLine = false;
					repeatLastValue = false;

					if (IsMultiLinedFormat && _maxLineLength > 0 && sb.Length - lineStartPos > _maxLineLength && !isFirstValueInLine)
					{
						// Maximum line length has been breached in the multi-lined format
						// As a consequence, last value should be deleted from output, new line should be started and last value formatted again

						sb.Length = prevLineLength + lineStartPos;

						success = success && FormatLinePrefix(sb, true, false, true, linePrefixLength, ref maxLength);

						lineStartPos = sb.Length;
						repeatLastValue = true;
						isFirstValueInLine = true;
					}
				}

				if (!_showItemsOnly)
				{
					success = FormatInfoUtils.TryAppendString(this, sb, LastContainedValueSuffix, success, ref maxLength);
				}

				_fieldLength = prevFieldLength; // Restore field length to value before this method was called

				if (rank == 1)
				{
					PopCurrentObject();
				}
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
		///     Formats user friendly representation of the name of the given type.
		/// </summary>
		/// <param name="type">Type for which user friendly representation of the name is required.</param>
		/// <param name="instance">
		///     Instance for which friendly type name is appended.
		///     This argument is used to gather additional information which might not be available from the type information.
		///     This argument may be null if instance is not available.
		/// </param>
		/// <param name="sb">
		///     String builder to which user friendly representation of the name of <paramref name="type" /> is appended.
		///     If <paramref name="type" /> is null then nothing is appended to this string builder.
		/// </param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to this method to append to string builder. Negative value indicates
		///     unlimited number of characters allowed. Method fails and returns false if it could not perform the task within given number of
		///     characters.
		///     On output contains remaining number of characters allowed.
		/// </param>
		/// <returns>
		///     true if method has successfully appended friendly name of the data type within given number of characters allowed; otherwise
		///     false.
		/// </returns>
		internal override bool AppendFriendlyTypeName(Type type, object instance, StringBuilder sb, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			if (type.IsArray && type.GetArrayRank() == 1)
			{
				// This

				var elementType = type.GetElementType();

				var sfi = new ScalarFormatInfo(this);
				success = success && sfi.AppendFriendlyTypeName(elementType, null, sb, ref maxLength); // This will append compact name for scalar types

				var ar = (Array) instance;
				var dimension = string.Format("[{0}]", ar.GetLength(0));
				success = FormatInfoUtils.TryAppendString(this, sb, dimension, success, ref maxLength);
			}

			if (!success)
			{
				sb.Length = originalLength;
			}

			return success;
		}
	}
}
