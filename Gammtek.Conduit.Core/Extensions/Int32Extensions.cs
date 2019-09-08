using System;

namespace Gammtek.Conduit.Extensions
{
	public static class Int32Extensions
	{
		public static int Align(this int value, int align)
		{
			if (value == 0)
			{
				return value;
			}

			return value + ((align - (value % align)) % align);
		}

		public static int BigEndian(this int value)
		{
			return BitConverter.IsLittleEndian ? value.Swap() : value;
		}

		public static int LittleEndian(this int value)
		{
			return BitConverter.IsLittleEndian == false ? value.Swap() : value;
		}

		public static int RotateLeft(this int value, int count)
		{
			return (int) (((uint) value).RotateLeft(count));
		}

		public static int RotateRight(this int value, int count)
		{
			return (int) (((uint) value).RotateRight(count));
		}

		public static int Swap(this int value)
		{
			var uvalue = (uint) value;
			var swapped = ((0x000000FF) & (uvalue >> 24) |
						   (0x0000FF00) & (uvalue >> 8) |
						   (0x00FF0000) & (uvalue << 8) |
						   (0xFF000000) & (uvalue << 24));

			return (int) swapped;
		}

		public static int Invert(this int value)
		{
			var uvalue = (uint) value;
			var swapped = ((0x000000FF) & (uvalue >> 24) |
						   (0x0000FF00) & (uvalue >> 8) |
						   (0x00FF0000) & (uvalue << 8) |
						   (0xFF000000) & (uvalue << 24));
			return (int) swapped;
		}

		public static long Join(this int i, int i2)
		{
			long b = i2;
			b = b << 32;
			b = b | (uint) i;

			return b;
		}

		public static bool ToBoolean(this int value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this int value)
		{
			return Convert.ToByte(value);
		}

		public static char ToChar(this int value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this int value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this int value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this int value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this int value)
		{
			return Convert.ToInt16(value);
		}

		public static long ToInt64(this int value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this int value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this int value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this int value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this int value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this int value)
		{
			return Convert.ToUInt64(value);
		}
	}
}
