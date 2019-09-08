using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Gammtek.Conduit.IO
{
	public static class ConduitPath
	{
		/// <summary>
		///     Takes a path string and makes all of the path separator characters consistent. Also removes unnecessary multiple separators.
		/// </summary>
		/// <param name="filePath">File path with potentially inconsistent slashes</param>
		/// <param name="directorySeparatorChar"></param>
		/// <returns>File path with consistent separators</returns>
		public static string CleanDirectorySeparators(string filePath, char directorySeparatorChar = '\0')
		{
			StringBuilder cleanPath = null;

			if (directorySeparatorChar == '\0')
			{
				directorySeparatorChar = Environment.OSVersion.Platform == PlatformID.Unix ? '/' : '\\';
			}

			var prevC = '\0';

			// Don't check for double separators until we run across a valid dir name. Paths that start with '//' or '\\' can still be valid.			
			var canCheckDoubleSeparators = false;

			for (var index = 0; index < filePath.Length; ++index)
			{
				var c = filePath[index];

				if (c == '/' || c == '\\')
				{
					if (c != directorySeparatorChar)
					{
						c = directorySeparatorChar;

						if (cleanPath == null)
						{
							cleanPath = new StringBuilder(filePath.Substring(0, index), filePath.Length);
						}
					}

					if (canCheckDoubleSeparators && c == prevC)
					{
						if (cleanPath == null)
						{
							cleanPath = new StringBuilder(filePath.Substring(0, index), filePath.Length);
						}

						continue;
					}
				}
				else
				{
					// First non-separator character, safe to check double separators
					canCheckDoubleSeparators = true;
				}

				cleanPath?.Append(c);

				prevC = c;
			}

			return cleanPath?.ToString() ?? filePath;
		}

		/// <summary>
		///     Gets the executing assembly directory.
		///     This method is using Assembly.CodeBase property to properly resolve original
		///     assembly directory in case shadow copying is enabled.
		/// </summary>
		/// <returns>Absolute path to the directory containing the executing assembly.</returns>
		public static string GetExecutingAssemblyDirectory()
		{
			return Path.GetDirectoryName(GetExecutingAssemblyLocation());
		}

		/// <summary>
		///     Gets the executing assembly path (including filename).
		///     This method is using Assembly.CodeBase property to properly resolve original
		///     assembly path in case shadow copying is enabled.
		/// </summary>
		/// <returns>Absolute path to the executing assembly including the assembly filename.</returns>
		public static string GetExecutingAssemblyLocation()
		{
			return new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
		}

		/// <summary>
		///     Given a file path and a directory, returns a file path that is relative to the specified directory
		/// </summary>
		/// <param name="sourcePath">File path to convert</param>
		/// <param name="relativeToDirectory">
		///     The directory that the source file path should be converted to be relative to.  If this path is not rooted, it will
		///     be assumed to be relative to the current working directory.
		/// </param>
		/// <param name="treatSourceAsDirectory">True if we should treat the source path like a directory even if it doesn't end with a path separator</param>
		/// <returns>Converted relative path</returns>
		public static string MakePathRelativeTo(string sourcePath, string relativeToDirectory, bool treatSourceAsDirectory = false)
		{
			if (string.IsNullOrEmpty(relativeToDirectory))
			{
				// Assume CWD
				relativeToDirectory = ".";
			}

			var absolutePath = sourcePath;

			if (!Path.IsPathRooted(absolutePath))
			{
				absolutePath = Path.GetFullPath(sourcePath);
			}

			var sourcePathEndsWithDirectorySeparator = absolutePath.EndsWith(Path.DirectorySeparatorChar.ToString())
													   || absolutePath.EndsWith(Path.AltDirectorySeparatorChar.ToString());

			if (treatSourceAsDirectory && !sourcePathEndsWithDirectorySeparator)
			{
				absolutePath += Path.DirectorySeparatorChar;
			}

			var absolutePathUri = new Uri(absolutePath);
			var absoluteRelativeDirectory = relativeToDirectory;

			if (!Path.IsPathRooted(absoluteRelativeDirectory))
			{
				absoluteRelativeDirectory = Path.GetFullPath(absoluteRelativeDirectory);
			}

			// Make sure the directory has a trailing directory separator so that the relative directory that
			// MakeRelativeUri creates doesn't include our directory -- only the directories beneath it!
			if (!absoluteRelativeDirectory.EndsWith(Path.DirectorySeparatorChar.ToString())
				&& !absoluteRelativeDirectory.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
			{
				absoluteRelativeDirectory += Path.DirectorySeparatorChar;
			}

			// Convert to URI form which is where we can make the relative conversion happen
			var absoluteRelativeDirectoryUri = new Uri(absoluteRelativeDirectory);

			// Ask the URI system to convert to a nicely formed relative path, then convert it back to a regular path string
			var uriRelativePath = absoluteRelativeDirectoryUri.MakeRelativeUri(absolutePathUri);
			var relativePath = Uri.UnescapeDataString(uriRelativePath.ToString()).Replace('/', Path.DirectorySeparatorChar);

			// If we added a directory separator character earlier on, remove it now
			if (!sourcePathEndsWithDirectorySeparator && treatSourceAsDirectory && relativePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
			{
				relativePath = relativePath.Substring(0, relativePath.Length - 1);
			}

			return relativePath;
		}
	}
}
