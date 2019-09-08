using System;

namespace Gammtek.Conduit.Extensions
{
	public static class Int16Extensions
	{
		public static short Invert(this short value)
		{
			var uvalue = (ushort) value;
			var swapped = (ushort) ((0x00FF) & (uvalue >> 8) |
									(0xFF00) & (uvalue << 8));
			return (short) swapped;
		}

		public static short BigEndian(this short value)
		{
			return BitConverter.IsLittleEndian ? value.Swap() : value;
		}

		public static short LittleEndian(this short value)
		{
			return BitConverter.IsLittleEndian == false ? value.Swap() : value;
		}

		public static short RotateLeft(this short value, int count)
		{
			return (short) (((ushort) value).RotateLeft(count));
		}

		public static short RotateRight(this short value, int count)
		{
			return (short) (((ushort) value).RotateRight(count));
		}

		public static short Swap(this short value)
		{
			var uvalue = (ushort) value;
			var swapped = (ushort) ((0x00FF) & (uvalue >> 8) |
									(0xFF00) & (uvalue << 8));

			return (short) swapped;
		}

		public static bool ToBoolean(this short value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this short value)
		{
			return Convert.ToByte(value);
		}

		public static char ToChar(this short value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this short value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this short value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this short value)
		{
			return Convert.ToDouble(value);
		}

		public static int ToInt32(this short value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this short value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this short value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this short value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this short value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this short value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this short value)
		{
			return Convert.ToUInt64(value);
		}
	}
}
