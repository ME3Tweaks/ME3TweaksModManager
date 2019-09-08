using System;
using System.Reflection;
using System.Text;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Format provider used to format strings that represent objects which implement
	///     System.Collections.IEnumerable or System.Collections.Generic.IEnumerable&lt;T&gt; interface.
	///     VerboseFormatInfo delegates calls to this class when formatting enumerable types.
	///     This class is internal and cannot be used directly.
	/// </summary>
	internal class EnumerableFormatInfo : VerboseFormatInfoBase
	{
		/// <summary>
		///     Indicates whether only values should be shown in the formatted string (true) or
		///     string should be formatted in usual way, depending on other settings (false).
		/// </summary>
		private bool _showValuesOnly;

		/// <summary>
		///     Default constructor.
		/// </summary>
		public EnumerableFormatInfo() {}

		/// <summary>
		///     Copy constructor which uses other instance to copy internal values which are common to all verbose formatters.
		/// </summary>
		/// <param name="other">Instance from which values are copied that are common to all verbose formatters.</param>
		public EnumerableFormatInfo(VerboseFormatInfoBase other)
			: base(other) {}

		/// <summary>
		///     Copy constructor which copies all internal values from other instance to create identical copy.
		/// </summary>
		/// <param name="other">Instance from which all internal values are copied to new instance.</param>
		public EnumerableFormatInfo(EnumerableFormatInfo other)
			: base(other) {}

		/// <summary>
		///     Gets or sets value indicating whether only values contained in the enumerable object should be shown
		///     in the formatted string (true), or complete formatted string should be produced (false).
		/// </summary>
		public bool ShowValuesOnly
		{
			get { return _showValuesOnly; }
			set { _showValuesOnly = value; }
		}

		/// <summary>
		///     Gets value indicating whether this format provider is applicable to given data type (true) or not (false).
		/// </summary>
		/// <param name="dataType">Type against which this formatter is tested.</param>
		/// <returns>true if this format provider can be applied to given type; otherwise false.</returns>
		internal override bool IsFormatApplicable(Type dataType)
		{
			Type[] interfaces = null;
			if (dataType != null)
			{
				interfaces = dataType.GetInterfaces();
			}
			else
			{
				interfaces = new Type[0];
			}

			var applicable = false;

			for (var i = 0; i < interfaces.Length; i++)
			{
				if (interfaces[i].FullName == "System.Collections.IEnumerable" ||
					interfaces[i].FullName.StartsWith("System.Collections.Generic.IEnumerable`"))
				{
					applicable = true;
					break;
				}
			}

			return applicable;
		}

		/// <summary>
		///     Gets enumerator for a given object which implements System.Collections.IEnumerable or
		///     generic System.Collections.Generic.IEnumerable&lt;T&gt; interface. If both interfaces are
		///     implemented, then generic version is returned by this method.
		/// </summary>
		/// <param name="arg">Instance for which enumerator is requested.</param>
		/// <param name="enumeratedType">
		///     On output contains enumerated type, which is the generic
		///     argument if <paramref name="arg" /> implements generic IEnumerable interface
		///     and object if non-generic IEnumerable is implemented.
		/// </param>
		/// <returns>
		///     Object which is either instance of System.Collections.Generic.IEnumerator&lt;T&gt;
		///     or non-generic System.Collections.IEnumerator, depending on which interface
		///     is implemented by <paramref name="arg" />.
		/// </returns>
		internal object GetEnumerator(object arg, ref Type enumeratedType)
		{
			Type argType = null;
			MethodInfo getEnumeratorMethod = null;
			object enumerator = null;

			if (arg != null)
			{
				argType = arg.GetType();
			}

			if (argType != null)
			{
				var methods = argType.GetMethods();

				for (var i = 0; i < methods.Length; i++)
				{
					if (methods[i].Name == "GetEnumerator" && methods[i].GetParameters().Length == 0)
					{
						getEnumeratorMethod = methods[i];
						if (methods[i].ReturnType != typeof (object))
						{
							break; // This will end further iteration if non-object returning GetEnumerator method was found
						}
					}
				}
			}

			enumeratedType = null;
			if (argType != null && argType.IsGenericType)
			{
				var genericArguments = argType.GetGenericArguments();
				enumeratedType = genericArguments[0];
			}
			else
			{
				enumeratedType = typeof (object);
			}

			if (getEnumeratorMethod != null)
			{
				enumerator = getEnumeratorMethod.Invoke(arg, null);
			}

			return enumerator;
		}

		/// <summary>
		///     Gets next value from enumerator used to iterate through given object.
		/// </summary>
		/// <param name="enumerator">
		///     Enumerator obtained using the <see cref="GetEnumerator" />
		///     method. It may be an instance of generic System.Collections.Generic.IEnumerator&lt;T&gt;
		///     or non-generic System.Collections.IEnumerator, depending on which interface is
		///     implemented by the processed object.
		/// </param>
		/// <param name="value">On output contains next value obtained from the enumerator.</param>
		/// <returns>true if next value was available from the enumerator; otherwise false.</returns>
		internal bool GetNextValue(object enumerator, ref object value)
		{
			var enumType = enumerator.GetType();
			var moveNextMethod = enumType.GetMethod("MoveNext");

			var valueRead = (bool) moveNextMethod.Invoke(enumerator, null);

			if (valueRead)
			{
				var prop = enumType.GetProperty("Current");
				value = prop.GetValue(enumerator, null);
			}
			else
			{
				value = null;
			}

			return valueRead;
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

			if (PushCurrentObject(arg))
			{
				if (ShowDataType && !_showValuesOnly)
				{
					success = success && AppendInstanceTypeName(arg, sb, ref maxLength);
				}

				if (ShowInstanceName && !string.IsNullOrEmpty(InstanceName) && !_showValuesOnly)
				{
					success = FormatInfoUtils.TryAppendSpaceIfNeeded(this, sb, success, ref maxLength);
					success = FormatInfoUtils.TryAppendString(this, sb, InstanceName, success, ref maxLength);
				}

				if (!_showValuesOnly)
				{
					success = FormatInfoUtils.TryAppendSpaceIfNeeded(this, sb, success, ref maxLength);
					success = FormatInfoUtils.TryAppendString(this, sb, FirstContainedValuePrefix, success, ref maxLength);
				}

				Type enumeratedType = null;
				var enumerator = GetEnumerator(arg, ref enumeratedType);

				var itemFormatter = new VerboseFormatInfo(this);

				var value = new object[2];
				var valueFetched = new bool[2];

				valueFetched[0] = GetNextValue(enumerator, ref value[0]);
				if (valueFetched[0])
				{
					valueFetched[1] = GetNextValue(enumerator, ref value[1]);
				}

				var itemPos = 0;

				while (valueFetched[0])
				{
					IncIndentationLevel(valueFetched[1]);
					itemFormatter.IncIndentationLevel(valueFetched[1]);

					var valueName = string.Format("Item[{0}]", itemPos++);

					itemFormatter.InstanceDataType = enumeratedType;
					itemFormatter.InstanceName = valueName;

					success = success && FormatLinePrefix(sb, itemPos == 0, !valueFetched[1], false, 0, ref maxLength);
					success = success && itemFormatter.Format(sb, null, value[0], itemFormatter, ref maxLength);

					valueFetched[0] = valueFetched[1];
					value[0] = value[1];

					valueFetched[1] = GetNextValue(enumerator, ref value[1]);

					itemFormatter.DecIndentationLevel();
					DecIndentationLevel();
				}

				if (!_showValuesOnly)
				{
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
		///     Performs deep copying of this instance.
		/// </summary>
		/// <returns>New instance which is identical to this instance.</returns>
		public override object Clone()
		{
			return new EnumerableFormatInfo(this);
		}
	}
}
