using System;

namespace Gammtek.Conduit.Extensions
{
	public static class SingleExtensions
	{
		public static float BigEndian(this float value)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return value;
			}

			var data = BitConverter.GetBytes(value);
			var junk = BitConverter.ToUInt32(data, 0).Swap();

			return BitConverter.ToSingle(BitConverter.GetBytes(junk), 0);
		}

		public static float LittleEndian(this float value)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return value;
			}

			var data = BitConverter.GetBytes(value);
			var junk = BitConverter.ToUInt32(data, 0).Swap();

			return BitConverter.ToSingle(BitConverter.GetBytes(junk), 0);
		}

		public static float Swap(this float value)
		{
			var data = BitConverter.GetBytes(value);
			var rawValue = BitConverter.ToInt32(data, 0).Swap();

			return BitConverter.ToSingle(BitConverter.GetBytes(rawValue), 0);
		}

		public static float Invert(this float value)
		{
			var data = BitConverter.GetBytes(value);
			var rawValue = BitConverter.ToInt32(data, 0).Invert();

			return BitConverter.ToSingle(BitConverter.GetBytes(rawValue), 0);
		}

		public static bool ToBoolean(this float value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this float value)
		{
			return Convert.ToByte(value);
		}

		public static char ToChar(this float value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this float value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this float value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this float value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this float value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this float value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this float value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this float value)
		{
			return Convert.ToSByte(value);
		}

		public static ushort ToUInt16(this float value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this float value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this float value)
		{
			return Convert.ToUInt64(value);
		}
	}
}
