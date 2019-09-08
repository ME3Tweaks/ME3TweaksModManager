using System;

namespace Gammtek.Conduit.Extensions
{
	public static class Int64Extensions
	{
		public static long Align(this long value, long align)
		{
			if (value == 0)
			{
				return value;
			}

			return value + ((align - (value % align)) % align);
		}

		public static long BigEndian(this long value)
		{
			return BitConverter.IsLittleEndian ? value.Swap() : value;
		}

		public static long LittleEndian(this long value)
		{
			return BitConverter.IsLittleEndian == false ? value.Swap() : value;
		}

		public static long RotateLeft(this long value, int count)
		{
			return (long) (((ulong) value).RotateLeft(count));
		}

		public static long RotateRight(this long value, int count)
		{
			return (long) (((ulong) value).RotateRight(count));
		}

		public static long Swap(this long value)
		{
			var uvalue = (ulong) value;
			var swapped = ((0x00000000000000FF) & (uvalue >> 56) |
						   (0x000000000000FF00) & (uvalue >> 40) |
						   (0x0000000000FF0000) & (uvalue >> 24) |
						   (0x00000000FF000000) & (uvalue >> 8) |
						   (0x000000FF00000000) & (uvalue << 8) |
						   (0x0000FF0000000000) & (uvalue << 24) |
						   (0x00FF000000000000) & (uvalue << 40) |
						   (0xFF00000000000000) & (uvalue << 56));

			return (long) swapped;
		}

		public static long Invert(this long value)
		{
			var uvalue = (ulong) value;
			var swapped = ((0x00000000000000FF) & (uvalue >> 56) |
						   (0x000000000000FF00) & (uvalue >> 40) |
						   (0x0000000000FF0000) & (uvalue >> 24) |
						   (0x00000000FF000000) & (uvalue >> 8) |
						   (0x000000FF00000000) & (uvalue << 8) |
						   (0x0000FF0000000000) & (uvalue << 24) |
						   (0x00FF000000000000) & (uvalue << 40) |
						   (0xFF00000000000000) & (uvalue << 56));
			return (long) swapped;
		}

		public static void Split(this long l, out int i1, out int i2)
		{
			i1 = (int) (l & uint.MaxValue);
			i2 = (int) (l >> 32);
		}

		public static bool ToBoolean(this long value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this long value)
		{
			return Convert.ToByte(value);
		}

		public static char ToChar(this long value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this long value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this long value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this long value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this long value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this long value)
		{
			return Convert.ToInt32(value);
		}

		public static sbyte ToSByte(this long value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this long value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this long value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this long value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this long value)
		{
			return Convert.ToUInt64(value);
		}
	}
}
