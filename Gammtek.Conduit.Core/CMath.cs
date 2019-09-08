using System;

namespace Gammtek.Conduit
{
	public static class CMath
	{
		public static T Clamp<T>(this T value, T min, T max)
			where T : IComparable<T>
		{
			var result = value;

			if (result.CompareTo(max) > 0)
			{
				result = max;
			}

			if (result.CompareTo(min) < 0)
			{
				result = min;
			}

			return result;
		}
	}
}
