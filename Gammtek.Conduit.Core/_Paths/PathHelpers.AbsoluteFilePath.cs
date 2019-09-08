using System;
using System.IO;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class AbsoluteFilePath : AbsolutePathBase, IAbsoluteFilePath
		{
			internal AbsoluteFilePath(string path)
				: base(path)
			{
				//Debug.Assert(path.IsValidAbsoluteFilePath());
			}

			public override bool Exists => File.Exists(CurrentPath);

			public string FileExtension => FileNameHelpers.GetFileNameExtension(CurrentPath);

			public FileInfo FileInfo
			{
				get
				{
					if (!Exists)
					{
						throw new FileNotFoundException(CurrentPath);
					}

					return new FileInfo(CurrentPath);
				}
			}
			
			public string FileName => FileNameHelpers.GetFileName(CurrentPath);

			public string FileNameWithoutExtension => FileNameHelpers.GetFileNameWithoutExtension(CurrentPath);
			
			public override bool IsDirectoryPath => false;

			public override bool IsFilePath => true;

			public override bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				Argument.IsNotNull(nameof(pivotDirectory), pivotDirectory);

				string relativePath, failureMessage;

				return AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out relativePath, out failureMessage);
			}

			public override bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory, out string failureMessage)
			{
				Argument.IsNotNull(nameof(pivotDirectory), pivotDirectory);

				string relativePath;

				return AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out relativePath, out failureMessage);
			}

			public override IRelativePath GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				Argument.IsNotNull(nameof(pivotDirectory), pivotDirectory);

				return (this as IAbsoluteFilePath).GetRelativePathFrom(pivotDirectory);
			}

			public IAbsoluteDirectoryPath GetSisterDirectoryPath(string directoryName)
			{
				Argument.IsNotNullOrEmpty(nameof(directoryName), directoryName);

				var path = PathBrowsingHelpers.GetSisterDirectoryPath(this, directoryName);

				return path as IAbsoluteDirectoryPath;
			}
			
			public IAbsoluteFilePath GetSisterFilePath(string fileName)
			{
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);

				var path = PathBrowsingHelpers.GetSisterFilePath(this, fileName);

				return path as IAbsoluteFilePath;
			}

			public bool HasExtension(string extension)
			{
				Argument.IsNotNull(nameof(extension), extension);
				Argument.IsValid(nameof(extension), extension, extension.Length >= 2 && extension[0] == '.');
				
				return FileNameHelpers.HasExtension(CurrentPath, extension);
			}

			public IAbsoluteFilePath UpdateExtension(string extension)
			{
				Argument.IsNotNull(nameof(extension), extension);
				Argument.IsValid(nameof(extension), extension, extension.Length >= 2 && extension[0] == '.');

				var path = PathBrowsingHelpers.UpdateExtension(this, extension);

				//Debug.Assert(pathString.IsValidAbsoluteFilePath());

				return new AbsoluteFilePath(path);
			}
			
			IRelativeFilePath IAbsoluteFilePath.GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				Argument.IsNotNull(nameof(pivotDirectory), pivotDirectory);

				string relativePath, failureMessage;

				if (!AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out relativePath, out failureMessage))
				{
					throw new ArgumentException(failureMessage);
				}

				//Debug.Assert(pathRelative != null);
				//Debug.Assert(pathRelative.Length > 0);

				return new RelativeFilePath(relativePath + MiscHelpers.DirectorySeparatorChar + FileName);
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
