using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Format provider applied to general objects which could not be conveniently formatted
	///     using more specific format providers. VerboseFormatInfo delegates calls to this class
	///     when none other formatter is applicable to given object.
	///     This class is internal and cannot be used directly.
	/// </summary>
	internal class GeneralFormatInfo : VerboseFormatInfoBase
	{
		/// <summary>
		///     Default constructor.
		/// </summary>
		public GeneralFormatInfo() {}

		/// <summary>
		///     Copy constructor which copies values common to all verbose format providers.
		/// </summary>
		/// <param name="other">Instance from which common values are copied.</param>
		public GeneralFormatInfo(VerboseFormatInfoBase other)
			: base(other) {}

		/// <summary>
		///     Copy constructor which copies all values from another instance of this class.
		/// </summary>
		/// <param name="other">Instance from which all contained values are copied to this instance.</param>
		public GeneralFormatInfo(GeneralFormatInfo other)
			: base(other) {}

		/// <summary>
		///     Extracts all members of a given instance that should be formatted to the output string.
		///     All public properties (which are not write-only) and fields are eligible to be included in the output.
		/// </summary>
		/// <param name="arg">Object from which members are extracted.</param>
		/// <returns>
		///     Sorted array containing full descriptions of all public readable properties and public fields
		///     exposed by <paramref name="arg" />. Members are sorted by their names, which means that properties and
		///     fields may be mixed in the output.
		/// </returns>
		private GeneralMemberDescription[] ExtractMembers(object arg)
		{
			var members = new List<GeneralMemberDescription>();

			if (arg != null)
			{
				var type = arg.GetType();
				var fieldFlags = BindingFlags.Public | BindingFlags.Instance;
				var propertyFlags = fieldFlags | BindingFlags.NonPublic;

				var properties = type.GetProperties(propertyFlags);
				var fields = type.GetFields(fieldFlags);

				for (var i = 0; i < properties.Length; i++)
				{
					if (properties[i].CanRead && properties[i].GetIndexParameters().Length == 0)
					{
						var value = properties[i].GetValue(arg, null);
						var gmd = new GeneralMemberDescription(properties[i].PropertyType, properties[i].Name, value);
						members.Add(gmd);
					}
				}

				for (var i = 0; i < fields.Length; i++)
				{
					var value = fields[i].GetValue(arg);
					var gmd = new GeneralMemberDescription(fields[i].FieldType, fields[i].Name, value);
					members.Add(gmd);
				}
			}

			var membersArray = members.ToArray();
			Array.Sort(membersArray);

			return membersArray;
		}

		/// <summary>
		///     Converts the value of a specified object to an equivalent string representation using
		///     specified format and culture-specific formatting information.
		/// </summary>
		/// <param name="sb">String builder to which formatted string will be appended.</param>
		/// <param name="format">Format string containing formatting specifications.</param>
		/// <param name="arg">An object to format.</param>
		/// <param name="formatProvider">An object that supplies format information about the current instance.</param>
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

				if (ShowInstanceName && !string.IsNullOrEmpty(InstanceName))
				{
					success = FormatInfoUtils.TryAppendSpaceIfNeeded(this, sb, success, ref maxLength);
					success = FormatInfoUtils.TryAppendString(this, sb, InstanceName, success, ref maxLength);

					if (IsMultiLinedFormat)
					{
						success = FormatInfoUtils.TryAppendString(this, sb, " = ", success, ref maxLength);
					}
					else
					{
						success = FormatInfoUtils.TryAppendChar(this, sb, '=', success, ref maxLength);
					}
				}
				else
				{
					success = FormatInfoUtils.TryAppendSpaceIfNeeded(this, sb, success, ref maxLength);
				}

				if (arg == null)
				{
					success = FormatInfoUtils.TryAppendString(this, sb, FormatInfoUtils.DefaultNullFormatted, success, ref maxLength);
				}
				else
				{
					success = FormatInfoUtils.TryAppendString(this, sb, FirstContainedValuePrefix, success, ref maxLength);

					if (MaximumDepth > 0)
					{
						MaximumDepth--;

						var members = ExtractMembers(arg);

						var vfi = new VerboseFormatInfo(this);
						vfi.ShowDataType = true;
						vfi.ShowInstanceName = true;

						for (var i = 0; success && i < members.Length; i++)
						{
							IncIndentationLevel(i < members.Length - 1);
							vfi.IncIndentationLevel(i < members.Length - 1);

							success = success && FormatLinePrefix(sb, i == 0, i == members.Length - 1, false, 0, ref maxLength);
							vfi.InstanceDataType = members[i].DataType;
							vfi.InstanceName = members[i].Name;
							success = success && vfi.Format(sb, format, members[i].Value, vfi, ref maxLength);

							vfi.DecIndentationLevel();
							DecIndentationLevel();
						}

						MaximumDepth++;
					}

					success = FormatInfoUtils.TryAppendString(this, sb, LastContainedValueSuffix, success, ref maxLength);
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
		///     Creates identical copy of this instance.
		/// </summary>
		/// <returns>New instance which is identical to this instance.</returns>
		public override object Clone()
		{
			return new GeneralFormatInfo(this);
		}
	}
}
