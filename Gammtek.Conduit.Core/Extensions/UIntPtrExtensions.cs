using System;

namespace Gammtek.Conduit.Extensions
{
	/// <summary>
	/// </summary>
	public static class UIntPtrExtensions
	{
		/// <summary>
		/// </summary>
		/// <param name="ptr"></param>
		/// <returns></returns>
		public static IntPtr ToIntPtr(this UIntPtr ptr)
		{
			return unchecked((IntPtr) (long) (ulong) ptr);
		}
	}
}
