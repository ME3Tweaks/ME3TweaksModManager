using System;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private static class FileNameHelpers
		{
			internal static string GetFileName(string path)
			{
				Argument.IsNotNull(nameof(path), path);

				return MiscHelpers.GetLastName(path);
			}

			internal static string GetFileNameExtension(string path)
			{
				Argument.IsNotNull(nameof(path), path);

				var fileName = MiscHelpers.GetLastName(path);
				var index = fileName.LastIndexOf('.');

				if (index == -1)
				{
					return string.Empty;
				}

				return index == fileName.Length - 1 
					? string.Empty 
					: fileName.Substring(index, fileName.Length - index);
			}

			internal static string GetFileNameWithoutExtension(string path)
			{
				Argument.IsNotNull(nameof(path), path);

				var fileName = GetFileName(path);
				var extension = GetFileNameExtension(path);

				return string.IsNullOrEmpty(extension) 
					? fileName :
					fileName.Substring(0, fileName.Length - extension.Length);

				//Debug.Assert(fileName.Length - extension.Length >= 0);
			}

			internal static bool HasExtension(string path, string extension)
			{
				Argument.IsNotNull(nameof(extension), extension);
				Argument.IsValid(nameof(extension), extension, extension.Length >= 2 && extension[0] == '.');

				var pathExtension = GetFileNameExtension(path);

				return (string.Compare(pathExtension, extension, StringComparison.OrdinalIgnoreCase) == 0);
			}
		}
	}
}
