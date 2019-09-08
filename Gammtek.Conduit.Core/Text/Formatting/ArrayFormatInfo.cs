using System;
using System.Collections.Generic;
using System.Text;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Format provider used to format strings that represent arrays of objects.
	///     VerboseFormatInfo delegates calls to this class when formatting arrays.
	///     This class is internal and cannot be used directly.
	/// </summary>
	internal class ArrayFormatInfo : VerboseFormatInfoBase
	{
		/// <summary>
		///     Default constructor.
		/// </summary>
		public ArrayFormatInfo() {}

		/// <summary>
		///     Copy constructor which copies values common to all verbose format providers.
		/// </summary>
		/// <param name="other">Instance from which common values are copied.</param>
		public ArrayFormatInfo(VerboseFormatInfoBase other)
			: base(other) {}

		/// <summary>
		///     Copy constructor which copies values common to all instances of this class.
		/// </summary>
		/// <param name="other">Instance from which values are copied.</param>
		public ArrayFormatInfo(ArrayFormatInfo other)
			: base(other) {}

		/// <summary>
		///     Gets value indicating whether this format provider is applicable to format strings
		///     that represent instances of given data type.
		///     This format provider is applicable if <paramref name="dataType" /> is array.
		/// </summary>
		/// <param name="dataType">Data type for which this format provider is tested.</param>
		/// <returns>
		///     true if this format provider is capable to format string representing instance
		///     of given data type; false if <paramref name="dataType" /> is null or
		///     this format provider is not applicable to specified type.
		/// </returns>
		internal override bool IsFormatApplicable(Type dataType)
		{
			return (dataType != null && dataType.IsArray);
		}

		/// <summary>
		///     Appends formatted string representing part of the array provided, starting from specific location within the array.
		/// </summary>
		/// <param name="sb">String builder to which formatted string will be appended.</param>
		/// <param name="arg">Array which should be formatted into string which is appended to <paramref name="sb" />.</param>
		/// <param name="indices">
		///     List of index values for dimensions iterated this far. In every call, this method will continue
		///     from location within the array indicated by this parameter.
		/// </param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter.
		///     Single-lined formatting will fail (and return false) if this number of characters is breached.
		///     Multi-lined formatters ignore this parameter. Negative value indicates
		///     that formatter has unlimited space available. On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if representation of <paramref name="arg" /> has been successfully appended to <paramref name="sb" />
		///     within given number of allowed characters; otherwise false.
		/// </returns>
		private bool RecursiveAppendDimension(StringBuilder sb, Array arg, List<int> indices, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			var dimension = arg.GetLength(indices.Count);

			if (dimension > 0)
			{
				for (var i = 0; i < dimension; i++)
				{
					indices.Add(i);

					IncIndentationLevel(i < dimension - 1);

					success = success && FormatLinePrefix(sb, true, i == dimension - 1, false, 0, ref maxLength);
					success = FormatInfoUtils.TryAppendString(this, sb, "Item[", success, ref maxLength);

					var firstIndex = true;
					foreach (var index in indices)
					{
						if (!firstIndex)
						{
							success = FormatInfoUtils.TryAppendString(this, sb, ", ", success, ref maxLength);
						}
						firstIndex = false;
						success = FormatInfoUtils.TryAppendString(this, sb, index.ToString("0"), success, ref maxLength);
					}

					success = FormatInfoUtils.TryAppendString(this, sb, "] ", success, ref maxLength);
					success = FormatInfoUtils.TryAppendString(this, sb, FirstContainedValuePrefix, success, ref maxLength);

					if (indices.Count < arg.Rank)
					{
						success = success && RecursiveAppendDimension(sb, arg, indices, ref maxLength);
					}
					else
					{
						var value = arg.GetValue(indices.ToArray());

						var vfi = new VerboseFormatInfo(this);
						vfi.MaximumFormattedLength =
							FormatInfoUtils.CombineMaxFormattedLengths(
																	   RawMaximumFormattedLength,
																	   FormatInfoUtils.DefaultMaxFormattedLength);

						success = FormatInfoUtils.TryAppendChar(this, sb, ' ', success, ref maxLength);
						success = success && vfi.Format(sb, null, value, vfi, ref maxLength);
					}

					success = FormatInfoUtils.TryAppendString(this, sb, LastContainedValueSuffix, success, ref maxLength);

					DecIndentationLevel();

					indices.RemoveAt(indices.Count - 1);
				}
			}

			if (!success)
			{
				sb.Length = originalLength;
			}

			return success;
		}

		/// <summary>
		///     Converts the value of a specified object to an equivalent string representation using
		///     specified format and culture-specific formatting information.
		/// </summary>
		/// <param name="sb">String builder to which formatted string should be appended.</param>
		/// <param name="format">
		///     A format string containing formatting specifications;
		///     ignored in this instance.
		/// </param>
		/// <param name="arg">An object to format.</param>
		/// <param name="formatProvider">An object that supplies format information about current instance.</param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to the formatter. Single-lined formatting
		///     will fail (and return false) if this number of characters is breached.
		///     Multi-lined formatters ignore this parameter. Negative value indicates that formatter has unlimited
		///     space available. On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if string representation of <paramref name="arg" /> has been successfully
		///     appended to <paramref name="sb" /> within given number of allowed characters; otherwise false.
		/// </returns>
		internal override bool Format(StringBuilder sb, string format, object arg,
			IFormatProvider formatProvider, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			if (PushCurrentObject(arg))
			{
				if (success)
				{
					success = AppendInstanceTypeName(arg, sb, ref maxLength);
				}

				var dimensions = GetDimensions(arg);

				success = FormatInfoUtils.TryAppendSpaceIfNeeded(this, sb, success, ref maxLength);
				success = FormatInfoUtils.TryAppendString(this, sb, FirstContainedValuePrefix, success, ref maxLength);

				if (MaximumDepth > 0)
				{
					MaximumDepth--;

					success = success && RecursiveAppendDimension(sb, (Array) arg, new List<int>(), ref maxLength);

					MaximumDepth++;
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
				sb.Length = originalLength; // Remove any characters appended to string builder in an unsuccessful attempt
			}

			return success;
		}

		/// <summary>
		///     Gets array specifying lengths of all dimensions of instance for which
		///     formatted string is produced using this format provider.
		/// </summary>
		/// <param name="arg">Instance for which dimension lengths are requested.</param>
		/// <returns>
		///     Array containing lengths of all dimensions of <paramref name="arg" />;
		///     empty array if <paramref name="arg" /> is null.
		/// </returns>
		private int[] GetDimensions(object arg)
		{
			int[] dimensions = null;

			if (arg != null)
			{
				var a = (Array) arg;
				dimensions = new int[a.Rank];
				for (var i = 0; i < dimensions.Length; i++)
				{
					dimensions[i] = a.GetLength(i);
				}
			}

			return dimensions;
		}

		/// <summary>
		///     Formats friendly representation of the name of the given type.
		/// </summary>
		/// <param name="type">Type for which friendly representation of the name is required.</param>
		/// <param name="instance">
		///     Instance for which friendly type name is appended.
		///     This argument is used to gather additional information which might not be available
		///     from the type information. This argument may be null if instance is not available.
		/// </param>
		/// <param name="sb">
		///     String builder to which friendly name of <paramref name="type" /> is appended.
		///     If <paramref name="type" /> is null then nothing is appended to this string builder.
		/// </param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to this method
		///     to append to string builder. Negative value indicates unlimited number of characters allowed.
		///     Method fails and returns false if it could not perform the task within given number
		///     of characters. On output contains remaining number of characters allowed.
		/// </param>
		/// <returns>
		///     true if method has successfully appended friendly name of the data type
		///     within given number of characters allowed; otherwise false.
		/// </returns>
		internal override bool AppendFriendlyTypeName(Type type, object instance, StringBuilder sb, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			if (type != null)
			{
				type = type.GetElementType(); // For arrays print element type rather than array type
			}

			if (success)
			{
				var sfi = new ScalarFormatInfo(this);
				success = sfi.AppendFriendlyTypeName(type, null, sb, ref maxLength);
				// Use scalar format info because it can write simple data types in a more compact way
			}

			// Now append dimensions of the array
			// If instance is null then dimensions cannot be determined and braces will remain empty, like int[].
			// Otherwise, if instance is present, dimensions will be fully shown, like int[3, 4, 2] in case of three-dimensional array.

			success = FormatInfoUtils.TryAppendChar(this, sb, '[', success, ref maxLength);

			if (instance != null)
			{
				var dimensions = GetDimensions(instance);
				for (var i = 0; success && i < dimensions.Length; i++)
				{
					if (i > 0)
					{
						success = FormatInfoUtils.TryAppendString(this, sb, ", ", success, ref maxLength);
					}
					success = FormatInfoUtils.TryAppendString(this, sb, dimensions[i].ToString("0"), success, ref maxLength);
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
		///     Performs deep copying of this instance.
		/// </summary>
		/// <returns>New instance which is identical to this instance.</returns>
		public override object Clone()
		{
			return new ArrayFormatInfo(this);
		}
	}
}
