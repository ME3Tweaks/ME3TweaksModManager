using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Gammtek.Conduit.Extensions.Reflection
{
	public static class MemberInfoExtensions
	{
		/// <summary>
		///     Gets all the attributes of a particular type.
		/// </summary>
		/// <typeparam name="T">The type of attributes to get.</typeparam>
		/// <param name="member">The member to inspect for attributes.</param>
		/// <param name="inherit">Whether or not to search for inherited attributes.</param>
		/// <returns>The list of attributes found.</returns>
		public static IEnumerable<T> GetAttributes<T>(this MemberInfo member, bool inherit)
		{
			return Attribute.GetCustomAttributes(member, inherit).OfType<T>();
		}

		/// <summary>
		///     Returns whether property is static.
		/// </summary>
		/// <param name="propertyInfo">Property info.</param>
		public static bool IsStatic(this PropertyInfo propertyInfo)
		{
#if NETFX_CORE || PCL
            return (propertyInfo.CanRead && propertyInfo.GetMethod.IsStatic) || (propertyInfo.CanWrite && propertyInfo.SetMethod.IsStatic);
#else
			return (propertyInfo.CanRead && propertyInfo.GetGetMethod().IsStatic) || (propertyInfo.CanWrite && propertyInfo.GetSetMethod().IsStatic);
#endif
		}
	}
}
