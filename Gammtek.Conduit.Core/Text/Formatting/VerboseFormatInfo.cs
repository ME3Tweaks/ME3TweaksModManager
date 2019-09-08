using System;
using System.Collections.Generic;
using System.Text;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Provides extended formatting serivces that can be applied to all reference and value types.
	///     This class merely analyzes object that should be formatted and then
	///     delegates formatting to the specific formatter most appropriate for the given type.
	/// </summary>
	public class VerboseFormatInfo : VerboseFormatInfoBase
	{
		/// <summary>
		///     Default constructor.
		/// </summary>
		public VerboseFormatInfo() {}

		/// <summary>
		///     Constructor which allows caller to set name and type of the instance for which
		///     appropriate string is formatted using this object.
		/// </summary>
		/// <param name="instanceType">Type of the instance for which string is being formatted.</param>
		/// <param name="instanceName">Name of the instance for which string is being formatted.</param>
		public VerboseFormatInfo(Type instanceType, string instanceName)
		{
			InstanceDataType = instanceType;
			InstanceName = instanceName;
		}

		/// <summary>
		///     Copy constructor. Used to copy only those property values that are common to all classes derived from VerboseFormatInfoBase.
		/// </summary>
		/// <param name="other">Instance from which contents is copied to new instance of this class.</param>
		public VerboseFormatInfo(VerboseFormatInfoBase other)
			: base(other) {}

		/// <summary>
		///     Copy constructor. Used to copy common property values from another formatter of the same type,
		///     which results in creating output string which is formatted in the same way.
		/// </summary>
		/// <param name="other">Instance from which contents is copied to new instance of this class.</param>
		public VerboseFormatInfo(VerboseFormatInfo other)
			: base(other) {}

		/// <summary>
		///     Gets new instance of this class which can be applied to format single-lined string
		///     which represents given object. This format is applicable to simple objects.
		/// </summary>
		public static VerboseFormatInfoBase SingleLinedFormat
		{
			get { return FormatInfoUtils.CreateSingleLinedFormatter(); }
		}

		/// <summary>
		///     Gets new instance of verbose formatter which can be applied to format multi-lined string without any decorations.
		///     This format is applicable to objets of medium complexity. Indentation is performed using multiple whitespace characters.
		/// </summary>
		public static VerboseFormatInfoBase MultiLinedFormat
		{
			get { return FormatInfoUtils.CreateMultiLinedFormatter(); }
		}

		/// <summary>
		///     Gets new instance of this class which can be applied to format multi-lined strings without any decorations.
		///     This format is applicable to objects of medium complexity. Indentation is performed using horizontal tab characters.
		/// </summary>
		public static VerboseFormatInfoBase TabbedMultiLinedFormat
		{
			get { return FormatInfoUtils.CreateTabbedMultiLinedFormatter(); }
		}

		/// <summary>
		///     Gets new instance of this class which can be applied to format multi-lined string which is decorated
		///     to resemble the look of a tree structure. This format is applicable to complex objects.
		/// </summary>
		public static VerboseFormatInfoBase TreeMultiLinedFormat
		{
			get { return FormatInfoUtils.CreateTreeMultiLinedFromatter(); }
		}

		/// <summary>
		///     Gets new instance of this class which can be applied to format simplest possible strings.
		///     Only the first level of depth is presented in the formatted string and format is single-lined.
		///     This format is applicable to objects of unpredictable complexity, which should be presented by strings of limited length.
		/// </summary>
		public static VerboseFormatInfoBase SimpleFormat
		{
			get { return FormatInfoUtils.CreateSimpleFormatter(); }
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
		///     Maximum number of characters allowed to the formatter. Formatting should fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters should ignore this parameter.
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

			var formatters = new List<VerboseFormatInfoBase>();
			var singleLinedFormat = SingleLinedFormat;

			if (arg != null || InstanceDataType != null)
			{
				var type = GetInstanceType(arg);

				var sfi = new ScalarFormatInfo(this);
				sfi.MaximumFormattedLength = maxLength;

				if (sfi.IsFormatApplicable(type))
				{
					if (IsMultiLinedFormat)
					{
						var singleLinedSfi = new ScalarFormatInfo(this);
						FormatInfoUtils.CopyFormatting(singleLinedFormat, singleLinedSfi);
						singleLinedSfi.CombineMaximumFormattedLength(FormatInfoUtils.DefaultMaxFormattedLength);
						formatters.Add(singleLinedSfi);
					}

					formatters.Add(sfi);
				}

				if (formatters.Count == 0)
				{
					var dfi = new DictionaryFormatInfo(this);
					dfi.MaximumFormattedLength = maxLength;

					if (dfi.IsFormatApplicable(type))
					{
						if (IsMultiLinedFormat)
						{
							var singleLinedDfi = new DictionaryFormatInfo(this);
							FormatInfoUtils.CopyFormatting(singleLinedFormat, singleLinedDfi);
							singleLinedDfi.CombineMaximumFormattedLength(FormatInfoUtils.DefaultMaxFormattedLength);
							formatters.Add(singleLinedDfi);
						}
						formatters.Add(dfi);
					}
				}

				if (formatters.Count == 0)
				{
					var cmfi = new CompactMatrixFormatInfo(this);
					cmfi.MaximumFormattedLength = maxLength;

					if (cmfi.IsFormatApplicable(type))
					{
						//if (IsMultiLinedFormat)   // Uncomment these lines to enable inlining compactly presented matrices; that operation is not suggested
						//{
						//    CompactMatrixFormatInfo singleLinedCmfi = new CompactMatrixFormatInfo(this);
						//    FormatInfoUtils.CopyFormatting(singleLinedFormat, singleLinedCmfi);
						//    singleLinedCmfi.CombineMaximumFormattedLength(FormatInfoUtils.DefaultMaxFormattedLength);
						//    formatters.Add(singleLinedCmfi);
						//}
						formatters.Add(cmfi);
					}
				}

				if (formatters.Count == 0)
				{
					var cafi = new CompactArrayFormatInfo(this);
					cafi.MaximumFormattedLength = maxLength;

					if (cafi.IsFormatApplicable(type))
					{
						if (IsMultiLinedFormat)
						{
							var singleLinedCafi = new CompactArrayFormatInfo(this);
							FormatInfoUtils.CopyFormatting(singleLinedFormat, singleLinedCafi);
							singleLinedCafi.CombineMaximumFormattedLength(FormatInfoUtils.DefaultMaxFormattedLength);
							formatters.Add(singleLinedCafi);
						}
						formatters.Add(cafi);
					}
				}

				if (formatters.Count == 0)
				{
					var afi = new ArrayFormatInfo(this);
					afi.MaximumFormattedLength = maxLength;

					if (afi.IsFormatApplicable(type))
					{
						if (IsMultiLinedFormat)
						{
							var singleLinedAfi = new ArrayFormatInfo(this);
							FormatInfoUtils.CopyFormatting(singleLinedFormat, singleLinedAfi);
							singleLinedAfi.CombineMaximumFormattedLength(FormatInfoUtils.DefaultMaxFormattedLength);
							formatters.Add(singleLinedAfi);
						}
						formatters.Add(afi);
					}
				}

				if (formatters.Count == 0)
				{
					var efi = new EnumerableFormatInfo(this);
					efi.MaximumFormattedLength = maxLength;

					if (efi.IsFormatApplicable(type))
					{
						if (IsMultiLinedFormat)
						{
							var singleLinedEfi = new EnumerableFormatInfo(this);
							FormatInfoUtils.CopyFormatting(singleLinedFormat, singleLinedEfi);
							singleLinedEfi.CombineMaximumFormattedLength(FormatInfoUtils.DefaultMaxFormattedLength);
							formatters.Add(singleLinedEfi);
						}
						formatters.Add(efi);
					}
				}

				if (formatters.Count == 0)
				{
					if (IsMultiLinedFormat)
					{
						var singleLinedGfi = new GeneralFormatInfo(this);
						FormatInfoUtils.CopyFormatting(singleLinedFormat, singleLinedGfi);
						singleLinedGfi.CombineMaximumFormattedLength(FormatInfoUtils.DefaultMaxFormattedLength);
						formatters.Add(singleLinedGfi);
					}

					var gfi = new GeneralFormatInfo(this);
					gfi.MaximumFormattedLength = maxLength;

					formatters.Add(gfi);
				}
			}

			if (formatters.Count > 0)
			{
				success = success && FormatInfoUtils.BestTryFormat(sb, format, arg, formatters, ref maxLength);
			}

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
			return new VerboseFormatInfo(this);
		}
	}
}
