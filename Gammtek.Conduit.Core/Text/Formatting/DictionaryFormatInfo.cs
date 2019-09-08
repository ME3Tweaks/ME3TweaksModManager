using System;
using System.Collections;
using System.Text;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Format provider used to format strings that represent objects that implement System.Collections.IDictionary
	///     or System.Collections.Generic.IDictionary&lt;T&gt; interface.
	///     Formatted string represents keys and values contained in the object.
	///     VerboseFormatInfo delegates calls to this class when formatting dictionary objects.
	///     This class is internal and cannot be used directly.
	/// </summary>
	internal class DictionaryFormatInfo : VerboseFormatInfoBase
	{
		/// <summary>
		///     Default constructor.
		/// </summary>
		public DictionaryFormatInfo() {}

		/// <summary>
		///     Copy constructor which creates new instance identical to other instance of this class.
		/// </summary>
		/// <param name="other">Instance from which values are copied to newly created instance.</param>
		public DictionaryFormatInfo(DictionaryFormatInfo other)
			: base(other) {}

		/// <summary>
		///     Copy constructor which creates new instance having same values of common properties as other instance.
		/// </summary>
		/// <param name="other">Instance from which values are taken for common properties.</param>
		public DictionaryFormatInfo(VerboseFormatInfoBase other)
			: base(other) {}

		/// <summary>
		///     Gets collection of keys of the dictionary object.
		/// </summary>
		/// <param name="arg">Argument which has property named Keys which returns object implementing IEnumerable.</param>
		/// <returns>Collection of keys of the specified dictionary object.</returns>
		private IEnumerable GetKeysCollection(object arg)
		{
			var type = arg.GetType();
			var prop = type.GetProperty("Keys");
			var keys = (IEnumerable) prop.GetValue(arg, null);

			return keys;
		}

		/// <summary>
		///     Gets object stored in the given dictionary under given key.
		/// </summary>
		/// <param name="dict">Dictionary which may either be simple dictionary collection or generic one.</param>
		/// <param name="key">Key associated with the requested object.</param>
		/// <returns>Object stored in the given dictionary under given key.</returns>
		private object GetDictionaryObject(object dict, object key)
		{
			object value = null;
			var dictType = dict.GetType();
			var dictionaryInterface = dictType.GetInterface("System.Collections.IDictionary");

			if (dictionaryInterface == null)
			{
				dictionaryInterface = dictType.GetInterface("System.Collections.Generic.IDictionary");
			}

			if (dictionaryInterface != null)
			{
				var prop = dictionaryInterface.GetProperty("Item");
				value = prop.GetValue(dict, new[] { key });
			}

			return value;
		}

		/// <summary>
		///     Gets declared type of the keys or values used in this dictionary collection.
		/// </summary>
		/// <param name="arg">
		///     Argument passed to this formatter to format appropriate string.
		///     It can be assumed that this value implements System.Collections.IDictionary or
		///     System.Collections.Generic.IDictionary&lt;T&gt; interface, or both. If both interfaces are
		///     implemented, then generic interface is considered first because it contains
		///     generic parameters that define key and value types.
		/// </param>
		/// <param name="getKeyType">
		///     Indicates whether key type is requested (true) or
		///     value type is requested (false).
		/// </param>
		/// <returns>
		///     Type of keys or values stored in the dictionary <paramref name="arg" />;
		///     System.Object if <paramref name="arg" /> is null.
		/// </returns>
		private Type GetDeclaredKeyValueType(object arg, bool getKeyType)
		{
			var type = typeof (object);
			var argType = (arg == null ? null : arg.GetType());

			if (argType != null && argType.IsGenericType)
			{
				// This is generic dictionary
				var genericArguments = argType.GetGenericArguments();
				type = genericArguments[getKeyType ? 0 : 1];
			}

			return type;
		}

		/// <summary>
		///     Gets value from specified dictionary object which is associated with given key value.
		/// </summary>
		/// <param name="dict">Dictionary from which value is requested.</param>
		/// <param name="key">Value of the key associated with requested value.</param>
		/// <returns>Object representing value stored in the given dictionary object.</returns>
		private object GetValue(object dict, object key)
		{
			object value = null;

			var getMethod = dict.GetType().GetMethod("get_Item");

			if (getMethod != null)
			{
				value = getMethod.Invoke(dict, new[] { key });
			}

			return value;
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
				if (arg == null)
				{
					success = FormatInfoUtils.TryAppendString(this, sb, FormatInfoUtils.DefaultNullFormatted, success, ref maxLength);
				}
				else
				{
					var keys = GetKeysCollection(arg);
					var keyType = GetDeclaredKeyValueType(arg, true);
					var valueType = GetDeclaredKeyValueType(arg, false);

					var sfi = new ScalarFormatInfo(this);
					success = success && sfi.AppendInstanceTypeName(arg, sb, ref maxLength);
					// Using scalar format info to append type name ensures that scalar types will be
					// represented with their short forms rather than CTS names which are less readable

					if (!string.IsNullOrEmpty(InstanceName))
					{
						success = FormatInfoUtils.TryAppendSpaceIfNeeded(this, sb, success, ref maxLength);
						success = FormatInfoUtils.TryAppendString(this, sb, InstanceName, success, ref maxLength);
					}

					if (IsMultiLinedFormat)
					{
						success = FormatInfoUtils.TryAppendSpaceIfNeeded(this, sb, success, ref maxLength);
						success = FormatInfoUtils.TryAppendString(this, sb, "= ", success, ref maxLength);
					}
					else
					{
						success = FormatInfoUtils.TryAppendChar(this, sb, '=', success, ref maxLength);
					}

					success = FormatInfoUtils.TryAppendString(this, sb, FirstContainedValuePrefix, success, ref maxLength);

					if (MaximumDepth > 0)
					{
						MaximumDepth--;

						var enumerator = keys.GetEnumerator();

						var isFirstValue = true;

						var key = new object[2];
						var keyFetched = new bool[2];

						keyFetched[0] = enumerator.MoveNext();
						if (keyFetched[0])
						{
							key[0] = enumerator.Current;
							keyFetched[1] = enumerator.MoveNext();
							if (keyFetched[1])
							{
								key[1] = enumerator.Current;
							}
						}

						VerboseFormatInfoBase containedItemsFormat = new VerboseFormatInfo(this);

						var itemPos = 0;

						while (keyFetched[0])
						{
							IncIndentationLevel(keyFetched[1]);
							containedItemsFormat.IncIndentationLevel(keyFetched[1]);

							success = success && FormatLinePrefix(sb, isFirstValue, !keyFetched[1], false, 0, ref maxLength);
							isFirstValue = false;

							success = FormatInfoUtils.TryAppendString(this, sb, "Item[", success, ref maxLength);
							success = FormatInfoUtils.TryAppendString(this, sb, itemPos.ToString("0"), success, ref maxLength);
							itemPos++;
							success = FormatInfoUtils.TryAppendString(this, sb, "] = ", success, ref maxLength);
							success = FormatInfoUtils.TryAppendString(this, sb, FirstContainedValuePrefix, success, ref maxLength);

							containedItemsFormat.IncIndentationLevel(true);
							IncIndentationLevel(true);

							success = success && FormatLinePrefix(sb, true, false, false, 0, ref maxLength);
							containedItemsFormat.InstanceDataType = keyType;
							containedItemsFormat.InstanceName = "Key";
							success = success && containedItemsFormat.Format(sb, null, key[0], containedItemsFormat, ref maxLength);

							DecIndentationLevel();
							containedItemsFormat.DecIndentationLevel();

							containedItemsFormat.IncIndentationLevel(false);
							IncIndentationLevel(false);

							var value = GetValue(arg, key[0]);

							success = success && FormatLinePrefix(sb, false, true, false, 0, ref maxLength);
							containedItemsFormat.InstanceDataType = valueType;
							containedItemsFormat.InstanceName = "Value";
							success = success && containedItemsFormat.Format(sb, null, value, containedItemsFormat, ref maxLength);

							DecIndentationLevel();
							containedItemsFormat.DecIndentationLevel();

							success = FormatInfoUtils.TryAppendString(this, sb, LastContainedValueSuffix, success, ref maxLength);

							key[0] = key[1];
							keyFetched[0] = keyFetched[1];

							if (keyFetched[0])
							{
								keyFetched[1] = enumerator.MoveNext();
								if (keyFetched[1])
								{
									key[1] = enumerator.Current;
								}
							}

							containedItemsFormat.DecIndentationLevel();
							DecIndentationLevel();
						}

						MaximumDepth++;
					} // if (MaximumDepth > 0)

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
		///     Gets value indicating whether this formatter can format string which represents instance of given data type.
		/// </summary>
		/// <param name="dataType">Data type for which this format provider is tested.</param>
		/// <returns>true if this format provider can format strings that represent instances of given data type; otherwise false.</returns>
		internal override bool IsFormatApplicable(Type dataType)
		{
			Type[] interfaces = null;

			if (dataType == null)
			{
				interfaces = new Type[0];
			}
			else
			{
				interfaces = dataType.GetInterfaces();
			}

			var applicable = false;
			for (var i = 0; i < interfaces.Length; i++)
			{
				if (interfaces[i].FullName == "System.Collections.IDictionary" ||
					interfaces[i].FullName.StartsWith("System.Collections.Generic.IDictionary`"))
				{
					applicable = true;
					break;
				}
			}

			return applicable;
		}

		/// <summary>
		///     Creates deep copy of this instance.
		/// </summary>
		/// <returns>New instance which is identical to this instance.</returns>
		public override object Clone()
		{
			return new DictionaryFormatInfo(this);
		}
	}
}
