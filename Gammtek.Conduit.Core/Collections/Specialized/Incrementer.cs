using System;
using System.Collections.Generic;

namespace Gammtek.Conduit.Collections.Specialized
{
	public static class Incrementer
	{
		private static readonly Dictionary<Type, object> Incrementers;

		static Incrementer()
		{
			Incrementers =
				new Dictionary<Type, object>
				{
					{ typeof (sbyte), GetFunc<sbyte>(i => (sbyte) (i + 1)) },
					{ typeof (byte), GetFunc<byte>(i => (byte) (i + 1)) },
					{ typeof (short), GetFunc<short>(i => (short) (i + 1)) },
					{ typeof (ushort), GetFunc<ushort>(i => (ushort) (i + 1)) },
					{ typeof (int), GetFunc<int>(i => i + 1) },
					{ typeof (uint), GetFunc<uint>(i => i + 1) },
					{ typeof (long), GetFunc<long>(i => i + 1) },
					{ typeof (ulong), GetFunc<ulong>(i => i + 1) }
				};
		}

		public static T PlusOne<T>(this T value)
			where T : struct
		{
			object incrementer;

			if (!Incrementers.TryGetValue(typeof (T), out incrementer))
			{
				throw new NotSupportedException("This type is not supported.");
			}

			// Invoke
			return ((Func<T, T>) incrementer)(value);
		}

		private static Func<T, T> GetFunc<T>(Func<T, T> f)
		{
			return f;
		}
	}
}
