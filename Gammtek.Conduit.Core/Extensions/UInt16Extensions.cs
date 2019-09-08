using System;

namespace Gammtek.Conduit.Extensions
{
	public static class UInt16Extensions
	{
		public static ushort BigEndian(this ushort value)
		{
			return BitConverter.IsLittleEndian ? value.Swap() : value;
		}

		public static ushort LittleEndian(this ushort value)
		{
			return BitConverter.IsLittleEndian == false ? value.Swap() : value;
		}

		public static ushort RotateLeft(this ushort value, int count)
		{
			return (ushort) ((value << count) | (value >> (16 - count)));
		}

		public static ushort RotateRight(this ushort value, int count)
		{
			return (ushort) ((value >> count) | (value << (16 - count)));
		}

		public static ushort Swap(this ushort value)
		{
			var swapped = (ushort) ((0x00FF) & (value >> 8) |
									(0xFF00) & (value << 8));

			return swapped;
		}

		public static ushort Invert(this ushort value)
		{
			var swapped = (ushort) ((0x00FF) & (value >> 8) |
									(0xFF00) & (value << 8));
			return swapped;
		}

		public static bool ToBoolean(this ushort value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this ushort value)
		{
			return Convert.ToByte(value);
		}

		public static char ToChar(this ushort value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this ushort value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this ushort value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this ushort value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this ushort value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this ushort value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this ushort value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this ushort value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this ushort value)
		{
			return Convert.ToSingle(value);
		}

		public static uint ToUInt32(this ushort value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this ushort value)
		{
			return Convert.ToUInt64(value);
		}
	}
}
