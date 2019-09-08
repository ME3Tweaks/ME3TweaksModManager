using System;

namespace Gammtek.Conduit.Extensions
{
	/// <summary>
	/// </summary>
	public static class CharExtensions
	{
		public static bool IsDigit(this char c)
		{
			return char.IsDigit(c);
		}

		public static bool IsLetter(this char c)
		{
			return char.IsLetter(c);
		}

		public static bool IsQuote(this char c)
		{
			return c == '\"';
		}

		public static bool IsWhiteSpace(this char c)
		{
			return c > '\0' && c <= ' ';
		}

		public static bool ToBoolean(this char value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this char value)
		{
			return Convert.ToByte(value);
		}

		public static DateTime ToDateTime(this char value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this char value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this char value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this char value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this char value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this char value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this char value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this char value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this char value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this char value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this char value)
		{
			return Convert.ToUInt64(value);
		}
	}
}
