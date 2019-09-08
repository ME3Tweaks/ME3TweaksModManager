using System;
using System.Text;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Formats strings that represent matrices of scalar types.
	///     Also applicable to jagged arrays of scalar types.
	///     VerboseFormatInfo delegates calls to this class when formatting
	///     two-dimensional arrays or jagged arrays of simple types.
	///     This class is internal and cannot be used directly.
	/// </summary>
	internal class CompactMatrixFormatInfo : VerboseFormatInfoBase
	{
		/// <summary>
		///     Default constructor.
		/// </summary>
		public CompactMatrixFormatInfo() {}

		/// <summary>
		///     Copy constructor which uses other instance to copy values that are common to all verbose format providers.
		/// </summary>
		/// <param name="other">Object from which common values will be copied.</param>
		public CompactMatrixFormatInfo(VerboseFormatInfoBase other)
			: base(other) {}

		/// <summary>
		///     Copy constructor which uses other instance to initialize all contained values.
		/// </summary>
		/// <param name="other">Instance from which internal values are copied.</param>
		public CompactMatrixFormatInfo(CompactMatrixFormatInfo other)
			: base(other) {}

		/// <summary>
		///     Iterates through all values in the matrix or jagged array and finds maximum length required
		///     to show any of the values contained.
		/// </summary>
		/// <param name="array">Matrix or jagged array through which this method will iterate.</param>
		/// <returns>Number of characters sufficient to receive any formatted value contained in <paramref name="array" />.</returns>
		private int GetMaxValueLength(Array array)
		{
			var maxLength = 0;

			if (IsMultiLinedFormat)
			{
				var sfi = new ScalarFormatInfo(this);
				sfi.ShowDataType = false;
				sfi.ShowInstanceName = false;

				if (array.Rank == 2)
				{
					maxLength = sfi.GetMaxValueLength(array.GetEnumerator());
				}
				else
				{
					// In jagged array we have to iterate through rows manually

					var rowsEnumerator = array.GetEnumerator();
					while (rowsEnumerator.MoveNext())
					{
						var row = (Array) rowsEnumerator.Current;
						var curMaxLength = sfi.GetMaxValueLength(row.GetEnumerator());
						maxLength = Math.Max(maxLength, curMaxLength);
					}
				}
			}

			return maxLength;
		}

		/// <summary>
		///     Converts the value of a specified object to an equivalent string representation using
		///     specified format and culture-specific formatting information.
		/// </summary>
		/// <param name="sb">String builder to which formatted string should be appended.</param>
		/// <param name="format">A format string containing formatting specifications.</param>
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
					var instanceType = GetInstanceType(arg);
					success = success && AppendFriendlyTypeName(instanceType, arg, sb, ref maxLength);
				}

				if (sb.Length > originalLength)
				{
					success = FormatInfoUtils.TryAppendChar(this, sb, ' ', success, ref maxLength);
				}
				success = FormatInfoUtils.TryAppendString(this, sb, FirstContainedValuePrefix, success, ref maxLength);

				if (arg != null)
				{
					IncIndentationLevel(true);

					var array = (Array) arg;
					var rows = array.GetLength(0);

					var cafi = new CompactArrayFormatInfo(this);
					cafi.ShowItemsOnly = true;
					cafi.FieldLength = GetMaxValueLength(array);

					var rowNumberFormatProvider = new ScalarFormatInfo(VerboseFormatInfo.SingleLinedFormat);
					rowNumberFormatProvider.ShowDataType = false;
					rowNumberFormatProvider.ShowInstanceName = false;
					if (IsMultiLinedFormat)
					{
						rowNumberFormatProvider.FieldLength = rowNumberFormatProvider.GetValueLength(rows);
					}

					for (var i = 0; i < rows; i++)
					{
						if (i == rows - 1)
						{
							DecIndentationLevel();
							IncIndentationLevel(false); // There are no more rows in the matrix

							cafi.DecIndentationLevel();
							cafi.IncIndentationLevel(false);
						}

						success = success && FormatLinePrefix(sb, true, i == rows - 1, false, 0, ref maxLength);
						success = FormatInfoUtils.TryAppendString(this, sb, "Row ", success, ref maxLength);
						success = success && rowNumberFormatProvider.Format(sb, null, i, rowNumberFormatProvider, ref maxLength);

						success = FormatInfoUtils.TryAppendChar(this, sb, ' ', success, ref maxLength);
						success = FormatInfoUtils.TryAppendString(this, sb, FirstContainedValuePrefix, success, ref maxLength);

						// Now we should append row content, which is obtained differently in case of matrix and in case of jagged array
						if (array.Rank == 1)
						{
							// Array is jagged
							var row = (Array) array.GetValue(i);
							cafi.ShowLastDimension = false;
							success = success && cafi.Format(sb, null, row, cafi, ref maxLength);
						}
						else
						{
							// Array is a matrix
							cafi.HeadingIndices = new[] { i };
							cafi.ShowLastDimension = true;
							success = success && cafi.Format(sb, null, array, cafi, ref maxLength);
							cafi.HeadingIndices = null;
						}

						success = FormatInfoUtils.TryAppendString(this, sb, LastContainedValueSuffix, success, ref maxLength);
					}

					DecIndentationLevel();
				}

				success = FormatInfoUtils.TryAppendString(this, sb, LastContainedValueSuffix, success, ref maxLength);

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
		///     Gets element type of the matrix or jagged array. Returns null if specified data type
		///     is neither matrix nor jagged array.
		/// </summary>
		/// <param name="dataType">Data type for which element type is required.</param>
		/// <returns>
		///     Type of the element of the matrix or jagged array represented by <paramref name="dataType" />;
		///     null if <paramref name="dataType" /> is not matrix or jagged array.
		/// </returns>
		private Type GetElementType(Type dataType)
		{
			Type elementType = null;

			if (dataType != null && dataType.IsArray && dataType.GetArrayRank() == 2)
			{
				// Data type is matrix
				elementType = dataType.GetElementType();
			}
			else if (dataType != null && dataType.IsArray && dataType.GetArrayRank() == 1)
			{
				// This is possibly a jagged array, but that must be checked further
				var partElementType = dataType.GetElementType();
				if (partElementType != null && partElementType.IsArray && partElementType.GetArrayRank() == 1)
				{
					elementType = partElementType.GetElementType(); // This is jagged array for sure
				}
			}

			return elementType;
		}

		/// <summary>
		///     Gets value indicating whether this format provider can be applied to specified data type or not.
		///     This format provider is applicable to matrices (arrays of rank 2) and to jagged arrays, but only
		///     if element type is supported by ScalarFormatInfo format provider.
		/// </summary>
		/// <param name="dataType">Data type agianst which this format provider is tested.</param>
		/// <returns>true if this format provider is applicable; otherwise false.</returns>
		internal override bool IsFormatApplicable(Type dataType)
		{
			var elementType = GetElementType(dataType);

			var applicable = false;

			if (elementType != null)
			{
				// Existence of elementType proves that dataType is matrix or jagged array
				var sfi = new ScalarFormatInfo(this);
				applicable = sfi.IsFormatApplicable(elementType);
			}

			return applicable;
		}

		/// <summary>
		///     Extracts dimension lengths for the given matrix or jagged array instance.
		/// </summary>
		/// <param name="instance">Instance for which dimension lengths are requested.</param>
		/// <param name="rowsCount">On output contains total number of rows in <paramref name="instance" /></param>
		/// <param name="lowColsCount">
		///     On output contains smallest number of elements in any row of the matrix or jagged array.
		///     In case that <paramref name="instance" /> is a proper matrix, this value will indicate number of columns in the matrix.
		/// </param>
		/// <param name="highColsCount">
		///     On output contains largest number of elements in any row of the matrix or jagged array.
		///     In case that <paramref name="instance" /> is a proper matrix, this value will indicate number of columns in the matrix.
		/// </param>
		private void GetDimensions(Array instance, out int rowsCount, out int lowColsCount, out int highColsCount)
		{
			rowsCount = instance.GetLength(0);
			lowColsCount = highColsCount = 0;

			if (instance.Rank == 2)
			{
				lowColsCount = highColsCount = instance.GetLength(1);
			}
			else
			{
				for (var i = 0; i < rowsCount; i++)
				{
					var row = (Array) instance.GetValue(i);
					var cols = row.GetLength(0);

					if (i == 0)
					{
						lowColsCount = highColsCount = cols;
					}
					else
					{
						lowColsCount = Math.Min(lowColsCount, cols);
						highColsCount = Math.Max(highColsCount, cols);
					}
				}
			}
		}

		/// <summary>
		///     Formats user friendly representation of the name of the given matrix or jagged array type.
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

			if (type != null)
			{
				var elementType = GetElementType(type);
				if (elementType != null)
				{
					var sfi = new ScalarFormatInfo();
					success = success && sfi.AppendFriendlyTypeName(elementType, null, sb, ref maxLength);
				}
			}

			var rows = 0;
			var lowCols = 0;
			var highCols = 0;

			if (instance != null)
			{
				GetDimensions((Array) instance, out rows, out lowCols, out highCols);
			}

			success = FormatInfoUtils.TryAppendChar(this, sb, '[', success, ref maxLength);
			if (instance != null)
			{
				success = FormatInfoUtils.TryAppendString(this, sb, rows.ToString("0"), success, ref maxLength);
			}
			success = FormatInfoUtils.TryAppendChar(this, sb, ']', success, ref maxLength);

			success = FormatInfoUtils.TryAppendChar(this, sb, '[', success, ref maxLength);
			if (instance != null)
			{
				success = FormatInfoUtils.TryAppendString(this, sb, lowCols.ToString("0"), success, ref maxLength);
				if (highCols != lowCols)
				{
					success = FormatInfoUtils.TryAppendChar(this, sb, '-', success, ref maxLength);
					success = FormatInfoUtils.TryAppendString(this, sb, highCols.ToString("0"), success, ref maxLength);
				}
			}
			success = FormatInfoUtils.TryAppendChar(this, sb, ']', success, ref maxLength);

			if (!success)
			{
				sb.Length = originalLength;
			}

			return success;
		}

		/// <summary>
		///     Creates deep copy of this instance.
		/// </summary>
		/// <returns>New instance which is identical to this instance.</returns>
		public override object Clone()
		{
			return new CompactMatrixFormatInfo(this);
		}
	}
}
