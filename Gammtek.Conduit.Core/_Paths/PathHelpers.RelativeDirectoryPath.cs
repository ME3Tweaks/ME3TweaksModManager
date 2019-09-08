using System;
using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class RelativeDirectoryPath : RelativePathBase, IRelativeDirectoryPath
		{
			internal RelativeDirectoryPath(string path)
				: base(path)
			{
				Debug.Assert(path != null);
				Debug.Assert(path.Length > 0);
				Debug.Assert(path.IsValidRelativeDirectoryPath());
			}
			
			public string DirectoryName => MiscHelpers.GetLastName(CurrentPath);
			
			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public override IAbsolutePath GetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				return (this as IRelativeDirectoryPath).GetAbsolutePathFrom(path);
			}

			public IRelativeDirectoryPath GetChildDirectoryPath(string directoryName)
			{
				Argument.IsNotNullOrEmpty(nameof(directoryName), directoryName);

				var pathString = PathBrowsingHelpers.GetChildDirectoryPath(this, directoryName);

				//Debug.Assert(pathString.IsValidRelativeDirectoryPath());

				return new RelativeDirectoryPath(pathString);
			}

			public IRelativeFilePath GetChildFilePath(string fileName)
			{
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);

				var pathString = PathBrowsingHelpers.GetChildFilePath(this, fileName);

				//Debug.Assert(pathString.IsValidRelativeFilePath());

				return new RelativeFilePath(pathString);
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
			
			IAbsoluteDirectoryPath IRelativeDirectoryPath.GetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				Argument.IsNotNull(nameof(path), path);

				string absolutePath, failureMessage;

				if (!AbsoluteRelativePathHelpers.TryGetAbsolutePathFrom(path, this, out absolutePath, out failureMessage))
				{
					throw new ArgumentException(failureMessage);
				}
				
				//Debug.Assert(pathAbsolute != null);
				//Debug.Assert(pathAbsolute.Length > 0);

				return absolutePath.ToAbsoluteDirectoryPath();
			}

			IDirectoryPath IDirectoryPath.GetChildDirectoryPath(string directoryName)
			{
				return GetChildDirectoryPath(directoryName);
			}

			IFilePath IDirectoryPath.GetChildFilePath(string fileName)
			{
				return GetChildFilePath(fileName);
			}

			IDirectoryPath IDirectoryPath.GetSisterDirectoryPath(string directoryName)
			{
				return GetSisterDirectoryPath(directoryName);
			}
			
			IFilePath IDirectoryPath.GetSisterFilePath(string fileName)
			{
				return GetSisterFilePath(fileName);
			}
		}
	}
}
