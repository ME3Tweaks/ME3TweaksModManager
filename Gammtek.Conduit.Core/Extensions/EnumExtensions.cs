using System;
using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.Extensions
{
	public static class EnumExtensions
	{
		public static Array Values(this Enum value)
		{
			return Enum.GetValues(value.GetType());
		}

		public static bool Has<T>(this Enum value, T check)
		{
			var type = value.GetType();

			//determine the values
			var parsed = new Value(check, type);

			if (parsed.Signed.HasValue)
			{
				return (Convert.ToInt64(value) &
						(long) parsed.Signed) == (long) parsed.Signed;
			}

			if (parsed.Unsigned.HasValue)
			{
				return (Convert.ToUInt64(value) &
						(ulong) parsed.Unsigned) == (ulong) parsed.Unsigned;
			}

			return false;
		}

		public static bool Missing<T>(this Enum obj, T value)
		{
			return !Has(obj, value);
		}

		public static T Include<T>(this Enum value, T append)
		{
			return Incorporate(value, append);
		}

		public static void Inject<T>(this Enum append, ref T value)
		{
			value = Incorporate(append, value);
		}

		private static T Incorporate<T>(Enum value, T append)
		{
			var type = value.GetType();

			//determine the values
			object result = value;
			var parsed = new Value(append, type);

			if (parsed.Signed.HasValue)
			{
				result = Convert.ToInt64(value) | (long) parsed.Signed;
			}
			else if (parsed.Unsigned.HasValue)
			{
				result = Convert.ToUInt64(value) | (ulong) parsed.Unsigned;
			}

			//return the final value
			return (T) Enum.Parse(type, result.ToString());
		}

		public static T Remove<T>(this Enum value, T remove)
		{
			return Relegate(value, remove);
		}

		public static void Expel<T>(this Enum remove, ref T value)
		{
			value = Relegate(remove, value);
		}

		private static T Relegate<T>(Enum value, T remove)
		{
			var type = value.GetType();

			//determine the values
			object result = value;
			var parsed = new Value(remove, type);

			if (parsed.Signed.HasValue)
			{
				result = Convert.ToInt64(value) & ~(long) parsed.Signed;
			}
			else if (parsed.Unsigned.HasValue)
			{
				result = Convert.ToUInt64(value) & ~(ulong) parsed.Unsigned;
			}

			//return the final value
			return (T) Enum.Parse(type, result.ToString());
		}

		//class to simplfy narrowing values between 
		//a ulong and long since either value should
		//cover any lesser value
		private class Value
		{
			public readonly long? Signed;
			public readonly ulong? Unsigned;

			public Value(object value, Type type)
			{
				//make sure it is even an enum to work with
				if (!type.IsEnum)
				{
					throw new
						ArgumentException("Value provided is not an enumerated type!");
				}

				//then check for the enumerated value
				var compare = Enum.GetUnderlyingType(type);

				//if this is an unsigned long then the only
				//value that can hold it would be a ulong
				if (compare == typeof (long) || compare == typeof (ulong))
				{
					Unsigned = Convert.ToUInt64(value);
				}
				else
				{
					Signed = Convert.ToInt64(value);
				}
			}
		}

		public static T ToEnumFlags<T>(this IEnumerable<string> value) where T : struct
		{
			if (!typeof (T).IsEnum)
			{
				throw new NotSupportedException(typeof(T).Name + " is not an Enum");
			}

			T flags;

			var validValues = value.Where(s => Enum.TryParse(s, true, out flags));
			var commaSeparatedFlags = string.Join(",", validValues);

			Enum.TryParse(commaSeparatedFlags, true, out flags);

			return flags;
		}
	}
}
