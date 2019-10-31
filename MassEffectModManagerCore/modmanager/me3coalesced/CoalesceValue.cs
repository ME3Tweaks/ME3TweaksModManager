using System;

namespace MassEffect3.Coalesce
{
	public struct CoalesceValue : IEquatable<CoalesceValue>
	{
		public CoalesceValue(string value = null, int? valueType = null)
			: this()
		{
			Value = value;
			ValueType = valueType ?? CoalesceProperty.DefaultValueType;
		}

		public bool IsNull
		{
			get { return Value == null || ValueType == CoalesceProperty.NullValueType; }
		}

		public CoalesceParseAction ParseAction
		{
			get
			{
				switch (ValueType)
				{
					case 0:
					{
						return CoalesceParseAction.New;
					}
					case 1:
					{
						return CoalesceParseAction.RemoveProperty;
					}
					case 2:
					{
						return CoalesceParseAction.Add;
					}
					case 3:
					{
						return CoalesceParseAction.AddUnique;
					}
					case 4:
					{
						return CoalesceParseAction.Remove;
					}
					default:
					{
						return CoalesceParseAction.None;
					}
				}
			}
		}

		public string Value { get; set; }

		public int ValueType { get; set; }

		public static bool operator ==(CoalesceValue v1, CoalesceValue v2)
		{
			return v1.Equals(v2);
		}

		public static implicit operator CoalesceValue(string value)
		{
			return new CoalesceValue(value);
		}

		public static implicit operator string(CoalesceValue value)
		{
			return value.Value;
		}

		public static bool operator !=(CoalesceValue v1, CoalesceValue v2)
		{
			return !(v1 == v2);
		}

		public bool Equals(CoalesceValue other)
		{
			return Equals(other.Value) && ValueType == other.ValueType;
		}

		public bool Equals(string other)
		{
			return string.Equals(Value, other, StringComparison.InvariantCultureIgnoreCase);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
			{
				return false;
			}

			return obj is CoalesceValue && Equals((CoalesceValue) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Value != null ? Value.GetHashCode() : 0) * 397) ^ ValueType;
			}
		}
	}
}
