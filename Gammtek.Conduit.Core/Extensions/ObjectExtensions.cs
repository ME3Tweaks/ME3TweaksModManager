using System;
using System.Linq;

namespace Gammtek.Conduit.Extensions
{
	public static class ObjectExtensions
	{
		[ContractAnnotation("value:null => true")]
		public static bool IsNull<T>(this T value)
			where T : class
		{
			return value == null;
		}

		/// <summary>
		///     Converts the list of objects to an array of attributes, very easy to use during GetCustomAttribute reflection.
		/// </summary>
		/// <param name="objects">The object array, can be <c>null</c>.</param>
		/// <returns>Attribute array or empty array if <paramref name="objects" /> is <c>null</c>.</returns>
		public static Attribute[] ToAttributeArray(this object[] objects)
		{
			return objects?.Cast<Attribute>().ToArray()
				?? new Attribute[] { };
		}
	}
}
