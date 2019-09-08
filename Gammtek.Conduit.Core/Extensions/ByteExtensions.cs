using System;

namespace Gammtek.Conduit.Extensions
{
	/// <summary>
	/// </summary>
	public static class ByteExtensions
	{
		public static byte RotateLeft(this byte value, int count)
		{
			return (byte) ((value << count) | (value >> (8 - count)));
		}

		public static byte RotateRight(this byte value, int count)
		{
			return (byte) ((value >> count) | (value << (8 - count)));
		}

		public static bool ToBoolean(this byte value)
		{
			return Convert.ToBoolean(value);
		}

		public static char ToChar(this byte value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this byte value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this byte value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this byte value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this byte value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this byte value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this byte value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this byte value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this byte value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this byte value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this byte value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this byte value)
		{
			return Convert.ToUInt64(value);
		}
	}
}
