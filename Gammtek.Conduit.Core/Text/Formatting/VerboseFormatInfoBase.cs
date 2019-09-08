using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Base class for all verbose format providers. Implements basic functionality and defines mandatory members
	///     that would be overridden or implemented by derived classes in order to provide
	///     specific functionality applicable to format strings representing instances of particular data types.
	/// </summary>
	public abstract class VerboseFormatInfoBase : IFormatProvider, ICustomFormatter, ICloneable
	{
		/// <summary>
		///     String used to delimit successive fields;
		///     applied before any line prefixes and indentation strings.
		/// </summary>
		private string _fieldDelimiter;

		/// <summary>
		///     String printed before the first contained value.
		/// </summary>
		private string _firstContainedValuePrefix;

		/// <summary>
		///     Indentation level applied to next value printed.
		/// </summary>
		private int _indentationLevel;

		/// <summary>
		///     String applied at every indentation level.
		///     Ignored in single-lined formats.
		/// </summary>
		private string _indentationString;

		/// <summary>
		///     Data type of the instance for which string should be formatted. This field is used only if
		///     given instance is null and its type cannot be determined.
		/// </summary>
		private Type _instanceDataType;

		/// <summary>
		///     Name of the instance for which string is formatted.
		/// </summary>
		private string _instanceName;

		/// <summary>
		///     List containing flags, each showing whether there are more items to follow
		///     at specified indentation level (true) or previous item at that
		///     indentation level was the last one (false). This field may be null
		///     if no indentation levels other than 0 have been used.
		/// </summary>
		private List<bool> _isIndentationLevelOccupied;

		/// <summary>
		///     Value indicating whether this format is multi-lined (true) or single-lined (false).
		/// </summary>
		private bool _isMultiLinedFormat;

		/// <summary>
		///     String printed after the last contained value.
		/// </summary>
		private string _lastContainedValueSuffix;

		/// <summary>
		///     String applied at every indentation level before the last value is output.
		///     Used only in multi-lined formats.
		/// </summary>
		private string _lastIndentationString;

		/// <summary>
		///     String applied at last indentation level before the last value is printed.
		///     Used only in multi-lined formats.
		/// </summary>
		private string _lastRightMostIndentationString;

		/// <summary>
		///     String prepended to every newly started line.
		///     Ignored in single-lined formats.
		/// </summary>
		private string _linePrefix;

		/// <summary>
		///     Maximum depth of any contained object which is written to the output
		///     while formatting the string.
		/// </summary>
		private int _maximumDepth;

		/// <summary>
		///     Maximum length of the formatted string when this formatter is applied.
		///     Ignored in multi-lined formats.
		/// </summary>
		private int _maximumFormattedLength;

		/// <summary>
		///     String applied at last indentation level before value is printed.
		///     Used only in multi-lined formats.
		/// </summary>
		private string _rightMostIndentationString;

		/// <summary>
		///     Indicates whether data type of the currently formatted instance
		///     should be shown (true) or not (false) when formatting the string.
		/// </summary>
		private bool _showDataType;

		/// <summary>
		///     Indicates whether name of the currently formatted instance
		///     should be shown (true) or not (false) when formatting the string.
		/// </summary>
		private bool _showInstanceName;

		/// <summary>
		///     Stack containing objects which have been visited in current line of references.
		///     This structure is used to detect loops which would end up in producing infinite
		///     output if such structure is attempted to be converted to formatted string.
		/// </summary>
		private Stack<object> _visitedInstances;

		/// <summary>
		///     Default constructor.
		/// </summary>
		public VerboseFormatInfoBase()
		{
			_firstContainedValuePrefix = "{";
			_lastContainedValueSuffix = "}";

			_fieldDelimiter = CultureInfo.CurrentCulture.TextInfo.ListSeparator + " ";
			_isMultiLinedFormat = false;

			_linePrefix = string.Empty;

			_indentationString = _rightMostIndentationString = _lastIndentationString = _lastRightMostIndentationString = "    ";

			_maximumDepth = FormatInfoUtils.DefaultMaximumDepth;
			_maximumFormattedLength = -1; // By default, none of the formatters is limited in length of the formatted string
			// If there is any reason to limit length of the formatted string then it must be stated
			// explicitly after constructor is executed by setting the MaximumFormattedLength property value

			_showDataType = true;
			_showInstanceName = true;
		}

		/// <summary>
		///     Copy constructor. Used to build format providers that share common property values with some existing instance.
		/// </summary>
		/// <param name="other">Instance from which common property values will be copied.</param>
		public VerboseFormatInfoBase(VerboseFormatInfoBase other)
			: this()
		{
			_instanceDataType = other._instanceDataType;
			_instanceName = other._instanceName;
			_fieldDelimiter = other._fieldDelimiter;

			_firstContainedValuePrefix = other._firstContainedValuePrefix;
			_lastContainedValueSuffix = other._lastContainedValueSuffix;

			_isMultiLinedFormat = other._isMultiLinedFormat;
			_linePrefix = other._linePrefix;

			_indentationString = other._indentationString;
			_rightMostIndentationString = other._rightMostIndentationString;
			_lastIndentationString = other._lastIndentationString;
			_lastRightMostIndentationString = other._lastRightMostIndentationString;

			_maximumDepth = other._maximumDepth;
			_maximumFormattedLength = other._maximumFormattedLength;

			_showDataType = other._showDataType;
			_showInstanceName = other._showInstanceName;

			while (_indentationLevel < other.IndentationLevel)
			{
				IncIndentationLevel(other.IsIndentationLevelOccupied(_indentationLevel + 1));
			}

			if (other._visitedInstances != null && other._visitedInstances.Count > 0)
			{
				var tempStack = new Stack<object>();
				foreach (var obj in other._visitedInstances)
				{
					tempStack.Push(obj);
				}

				_visitedInstances = new Stack<object>();
				while (tempStack.Count > 0)
				{
					_visitedInstances.Push(tempStack.Pop());
				}
			}
		}

		/// <summary>
		///     Gets or sets value indicating type of the instance for which string should be formatted.
		///     This property is used when null reference is passed to the formatter, making it unable
		///     to determine type of the instance. In such cases caller might set this property to appropriate
		///     value, if it knows (e.g. through reflection) actual type of the instance.
		/// </summary>
		public Type InstanceDataType
		{
			get { return _instanceDataType; }
			set { _instanceDataType = value; }
		}

		/// <summary>
		///     Gets or sets name of the instance for which string is being formatted.
		/// </summary>
		public string InstanceName
		{
			get { return _instanceName; }
			set { _instanceName = value; }
		}

		/// <summary>
		///     Gets or sets string printed before the first contained value is output.
		/// </summary>
		public string FirstContainedValuePrefix
		{
			get { return _firstContainedValuePrefix; }
			set { _firstContainedValuePrefix = value; }
		}

		/// <summary>
		///     Gets or sets string printed after the last contained value is output.
		/// </summary>
		public string LastContainedValueSuffix
		{
			get { return _lastContainedValueSuffix; }
			set { _lastContainedValueSuffix = value; }
		}

		/// <summary>
		///     Gets indentation level applied to next value output.
		/// </summary>
		internal int IndentationLevel
		{
			get { return _indentationLevel; }
		}

		/// <summary>
		///     Gets or sets string used to delimit successive fields.
		///     This string is appended after every field except last one, regardless of
		///     indentation and line prefixes.
		/// </summary>
		public string FieldDelimiter
		{
			get { return _fieldDelimiter; }
			set
			{
				_fieldDelimiter = value;
				_isMultiLinedFormat = _fieldDelimiter != null && (_fieldDelimiter.Contains("\n") || _fieldDelimiter.Contains("\r"));
			}
		}

		/// <summary>
		///     Gets or sets value indicating whether this format is multi-lined (true) or single-lined (false).
		///     Multi-lined formats contain new line characters as part of their field delimiter.
		/// </summary>
		internal bool IsMultiLinedFormat
		{
			get { return _isMultiLinedFormat; }
			set { _isMultiLinedFormat = value; }
		}

		/// <summary>
		///     Gets or sets string prepended to every new line. Ignored in single-lined formats.
		/// </summary>
		public string LinePrefix
		{
			get { return _linePrefix; }
			set { _linePrefix = value; }
		}

		/// <summary>
		///     Gets or sets string applied at every indentation step. Ignored in single-lined formats.
		/// </summary>
		public string IndentationString
		{
			get { return _indentationString; }
			set { _indentationString = value; }
		}

		/// <summary>
		///     Gets or sets string applied at last indentation step before value is output.
		///     If null, then IndentationString is used.
		///     Used only in multi-lined formats.
		/// </summary>
		public string RightMostIndentationString
		{
			get { return _rightMostIndentationString; }
			set { _rightMostIndentationString = value; }
		}

		/// <summary>
		///     Gets or sets string applied at every indentation step of the last value in a collection.
		///     If null, then IndentationString is used instead.
		///     Used only in multi-lined formats.
		/// </summary>
		public string LastIndentationString
		{
			get { return _lastIndentationString; }
			set { _lastIndentationString = value; }
		}

		/// <summary>
		///     Gets or sets string applied at the last indentation step of the last value in a collection.
		///     If null and LastIndentationString is non-null, then LastIndentationString is used.
		///     If LastIndentationString is also null, then RightMostIndentationString is used.
		///     If that property is also null, then IndentationString is used.
		///     Used only in multi-lined formats.
		/// </summary>
		public string LastRightMostIndentationString
		{
			get { return _lastRightMostIndentationString; }
			set { _lastRightMostIndentationString = value; }
		}

		/// <summary>
		///     Gets or sets one-based value indicating maximum allowed depth to which
		///     contained objects are traversed when formatting the string.
		/// </summary>
		public int MaximumDepth
		{
			get { return _maximumDepth; }
			set { _maximumDepth = value; }
		}

		/// <summary>
		///     Gets or sets value indicating whether data type of the currently formatted instance should
		///     be shown (true) or not (false) when formatting the string. Default is true.
		/// </summary>
		internal bool ShowDataType
		{
			get { return _showDataType; }
			set { _showDataType = value; }
		}

		/// <summary>
		///     Gets or sets value indicating whether name of the currently formatted instance should
		///     be shown (true) or not (false) when formatting the string. Default is true.
		/// </summary>
		internal bool ShowInstanceName
		{
			get { return _showInstanceName; }
			set { _showInstanceName = value; }
		}

		/// <summary>
		///     Gets or sets value indicating maximum allowed length of the formatted string when
		///     formatting is done in single line. Ignored in multi-lined formats.
		///     Negative value indicates infinite allowed length. Returns -1 if this formatter is multi-lined.
		/// </summary>
		public int MaximumFormattedLength
		{
			get { return IsMultiLinedFormat ? -1 : _maximumFormattedLength; }
			set { _maximumFormattedLength = (value < 0 ? -1 : value); }
		}

		/// <summary>
		///     Gets maximum allowed length of the formatted string not affected by formatting options.
		/// </summary>
		protected int RawMaximumFormattedLength
		{
			get { return _maximumFormattedLength; }
		}

		/// <summary>
		///     Creates new instance which is identical to this one but has ShowDataType and ShowInstanceName
		///     properties set to false. Use this property to instantiate format providers that can show
		///     only value of the object, ignoring its type and name, using existing format.
		/// </summary>
		public VerboseFormatInfoBase ValueOnly
		{
			get
			{
				var format = (VerboseFormatInfoBase) Clone();
				format.ShowDataType = false;
				format.ShowInstanceName = false;
				return format;
			}
		}

		/// <summary>
		///     Implement in derived classes to perform deep copying of the current object.
		/// </summary>
		/// <returns>New instance which is identical to current instance.</returns>
		public abstract object Clone();

		/// <summary>
		///     Converts the value of a specified object to an equivalent string representation using
		///     specified format and culture-specific formatting information.
		/// </summary>
		/// <param name="format">A format string containing formatting specifications.</param>
		/// <param name="arg">An object to format.</param>
		/// <param name="formatProvider">An object that supplies format information about the current instance.</param>
		/// <returns>
		///     The string representation of the value of <paramref name="arg" />,
		///     formatted as specified by <paramref name="format" /> and <paramref name="formatProvider" />.
		/// </returns>
		public virtual string Format(string format, object arg, IFormatProvider formatProvider)
		{
			var sb = new StringBuilder();
			var maxLength = MaximumFormattedLength;
			Format(sb, format, arg, formatProvider, ref maxLength);
			return sb.ToString();
		}

		/// <summary>
		///     Returns an object that provides formatting services for the specified type.
		/// </summary>
		/// <param name="formatType">An object that specifies the type of format object to return.</param>
		/// <returns>
		///     An instance of the object specified by formatType, if the IFormatProvider implementation can
		///     supply that type of object; otherwise, null.
		/// </returns>
		public virtual object GetFormat(Type formatType)
		{
			object format = null;

			if (formatType == typeof (ICustomFormatter))
			{
				format = this;
			}

			return format;
		}

		/// <summary>
		///     Converts the value of a specified object to an equivalent string representation
		///     using default format and culture-specific formatting information.
		/// </summary>
		/// <param name="arg">An object to format.</param>
		/// <returns>
		///     The string representation of the value of <paramref name="arg" />, formatted using default format
		///     and this object as format provider.
		/// </returns>
		public virtual string Format(object arg)
		{
			var sb = new StringBuilder();
			var maxLength = MaximumFormattedLength;
			Format(sb, null, arg, this, ref maxLength);
			return sb.ToString();
		}

		/// <summary>
		///     Gets value indicating whether current format provider is applicable to format strings
		///     that represent instances of given data type. Override in derived classes to return appropriate value.
		/// </summary>
		/// <param name="dataType">Data type for which current format provider is tested.</param>
		/// <returns>
		///     true if current format provider is capable to format string representing instance
		///     of given data type; otherwise false.
		/// </returns>
		internal virtual bool IsFormatApplicable(Type dataType)
		{
			return true;
		}

		/// <summary>
		///     Converts the value of a specified object to an equivalent string representation using
		///     specified format and culture-specific formatting information.
		/// </summary>
		/// <param name="sb">String builder to which formatted string should be appended.</param>
		/// <param name="format">Format string containing formatting specifications.</param>
		/// <param name="arg">An object to format.</param>
		/// <param name="formatProvider">An object that supplies format information about the current instance.</param>
		internal void Format(StringBuilder sb, string format, object arg, IFormatProvider formatProvider)
		{
			var infiniteMaxLength = -1;
			Format(sb, format, arg, formatProvider, ref infiniteMaxLength);
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
		///     Maximum number of characters allowed to the formatter. Formatting should fail (and return false)
		///     if this number of characters is breached. Multi-lined formatters should ignore this parameter.
		///     Negative value indicates that formatter has unlimited space available.
		///     On output should contain remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if representation of <paramref name="arg" /> has been successfully appended to <paramref name="sb" />
		///     within given number of allowed characters; otherwise false.
		/// </returns>
		internal abstract bool Format(StringBuilder sb, string format, object arg, IFormatProvider formatProvider, ref int maxLength);

		/// <summary>
		///     Gets type of the given instance if available either from the instance itself or from this format provider.
		/// </summary>
		/// <param name="instance">Instance for which data type is determined.</param>
		/// <returns>Type of the instance if non-null; otherwise value of the InstanceDataType property.</returns>
		protected virtual Type GetInstanceType(object instance)
		{
			Type t = null;

			if (instance != null)
			{
				t = instance.GetType();
			}
			else
			{
				t = _instanceDataType;
			}

			return t;
		}

		/// <summary>
		///     Gets user friendly name of the type of the instance if available either from the instance itself or from this format provider.
		/// </summary>
		/// <param name="instance">Instance for which data type name is required.</param>
		/// <returns>String representing user friendly name of the data type of the given instance.</returns>
		internal virtual string GetInstanceTypeName(object instance)
		{
			var sb = new StringBuilder();
			var infiniteMaxLength = -1;
			AppendInstanceTypeName(instance, sb, ref infiniteMaxLength);
			return sb.ToString();
		}

		/// <summary>
		///     Appends user friendly name of the type of the given instance if available either from the instance itself or from this format provider.
		/// </summary>
		/// <param name="instance">Instance for which data type name is required.</param>
		/// <param name="sb">
		///     String builder to which name of the type will be appended.
		///     If name cannot be determined (i.e. <paramref name="instance" /> is null and this format provider
		///     does not contain type of the instance) then nothing will be appended to this string builder.
		/// </param>
		/// <param name="maxLength">
		///     Indicates maximum number of characters allowed to this method to append to the string builder.
		///     Negative value indicates unlimited amount of space. Method fails and returns false if it would
		///     require more space to perform its task. On output contains remaining number of characters allowed.
		/// </param>
		/// <returns>
		///     true if this method has successfully formatted text at the end of the string builder within given amount of space; otherwise
		///     false.
		/// </returns>
		internal bool AppendInstanceTypeName(object instance, StringBuilder sb, ref int maxLength)
		{
			var type = GetInstanceType(instance);
			return AppendFriendlyTypeName(type, instance, sb, ref maxLength);
		}

		/// <summary>
		///     Formats user friendly representation of the name of the given type. Override in derived classes to implement specific
		///     friendly names for known types handled by derived classes. Make sure that overridden implementations call base class
		///     implementation in case that they cannot resolve type name.
		/// </summary>
		/// <param name="type">Type for which user friendly representation of the name is required.</param>
		/// <param name="instance">
		///     Instance for which friendly type name is appended.
		///     Use this argument to gather additional information which might not be available from the type information.
		///     This argument may be null if instance is not available.
		/// </param>
		/// <param name="sb">
		///     String builder to which user friendly representation of the name of <paramref name="type" /> is appended.
		///     If <paramref name="type" /> is null then nothing is appended to this string builder.
		/// </param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to this method to append to string builder.
		///     Negative value indicates unlimited number of characters allowed.
		///     Method fails and returns false if it could not perform the task within given number of characters.
		///     On output contains remaining number of characters allowed.
		/// </param>
		/// <returns>
		///     true if method has successfully appended friendly name of the data type within given number of characters allowed; otherwise
		///     false.
		/// </returns>
		internal virtual bool AppendFriendlyTypeName(Type type, object instance, StringBuilder sb, ref int maxLength)
		{
			var success = true;
			var originalLength = sb.Length;

			if (type != null)
			{
				var typeName = type.Name;
				var backQuotePos = typeName.IndexOf('`');
				if (backQuotePos >= 0)
				{
					typeName = typeName.Substring(0, backQuotePos);
				}

				success = FormatInfoUtils.TryAppendString(this, sb, typeName, success, ref maxLength);

				if (success && type.IsGenericType)
				{
					var genericArguments = type.GetGenericArguments();

					success = FormatInfoUtils.TryAppendString(this, sb, "<", success, ref maxLength);

					for (var i = 0; success && i < genericArguments.Length; i++)
					{
						if (i > 0)
						{
							success = FormatInfoUtils.TryAppendString(this, sb, ", ", success, ref maxLength);
						}
						success = success && AppendFriendlyTypeName(genericArguments[i], null, sb, ref maxLength);
					}

					success = FormatInfoUtils.TryAppendString(this, sb, ">", success, ref maxLength);
				}
			}

			if (!success)
			{
				sb.Length = originalLength;
			}

			return success;
		}

		/// <summary>
		///     Increases indentation level by one.
		/// </summary>
		/// <param name="moreItemsToFollow">
		///     Indicates whether there are more items to follow at the current level (true)
		///     or previously printed item on this level was the last item on current indentation level (false).
		/// </param>
		internal void IncIndentationLevel(bool moreItemsToFollow)
		{
			if (_isIndentationLevelOccupied == null)
			{
				_isIndentationLevelOccupied = new List<bool>();
			}

			_indentationLevel++;

			while (_isIndentationLevelOccupied.Count <= _indentationLevel)
			{
				_isIndentationLevelOccupied.Add(false);
			}

			_isIndentationLevelOccupied[_indentationLevel] = moreItemsToFollow;
		}

		/// <summary>
		///     Gets value indicating whether at specified indentation level there are more items to follow
		///     or last printed item on that indentation level is overall last item on that indentation level.
		/// </summary>
		/// <param name="level">Indentation level at which status is requested.</param>
		/// <returns>true if there are more items to follow at specified indentation level; otherwise false.</returns>
		internal bool IsIndentationLevelOccupied(int level)
		{
			var occupied = false;

			if (_isIndentationLevelOccupied != null && level >= 0 && level < _isIndentationLevelOccupied.Count)
			{
				occupied = _isIndentationLevelOccupied[level];
			}
			else if (level == _indentationLevel)
			{
				occupied = true; // Current indentation level is occupied by definition
			}

			return occupied;
		}

		/// <summary>
		///     Reduces indentation level by one.
		/// </summary>
		internal void DecIndentationLevel()
		{
			if (_indentationLevel > 0)
			{
				_indentationLevel--;
			}
		}

		/// <summary>
		///     Appends all fields that should be placed before next contained value is appended to the output.
		///     In single-lined formats that will only be the field delimiter.
		///     In multi-lined formats that may include indentation strings.
		/// </summary>
		/// <param name="sb">
		///     String builder which ends with a field after which strings that are used to
		///     delimit successive fields should be appended.
		/// </param>
		/// <param name="firstValueFollows">
		///     Indicates whether next value to be formatted is the first contained value
		///     under the current instance (true) or other fields at the same level have already been appended to the output (false).
		/// </param>
		/// <param name="lastValueFollows">
		///     Indicates whether next value is the last one (true) or there are more values to follow (false).
		///     Ignored if this format provider is single-lined.
		/// </param>
		/// <param name="skipRightMostIndentationString">
		///     Indicates whether right-most indentation string should be replaced with normal indentation string
		///     (true) or not (false). This stands for last right most indentation string as well.
		/// </param>
		/// <param name="padLineToLength">
		///     Indicates required length of the line in multi-lined formats. If string created by applying
		///     all prefixes and delimiters is shorter than this value then it is right-padded with blank spaces until this length is reached.
		/// </param>
		/// <param name="maxLength">
		///     Maximum number of characters allowed to be appended to string builder by this function.
		///     Negative value indicates unlimited number. If this function cannot fit the content into this number of characters
		///     then it fails and returns false. On output contains remaining number of characters available.
		/// </param>
		/// <returns>
		///     true if function has successfully appended specified content to the string builder without breaching the allowed
		///     number of characters specified by parameter <paramref name="maxLength" />; otherwise false.
		/// </returns>
		internal bool FormatLinePrefix(StringBuilder sb, bool firstValueFollows, bool lastValueFollows, bool skipRightMostIndentationString,
			int padLineToLength, ref int maxLength)
		{
			var originalLength = sb.Length;
			var success = true;

			if (firstValueFollows && _isMultiLinedFormat)
			{
				success = FormatInfoUtils.TryAppendString(this, sb, Environment.NewLine, success, ref maxLength);
			}
			else if (!firstValueFollows)
			{
				success = FormatInfoUtils.TryAppendString(this, sb, _fieldDelimiter, success, ref maxLength);
			}

			if (_isMultiLinedFormat)
			{
				success = FormatInfoUtils.TryAppendString(this, sb, _linePrefix, success, ref maxLength);

				for (var i = 0; success && i < IndentationLevel; i++)
				{
					string indentString = null;
					string lastIndentString = null;

					if (IsIndentationLevelOccupied(i + 1))
					{
						indentString = IndentationString;
						lastIndentString = LastIndentationString;
					}
					else
					{
						indentString = string.Empty;
						for (var j = 0; j < IndentationString.Length; j++)
						{
							if (IndentationString[j] == '\t')
							{
								indentString += '\t';
							}
							else
							{
								indentString += ' ';
							}
						}

						lastIndentString = string.Empty;
						for (var j = 0; j < LastIndentationString.Length; j++)
						{
							if (LastIndentationString[j] == '\t')
							{
								lastIndentString += '\t';
							}
							else
							{
								lastIndentString += ' ';
							}
						}
					}

					var rightMostIndentString = (skipRightMostIndentationString ? indentString : RightMostIndentationString);
					var lastRightMostIndentString = (skipRightMostIndentationString ? lastIndentString : LastRightMostIndentationString);

					if (lastValueFollows && i == IndentationLevel - 1)
					{
						success = FormatInfoUtils.TryAppendString(this, sb,
																  lastRightMostIndentString ?? lastIndentString ?? rightMostIndentString ?? indentString, success, ref maxLength);
					}
					else if (lastValueFollows)
					{
						success = FormatInfoUtils.TryAppendString(this, sb, lastIndentString ?? indentString, success, ref maxLength);
					}
					else if (i == IndentationLevel - 1)
					{
						success = FormatInfoUtils.TryAppendString(this, sb, rightMostIndentString ?? indentString, success, ref maxLength);
					}
					else
					{
						success = FormatInfoUtils.TryAppendString(this, sb, indentString, success, ref maxLength);
					}
				}

				if (padLineToLength > 0)
				{
					var lineLength = GetCurrentLineLength(sb);
					while (success && lineLength++ < padLineToLength)
					{
						success = FormatInfoUtils.TryAppendChar(this, sb, ' ', success, ref maxLength);
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
		///     Gets length of the last line in the given string builder. Use this method to pad lines depending on required length.
		/// </summary>
		/// <param name="sb">String builder for which length of the last line is requested.</param>
		/// <returns>
		///     Distance of the end of the string builder's contents from the last carriage return or
		///     line feed characters contained in it. Total length of the string builder's contents
		///     if no line ending characters are present.
		/// </returns>
		internal int GetCurrentLineLength(StringBuilder sb)
		{
			var lineStart = sb.Length;
			while (lineStart > 0 && sb[lineStart - 1] != '\r' && sb[lineStart - 1] != '\n')
			{
				lineStart--;
			}

			return sb.Length - lineStart;
		}

		/// <summary>
		///     Combines maximum formatted string length from this instance with given length and sets new maximum length for this instance.
		/// </summary>
		/// <param name="length">
		///     Length with which maximum allowed length of the string created by this formatter is combined;
		///     negative value indicates infinite allowed length.
		/// </param>
		/// <returns>
		///     New value of the maximum allowed string length for this formatter, which equals value that will be returned by
		///     MaximumFormattedLength property after this method returns. Note that return value is affected by IsMultiLinedFormat property
		///     value in sense that whenever this formatter is multi-lined, this method will return value -1 regardless of actual
		///     internal setting of maximum string length.
		/// </returns>
		public int CombineMaximumFormattedLength(int length)
		{
			_maximumFormattedLength = FormatInfoUtils.CombineMaxFormattedLengths(_maximumFormattedLength, length);
			return MaximumFormattedLength;
		}

		/// <summary>
		///     Converts current instance to user friendly string using simple formatter to format the string.
		/// </summary>
		/// <returns>String which represents contents of this instance.</returns>
		public override string ToString()
		{
			return FormatInfoUtils.CreateSimpleFormatter().Format(this);
		}

		/// <summary>
		///     Checks whether specified object is already present in the stack containing visited
		///     instances and, if it is not, pushes the object to the stack. Otherwise method fails
		///     and signals the caller that specified object has already been visited in current
		///     line of references.
		/// </summary>
		/// <param name="obj">Object which is currently being processed.</param>
		/// <returns>
		///     true if <paramref name="obj" /> was not present in the visited instances stack;
		///     otherwise false.
		/// </returns>
		protected bool PushCurrentObject(object obj)
		{
			var success = true;

			if (_visitedInstances == null)
			{
				_visitedInstances = new Stack<object>();
			}

			if (obj != null)
			{
				foreach (var inst in _visitedInstances)
				{
					if (ReferenceEquals(inst, obj))
					{
						success = false;
						break;
					}
				}
			}

			if (success)
			{
				_visitedInstances.Push(obj);
			}

			return success;
		}

		/// <summary>
		///     Pops object from the visited instances stack. Each call to this method
		///     must be matched by a single call to PushCurrentInstance method which returned true.
		/// </summary>
		protected void PopCurrentObject()
		{
			if (_visitedInstances != null && _visitedInstances.Count > 0)
			{
				_visitedInstances.Pop();
			}
		}
	}
}
