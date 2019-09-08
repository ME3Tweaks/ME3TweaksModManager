using System;

namespace Gammtek.Conduit.Extensions
{
	public static class UInt64Extensions
	{
		public static ulong Align(this ulong value, ulong align)
		{
			if (value == 0)
			{
				return value;
			}

			return value + ((align - (value % align)) % align);
		}

		public static ulong BigEndian(this ulong value)
		{
			return BitConverter.IsLittleEndian ? value.Swap() : value;
		}

		public static ulong LittleEndian(this ulong value)
		{
			return BitConverter.IsLittleEndian == false ? value.Swap() : value;
		}

		public static ulong RotateLeft(this ulong value, int count)
		{
			return (value << count) | (value >> (64 - count));
		}

		public static ulong RotateRight(this ulong value, int count)
		{
			return (value >> count) | (value << (64 - count));
		}

		public static ulong Swap(this ulong value)
		{
			var swapped = ((0x00000000000000FF) & (value >> 56) |
						   (0x000000000000FF00) & (value >> 40) |
						   (0x0000000000FF0000) & (value >> 24) |
						   (0x00000000FF000000) & (value >> 8) |
						   (0x000000FF00000000) & (value << 8) |
						   (0x0000FF0000000000) & (value << 24) |
						   (0x00FF000000000000) & (value << 40) |
						   (0xFF00000000000000) & (value << 56));

			return swapped;
		}

		public static ulong Invert(this ulong value)
		{
			var swapped = ((0x00000000000000FF) & (value >> 56) |
						   (0x000000000000FF00) & (value >> 40) |
						   (0x0000000000FF0000) & (value >> 24) |
						   (0x00000000FF000000) & (value >> 8) |
						   (0x000000FF00000000) & (value << 8) |
						   (0x0000FF0000000000) & (value << 24) |
						   (0x00FF000000000000) & (value << 40) |
						   (0xFF00000000000000) & (value << 56));
			return swapped;
		}

		public static bool ToBoolean(this ulong value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this ulong value)
		{
			return Convert.ToByte(value);
		}

		public static char ToChar(this ulong value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this ulong value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this ulong value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this ulong value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this ulong value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this ulong value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this ulong value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this ulong value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this ulong value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this ulong value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this ulong value)
		{
			return Convert.ToUInt32(value);
		}
	}
}
