using System;

namespace Gammtek.Conduit.Extensions
{
	public static class UInt32Extensions
	{
		public static uint Align(this uint value, uint align)
		{
			if (value == 0)
			{
				return value;
			}

			return value + ((align - (value % align)) % align);
		}

		public static uint BigEndian(this uint value)
		{
			return BitConverter.IsLittleEndian ? value.Swap() : value;
		}

		public static uint LittleEndian(this uint value)
		{
			return BitConverter.IsLittleEndian == false ? value.Swap() : value;
		}

		public static uint RotateLeft(this uint value, int count)
		{
			return (value << count) | (value >> (32 - count));
		}

		public static uint RotateRight(this uint value, int count)
		{
			return (value >> count) | (value << (32 - count));
		}

		public static uint Swap(this uint value)
		{
			var swapped = ((0x000000FF) & (value >> 24) |
						   (0x0000FF00) & (value >> 8) |
						   (0x00FF0000) & (value << 8) |
						   (0xFF000000) & (value << 24));

			return swapped;
		}

		public static uint Invert(this uint value)
		{
			var swapped = ((0x000000FF) & (value >> 24) |
						   (0x0000FF00) & (value >> 8) |
						   (0x00FF0000) & (value << 8) |
						   (0xFF000000) & (value << 24));
			return swapped;
		}

		public static bool ToBoolean(this uint value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this uint value)
		{
			return Convert.ToByte(value);
		}

		public static char ToChar(this uint value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this uint value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this uint value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this uint value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this uint value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this uint value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this uint value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this uint value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this uint value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this uint value)
		{
			return Convert.ToUInt16(value);
		}

		public static ulong ToUInt64(this uint value)
		{
			return Convert.ToUInt64(value);
		}
	}
}
