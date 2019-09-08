using System.Reflection;

namespace Gammtek.Conduit.Extensions.Reflection
{
	public static class AssemblyExtensions
	{
		/// <summary>
		///     Get's the name of the assembly.
		/// </summary>
		/// <param name="assembly">The assembly.</param>
		/// <returns>The assembly's name.</returns>
		public static string GetAssemblyName(this Assembly assembly)
		{
			return assembly.FullName.Remove(assembly.FullName.IndexOf(','));
		}
	}
}
