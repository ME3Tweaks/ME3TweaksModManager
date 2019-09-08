using System;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Contains information about value (property or field) exposed by the processed object.
	///     This class is internal and cannot be used directly.
	///     It is rather used by GeneralFormatInfo class to describe members of the formatted object.
	/// </summary>
	internal class GeneralMemberDescription : IComparable, IComparable<GeneralMemberDescription>, IEquatable<GeneralMemberDescription>
	{
		/// <summary>
		///     Data type of the contained object.
		/// </summary>
		private Type _dataType;

		/// <summary>
		///     Name of the contained object.
		/// </summary>
		private string _name;

		/// <summary>
		///     Value of the contained object.
		/// </summary>
		private object _value;

		/// <summary>
		///     Default constructor.
		/// </summary>
		public GeneralMemberDescription() {}

		/// <summary>
		///     Constructor which initializes contained values.
		/// </summary>
		/// <param name="dataType">Type of the contained object.</param>
		/// <param name="name">Name of the contained object.</param>
		/// <param name="value">Value of the contained object.</param>
		public GeneralMemberDescription(Type dataType, string name, object value)
		{
			_dataType = dataType;
			_name = name;
			_value = value;
		}

		/// <summary>
		///     Gets or sets type of the contained object.
		/// </summary>
		public Type DataType
		{
			get { return _dataType; }
			set { _dataType = value; }
		}

		/// <summary>
		///     Gets or sets name of the contained object.
		/// </summary>
		public string Name
		{
			get { return _name; }
			set { _name = value; }
		}

		/// <summary>
		///     Gets or sets value of the contained object.
		/// </summary>
		public object Value
		{
			get { return _value; }
			set { _value = value; }
		}

		/// <summary>
		///     Compares this instance with other instance.
		/// </summary>
		/// <param name="other">Instance with which this instance is compared.</param>
		/// <returns>
		///     -1, 0 or +1 if <paramref name="other" /> is GeneralMemberDescription, in which case result
		///     is actual result of comparison between this instance's Name and <paramref name="other" />'s Name.
		///     Returns +1 if <paramref name="other" /> is null or is not GeneralMemberDescription.
		/// </returns>
		public int CompareTo(object other)
		{
			var result = 1;

			if (!ReferenceEquals(other, null) && other.GetType() == typeof (GeneralMemberDescription))
			{
				result = CompareTo((GeneralMemberDescription) other);
			}

			return result;
		}

		/// <summary>
		///     Compares this instance with specified instance by actually comparing their names.
		/// </summary>
		/// <param name="other">Other instance with which this instance is compared.</param>
		/// <returns>
		///     -1 if this instance's name is lexicographically smaller than <paramref name="other" />'s name;
		///     0 if two names are equal; +1 if this instance's name is lexicographically larger than
		///     <paramref name="other" />'s name. Null name is considered smaller than any non-null name.
		/// </returns>
		public int CompareTo(GeneralMemberDescription other)
		{
			var result = 1;

			if (!ReferenceEquals(other, null))
			{
				if (_name == null && other._name == null)
				{
					result = 0;
				}
				else if (_name != null && other._name == null)
				{
					result = 1;
				}
				else if (_name == null && other._name != null)
				{
					result = -1;
				}
				else
				{
					result = _name.CompareTo(other._name);
				}
			}

			return result;
		}

		/// <summary>
		///     Tests whether this instance is the same as given other instance. Two instances are considered
		///     the same if they have the same name.
		/// </summary>
		/// <param name="other">Instance to which this instance is compared for equality.</param>
		/// <returns>true if this instance has the same name as <paramref name="other" />; otherwise false.</returns>
		public bool Equals(GeneralMemberDescription other)
		{
			return CompareTo(other) == 0;
		}

		/// <summary>
		///     Converts this instance into user-friendly string.
		/// </summary>
		/// <returns>String which represents contents of this instance.</returns>
		public override string ToString()
		{
			var formatProvider = FormatInfoUtils.CreateSingleLinedFormatter();
			formatProvider.InstanceDataType = _dataType;
			formatProvider.InstanceName = _name;

			return string.Format(formatProvider, "{0}", _value);
		}
	}
}
