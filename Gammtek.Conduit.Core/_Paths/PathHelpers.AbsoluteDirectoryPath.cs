using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class AbsoluteDirectoryPath : AbsolutePathBase, IAbsoluteDirectoryPath
		{
			internal AbsoluteDirectoryPath(string path)
				: base(path)
			{
				//Debug.Assert(path.IsValidAbsoluteDirectoryPath());
			}

			public IReadOnlyList<IAbsoluteDirectoryPath> ChildrenDirectoriesPath
			{
				get
				{
					var directoryInfo = DirectoryInfo;
					var directoriesInfos = directoryInfo.GetDirectories();
					var childrenDirectoriesPath = directoriesInfos.Select(childDirectoryInfo => ToAbsoluteDirectoryPath(childDirectoryInfo.FullName)).ToList();

					return childrenDirectoriesPath.ToReadOnlyWrappedList();
				}
			}

			public IReadOnlyList<IAbsoluteFilePath> ChildrenFilesPath
			{
				get
				{
					var directoryInfo = DirectoryInfo;
					var filesInfos = directoryInfo.GetFiles();
					var childrenFilesPath = filesInfos.Select(fileInfo => fileInfo.FullName.ToAbsoluteFilePath()).ToList();

					return childrenFilesPath.ToReadOnlyWrappedList();
				}
			}

			public DirectoryInfo DirectoryInfo
			{
				get
				{
					if (!Exists)
					{
						throw new DirectoryNotFoundException(CurrentPath);
					}

					var pathForDirectoryInfo = CurrentPath + MiscHelpers.DirectorySeparatorChar;

					return new DirectoryInfo(pathForDirectoryInfo);
				}
			}

			public string DirectoryName => MiscHelpers.GetLastName(CurrentPath);

			public override bool Exists => Directory.Exists(CurrentPath);

			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public override bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				Argument.IsNotNull(nameof(pivotDirectory), pivotDirectory);

				string pathResultUnused, failureReasonUnused;

				return AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out pathResultUnused, out failureReasonUnused);
			}

			public override bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory, out string failureMessage)
			{
				Argument.IsNotNull(nameof(pivotDirectory), pivotDirectory);

				string pathResultUnused;

				return AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out pathResultUnused, out failureMessage);
			}

			public IAbsoluteDirectoryPath GetChildDirectoryPath(string directoryName)
			{
				Argument.IsNotNullOrEmpty(nameof(directoryName), directoryName);
				
				var pathString = PathBrowsingHelpers.GetChildDirectoryPath(this, directoryName);

				//Debug.Assert(pathString.IsValidAbsoluteDirectoryPath());

				return new AbsoluteDirectoryPath(pathString);
			}

			public IAbsoluteFilePath GetChildFilePath(string fileName)
			{
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);

				var pathString = PathBrowsingHelpers.GetChildFilePath(this, fileName);

				//Debug.Assert(pathString.IsValidAbsoluteFilePath());

				return new AbsoluteFilePath(pathString);
			}

			public override IRelativePath GetRelativePathFrom(IAbsoluteDirectoryPath path)
			{
				return (this as IAbsoluteDirectoryPath).GetRelativePathFrom(path);
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

			IDirectoryPath IDirectoryPath.GetChildDirectoryPath(string directoryName)
			{
				return GetChildDirectoryPath(directoryName);
			}

			IFilePath IDirectoryPath.GetChildFilePath(string fileName)
			{
				return GetChildFilePath(fileName);
			}

			IRelativeDirectoryPath IAbsoluteDirectoryPath.GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				Argument.IsNotNull(nameof(pivotDirectory), pivotDirectory);

				string pathRelativeString, failureReason;

				if (!AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out pathRelativeString, out failureReason))
				{
					throw new ArgumentException(failureReason);
				}

				//Debug.Assert(pathRelativeString != null);
				//Debug.Assert(pathRelativeString.Length > 0);

				return pathRelativeString.ToRelativeDirectoryPath();
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
