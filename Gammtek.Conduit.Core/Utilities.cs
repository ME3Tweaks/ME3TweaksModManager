using System;
using System.Collections.Generic;

namespace Gammtek.Conduit
{
	public class Utilities
	{
		/// <summary>
		///     Correctly collapses any ../ or ./ entries in a path.
		/// </summary>
		/// <param name="inPath">The path to be collapsed</param>
		/// <returns>true if the path could be collapsed, false otherwise.</returns>
		public static bool CollapseRelativeDirectories(ref string inPath)
		{
			var localString = inPath;
			var hadBackSlashes = false;

			// look to see what kind of slashes we had
			if (localString.IndexOf("\\", StringComparison.CurrentCulture) != -1)
			{
				localString = localString.Replace("\\", "/");
				hadBackSlashes = true;
			}

			const string parentDir = "/..";
			var parentDirLength = parentDir.Length;

			//for (;;)
			while (true)
			{
				// An empty path is finished
				if (string.IsNullOrEmpty(localString))
				{
					break;
				}

				// Consider empty paths or paths which start with .. or /.. as invalid
				if (localString.StartsWith("..") || localString.StartsWith(parentDir))
				{
					return false;
				}

				// If there are no "/.."s left then we're done
				var index = localString.IndexOf(parentDir, StringComparison.CurrentCulture);

				if (index == -1)
				{
					break;
				}

				var previousSeparatorIndex = index;

				//for (;;)
				while (true)
				{
					// Find the previous slash
					previousSeparatorIndex = Math.Max(0, localString.LastIndexOf("/", previousSeparatorIndex - 1, StringComparison.CurrentCulture));

					// Stop if we've hit the start of the string
					if (previousSeparatorIndex == 0)
					{
						break;
					}

					// Stop if we've found a directory that isn't "/./"
					if ((index - previousSeparatorIndex) > 1 && (localString[previousSeparatorIndex + 1] != '.' || localString[previousSeparatorIndex + 2] != '/'))
					{
						break;
					}
				}

				// If we're attempting to remove the drive letter, that's illegal
				var colon = localString.IndexOf(":", previousSeparatorIndex, StringComparison.Ordinal);

				if (colon >= 0 && colon < index)
				{
					return false;
				}

				localString = localString.Substring(0, previousSeparatorIndex) + localString.Substring(index + parentDirLength);
			}

			localString = localString.Replace("./", "");

			// restore back slashes now
			if (hadBackSlashes)
			{
				localString = localString.Replace("/", "\\");
			}

			// and pass back out
			inPath = localString;

			return true;
		}

		/// <summary>
		///     Expands variables in $(VarName) format in the given string. Variables are retrieved from the given dictionary, or through the environment of the
		///     current process.
		///     Any unknown variables are ignored.
		/// </summary>
		/// <param name="inputString">String to search for variable names</param>
		/// <param name="additionalVariables">Lookup of variable names to values</param>
		/// <returns>String with all variables replaced</returns>
		public static string ExpandVariables(string inputString, Dictionary<string, string> additionalVariables = null)
		{
			var result = inputString;

			for (var idx = result.IndexOf("${", StringComparison.CurrentCulture); idx != -1; idx = result.IndexOf("${", idx, StringComparison.CurrentCulture))
			{
				// Find the end of the variable name
				var endIdx = result.IndexOf('}', idx + 2);

				if (endIdx == -1)
				{
					break;
				}

				// Extract the variable name from the string
				var name = result.Substring(idx + 2, endIdx - (idx + 2));

				// Find the value for it, either from the dictionary or the environment block
				string value;

				if (additionalVariables == null || !additionalVariables.TryGetValue(name, out value))
				{
					value = Environment.GetEnvironmentVariable(name);

					if (value == null)
					{
						idx = endIdx + 1;

						continue;
					}
				}

				// Replace the variable, or skip past it
				result = result.Substring(0, idx) + value + result.Substring(endIdx + 1);
			}

			return result;
		}

		/// <summary>
		///     Checks if given type implements given interface.
		/// </summary>
		/// <typeparam name="TInterfaceType">Interface to check.</typeparam>
		/// <param name="type">Type to check.</param>
		/// <returns>True if TestType implements InterfaceType. False otherwise.</returns>
		public static bool ImplementsInterface<TInterfaceType>(Type type)
		{
			return Array.IndexOf(type.GetInterfaces(), typeof (TInterfaceType)) != -1;
		}
	}
}
