using System;
using System.IO;
using System.Text;

namespace Gammtek.Conduit.Extensions.IO
{
	public static class PathWrapper
	{
		public static string GetRelativePath(FileSystemInfo path1, FileSystemInfo path2)
		{
			if (path1 == null)
			{
				throw new ArgumentNullException(nameof(path1));
			}

			if (path2 == null)
			{
				throw new ArgumentNullException(nameof(path2));
			}

			Func<FileSystemInfo, string> getFullName = delegate(FileSystemInfo path)
			{
				var fullName = path.FullName;

				if (path is DirectoryInfo)
				{
					if (fullName[fullName.Length - 1] != Path.DirectorySeparatorChar)
					{
						fullName += Path.DirectorySeparatorChar;
					}
				}

				return fullName;
			};

			var path1FullName = getFullName(path1);
			var path2FullName = getFullName(path2);

			var uri1 = new Uri(path1FullName);
			var uri2 = new Uri(path2FullName);
			var relativeUri = uri1.MakeRelativeUri(uri2);

			return relativeUri.OriginalString;
		}

		public static string GetRelativePath(string fullPath, string basePath)
		{
			// Require trailing backslash for path
			if (!basePath.EndsWith("\\") || !basePath.EndsWith("/"))
			{
				basePath += "\\";
			}

			var baseUri = new Uri(basePath);
			var fullUri = new Uri(fullPath);

			var relativeUri = baseUri.MakeRelativeUri(fullUri);

			// Uri's use forward slashes so convert back to backward slashes
			return relativeUri.ToString().Replace("/", "\\");
		}

		public static string RelativePath(string absolutePath, string relativeTo)
		{
			var absoluteDirs = absolutePath.Split('\\');
			var relativeDirs = relativeTo.Split('\\');

			// Get the shortest of the two paths
			var len = absoluteDirs.Length < relativeDirs.Length
				? absoluteDirs.Length
				: relativeDirs.Length;

			// Use to determine where in the loop we exited
			var lastCommonRoot = -1;
			int index;

			// Find common root
			for (index = 0; index < len; index++)
			{
				if (absoluteDirs[index] == relativeDirs[index])
				{
					lastCommonRoot = index;
				}
				else
				{
					break;
				}
			}

			// If we didn't find a common prefix then throw
			if (lastCommonRoot == -1)
			{
				throw new ArgumentException("Paths do not have a common base");
			}

			// Build up the relative path
			var relativePath = new StringBuilder();

			// Add on the ..
			for (index = lastCommonRoot + 1; index < absoluteDirs.Length; index++)
			{
				if (absoluteDirs[index].Length > 0)
				{
					relativePath.Append("..\\");
				}
			}

			// Add on the folders
			for (index = lastCommonRoot + 1; index < relativeDirs.Length - 1; index++)
			{
				relativePath.Append(relativeDirs[index] + "\\");
			}

			relativePath.Append(relativeDirs[relativeDirs.Length - 1]);

			return relativePath.ToString();
		}
	}
}
