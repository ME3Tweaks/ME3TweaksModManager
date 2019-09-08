using System;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class RelativeFilePath : RelativePathBase, IRelativeFilePath
		{
			internal RelativeFilePath(string path)
				: base(path)
			{
				//Debug.Assert(path.IsValidRelativeFilePath());
			}

			public string FileExtension => FileNameHelpers.GetFileNameExtension(CurrentPath);
			
			public string FileName => FileNameHelpers.GetFileName(CurrentPath);

			public string FileNameWithoutExtension => FileNameHelpers.GetFileNameWithoutExtension(CurrentPath);
			
			public override bool IsDirectoryPath => false;

			public override bool IsFilePath => true;

			public override IAbsolutePath GetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				return (this as IRelativeFilePath).GetAbsolutePathFrom(path);
			}

			public IRelativeDirectoryPath GetSisterDirectoryPath(string directoryName)
			{
				Argument.IsNotNullOrEmpty(nameof(directoryName), directoryName);

				var path = PathBrowsingHelpers.GetSisterDirectoryPath(this, directoryName);

				return path as IRelativeDirectoryPath;
			}
			
			public IRelativeFilePath GetSisterFilePath(string fileName)
			{
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);

				var path = PathBrowsingHelpers.GetSisterFilePath(this, fileName);

				return path as IRelativeFilePath;
			}

			public bool HasExtension(string extension)
			{
				Argument.IsNotNull(nameof(extension), extension);
				Argument.IsValid(nameof(extension), extension, extension.Length >= 2 && extension[0] == '.');

				return FileNameHelpers.HasExtension(CurrentPath, extension);
			}

			public IRelativeFilePath UpdateExtension(string extension)
			{
				Argument.IsNotNull(nameof(extension), extension);
				Argument.IsValid(nameof(extension), extension, extension.Length >= 2 && extension[0] == '.');

				var path = PathBrowsingHelpers.UpdateExtension(this, extension);

				//Debug.Assert(pathString.IsValidRelativeFilePath());

				return new RelativeFilePath(path);
			}
			
			IAbsoluteFilePath IRelativeFilePath.GetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				Argument.IsNotNull(nameof(path), path);

				string absolutePath, failureMessage;

				if (!AbsoluteRelativePathHelpers.TryGetAbsolutePathFrom(path, this, out absolutePath, out failureMessage))
				{
					throw new ArgumentException(failureMessage);
				}

				//Debug.Assert(pathAbsolute != null);
				//Debug.Assert(pathAbsolute.Length > 0);

				return (absolutePath + MiscHelpers.DirectorySeparatorChar + FileName).ToAbsoluteFilePath();
			}

			IDirectoryPath IFilePath.GetSisterDirectoryPath(string directoryName)
			{
				return GetSisterDirectoryPath(directoryName);
			}
			
			IFilePath IFilePath.GetSisterFilePath(string fileName)
			{
				return GetSisterFilePath(fileName);
			}

			IFilePath IFilePath.UpdateExtension(string extension)
			{
				return UpdateExtension(extension);
			}
		}
	}
}
