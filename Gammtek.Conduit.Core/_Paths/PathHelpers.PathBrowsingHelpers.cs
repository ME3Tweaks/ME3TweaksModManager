using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private static class PathBrowsingHelpers
		{
			internal static string GetChildDirectoryPath(IDirectoryPath directoryPath, string directoryName)
			{
				Argument.IsNotNull(nameof(directoryPath), directoryPath);
				Argument.IsNotNullOrEmpty(nameof(directoryName), directoryName);

				return directoryPath.ToString() + MiscHelpers.DirectorySeparatorChar + directoryName;
			}

			internal static string GetChildFilePath(IDirectoryPath directoryPath, string fileName)
			{
				Argument.IsNotNull(nameof(directoryPath), directoryPath);
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);

				return directoryPath.ToString() + MiscHelpers.DirectorySeparatorChar + fileName;
			}

			internal static IDirectoryPath GetSisterDirectoryPath(IPath path, string directoryName)
			{
				Argument.IsNotNull(nameof(path), path);
				Argument.IsNotNullOrEmpty(nameof(directoryName), directoryName);

				return path.ParentDirectoryPath.GetChildDirectoryPath(directoryName);
			}

			internal static IFilePath GetSisterFilePath(IPath path, string fileName)
			{
				Argument.IsNotNull(nameof(path), path);
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);

				return path.ParentDirectoryPath.GetChildFilePath(fileName);
			}

			internal static string UpdateExtension(IFilePath filePath, string extension)
			{
				Argument.IsNotNull(nameof(filePath), filePath);

				Argument.IsNotNull(nameof(extension), extension);
				Argument.IsValid(nameof(extension), extension, extension.Length >= 2 && extension[0] == '.');

				var fileNameBefore = filePath.FileName;
				var fileNameAfter = filePath.FileNameWithoutExtension + extension;
				var filePathString = filePath.ToString();

				//Debug.Assert(filePathString.Length > fileNameBefore.Length);

				var filePathStringWithoutFileName = filePathString.Substring(0, filePathString.Length - fileNameBefore.Length);
				
				return filePathStringWithoutFileName + fileNameAfter;
			}
		}
	}
}
