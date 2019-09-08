using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Gammtek.Conduit.Collections.Specialized
{
	public abstract class PolymorphicEnum<T, TEnum> : IComparable, IConvertible
		where T : struct, IComparable<T>, IConvertible
		where TEnum : PolymorphicEnum<T, TEnum>, new()
	{
		private static readonly Dictionary<T, TEnum> RegisteredInstances = new Dictionary<T, TEnum>();

		static PolymorphicEnum()
		{
			var enumMembers = typeof (TEnum).GetMembers(
				BindingFlags.Public
				| BindingFlags.Static
				| BindingFlags.GetField);

			foreach (var memberInfo in enumMembers)
			{
				var enumMember = (FieldInfo) memberInfo;
				var enumValue = enumMember.GetValue(null) as TEnum;

				if (enumValue != null)
				{
					enumValue.Name = enumMember.Name;
				}
			}
		}

		public T Ordinal { get; private set; }

		protected object Data { get; private set; }

		private bool IsRegistered
		{
			get { return RegisteredInstances.Values.Contains(this); }
		}

		private string Name { get; set; }

		public static explicit operator PolymorphicEnum<T, TEnum>(T x)
		{
			TEnum enumInstance;

			if (!RegisteredInstances.TryGetValue(x, out enumInstance))
			{
				throw new ArgumentException(string.Format("PolymorphicEnum value {0} not found", x), nameof(x));
			}

			return enumInstance;
		}

		public static implicit operator T(PolymorphicEnum<T, TEnum> x)
		{
			return x.Ordinal;
		}

		public static IEnumerable<TEnum> GetValues()
		{
			return RegisteredInstances.Values.ToArray();
		}

		public static TEnum Parse(string value)
		{
			return Parse(value, false);
		}

		public static TEnum Parse(string value, bool ignoreCase)
		{
			TEnum result;

			if (!TryParse(value, ignoreCase, out result))
			{
				throw new ArgumentException(string.Format("PolymorphicEnum value {0} not found", value), nameof(value));
			}

			return result;
		}

		public static bool TryParse(string value, out TEnum result)
		{
			return TryParse(value, false, out result);
		}

		public static bool TryParse(string value, bool ignoreCase, out TEnum result)
		{
			var instances = RegisteredInstances
				.Values
				.Where(
					e => e.Name.Equals(
						value,
						ignoreCase
							? StringComparison.InvariantCultureIgnoreCase
							: StringComparison.InvariantCulture))
				.ToArray();

			if (instances.Length == 1)
			{
				result = instances[0];
				return true;
			}

			result = default(TEnum);

			return false;
		}

		public int CompareTo(object target)
		{
			var typedTarget = target as PolymorphicEnum<T, TEnum>;

			if (typedTarget == null)
			{
				throw new ArgumentException(@"Comparison can only occur between compatible enums.", nameof(target));
			}

			return Ordinal.CompareTo(typedTarget.Ordinal);
		}

		public override string ToString()
		{
			return Name;
		}

		protected static TEnum Register(T? ordinal = null, object data = null)
		{
			return Register<TEnum>(ordinal, data);
		}

		protected static TEnum Register<TEnumInstance>(T? ordinal = null, object data = null)
			where TEnumInstance : TEnum, new()
		{
			var frame = new StackFrame(1);

			if (frame.GetMethod().Name == "Register")
			{
				frame = new StackFrame(2);
			}

			var enumConstructor = frame.GetMethod();

			if (enumConstructor.DeclaringType != typeof (TEnum))
			{
				throw new EnumInitializationException("Enum members cannot be registered from other enums.");
			}

			if (!ordinal.HasValue)
			{
				ordinal = RegisteredInstances.Any()
					? RegisteredInstances.Keys.Max().PlusOne()
					: default(T);
			}

			TEnum instance = new TEnumInstance();
			instance.Ordinal = ordinal.Value;
			instance.Data = data;

			RegisteredInstances.Add(ordinal.Value, instance);

			return instance;
		}

		protected TEnum Checked(TEnum value)
		{
			if (!value.IsRegistered)
			{
				throw new UnregisteredEnumException("This enum is not registered");
			}

			return value;
		}

		protected void Checked(Action a)
		{
			if (!IsRegistered)
			{
				throw new UnregisteredEnumException("This enum is not registered");
			}

			a();
		}

		protected TReturn Checked<TReturn>(Func<TReturn> f)
		{
			if (!IsRegistered)
			{
				throw new UnregisteredEnumException("This enum is not registered");
			}

			return f();
		}

		TypeCode IConvertible.GetTypeCode()
		{
			return Ordinal.GetTypeCode();
		}

		bool IConvertible.ToBoolean(IFormatProvider provider)
		{
			return Ordinal.ToBoolean(provider);
		}

		byte IConvertible.ToByte(IFormatProvider provider)
		{
			return Ordinal.ToByte(provider);
		}

		char IConvertible.ToChar(IFormatProvider provider)
		{
			return Ordinal.ToChar(provider);
		}

		DateTime IConvertible.ToDateTime(IFormatProvider provider)
		{
			return Ordinal.ToDateTime(provider);
		}

		decimal IConvertible.ToDecimal(IFormatProvider provider)
		{
			return Ordinal.ToDecimal(provider);
		}

		double IConvertible.ToDouble(IFormatProvider provider)
		{
			return Ordinal.ToDouble(provider);
		}

		short IConvertible.ToInt16(IFormatProvider provider)
		{
			return Ordinal.ToInt16(provider);
		}

		int IConvertible.ToInt32(IFormatProvider provider)
		{
			return Ordinal.ToInt32(provider);
		}

		long IConvertible.ToInt64(IFormatProvider provider)
		{
			return Ordinal.ToInt64(provider);
		}

		sbyte IConvertible.ToSByte(IFormatProvider provider)
		{
			return Ordinal.ToSByte(provider);
		}

		float IConvertible.ToSingle(IFormatProvider provider)
		{
			return Ordinal.ToSingle(provider);
		}

		string IConvertible.ToString(IFormatProvider provider)
		{
			return Ordinal.ToString(provider);
		}

		object IConvertible.ToType(Type conversionType, IFormatProvider provider)
		{
			return Ordinal.ToType(conversionType, provider);
		}

		ushort IConvertible.ToUInt16(IFormatProvider provider)
		{
			return Ordinal.ToUInt16(provider);
		}

		uint IConvertible.ToUInt32(IFormatProvider provider)
		{
			return Ordinal.ToUInt32(provider);
		}

		ulong IConvertible.ToUInt64(IFormatProvider provider)
		{
			return Ordinal.ToUInt64(provider);
		}
	}

	public abstract class PolymorphicEnum<TEnum> : PolymorphicEnum<int, TEnum>
		where TEnum : PolymorphicEnum<int, TEnum>, new()
	{}
}
