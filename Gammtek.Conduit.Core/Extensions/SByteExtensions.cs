using System;

namespace Gammtek.Conduit.Extensions
{
	public static class SByteExtensions
	{
		public static sbyte RotateLeft(this sbyte value, int count)
		{
			return (sbyte) (((byte) value).RotateLeft(count));
		}

		public static sbyte RotateRight(this sbyte value, int count)
		{
			return (sbyte) (((byte) value).RotateRight(count));
		}

		public static bool ToBoolean(this sbyte value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this sbyte value)
		{
			return Convert.ToByte(value);
		}

		public static char ToChar(this sbyte value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this sbyte value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this sbyte value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this sbyte value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this sbyte value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this sbyte value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this sbyte value)
		{
			return Convert.ToInt64(value);
		}

		public static float ToSingle(this sbyte value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this sbyte value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this sbyte value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this sbyte value)
		{
			return Convert.ToUInt64(value);
		}
	}
}
