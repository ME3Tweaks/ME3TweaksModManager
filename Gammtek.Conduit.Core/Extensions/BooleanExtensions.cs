using System;

namespace Gammtek.Conduit.Extensions
{
	/// <summary>
	/// </summary>
	public static class BooleanExtensions
	{
		public static byte ToByte(this bool value)
		{
			return Convert.ToByte(value);
		}

		public static char ToChar(this bool value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this bool value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this bool value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this bool value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this bool value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this bool value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this bool value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this bool value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this bool value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this bool value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this bool value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this bool value)
		{
			return Convert.ToUInt64(value);
		}
	}
}
