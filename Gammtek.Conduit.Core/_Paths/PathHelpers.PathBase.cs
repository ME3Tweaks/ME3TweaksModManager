using System;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private abstract class PathBase : IPath
		{
			protected readonly string CurrentPath;

			protected PathBase(string path)
			{
				Argument.IsNotNullOrEmpty(nameof(path), path);

				// At this point we know pathString is a valid path
				// but we need to normalize and resolve inner dir of path string
				var pathStringNormalized = AbsoluteRelativePathHelpers.NormalizeAndResolveInnerSpecialDir(path);

				CurrentPath = pathStringNormalized;
			}

			public virtual bool HasParentDirectory => MiscHelpers.HasParentDirectory(CurrentPath);

			public abstract bool IsAbsolutePath { get; }

			public abstract bool IsDirectoryPath { get; }

			public abstract bool IsEnvVarPath { get; }

			public abstract bool IsFilePath { get; }

			public abstract bool IsRelativePath { get; }

			public abstract bool IsVariablePath { get; }

			public abstract IDirectoryPath ParentDirectoryPath { get; }

			public abstract PathType PathType { get; }

			public override bool Equals(object obj)
			{
				var path = obj as IPath;

				return !ReferenceEquals(path, null) && PrivateEquals(path);
			}

			public override int GetHashCode()
			{
				return CurrentPath.ToLower().GetHashCode() +
					   (IsAbsolutePath ? 1231 : 5677) +
					   (IsFilePath ? 1457 : 3461);
			}

			public bool IsChildOf(IDirectoryPath parentDirectory)
			{
				Argument.IsNotNull(nameof(parentDirectory), parentDirectory);

				var parentDirectoryString = parentDirectory.ToString();

				// Don't accept equals pathString!
				var parentDirectoryStringLength = parentDirectoryString.Length;

				if (CurrentPath.Length <= parentDirectoryStringLength)
				{
					return false;
				}

				// Possible since at this point (m_PathString.Length > parentDirectoryStringLength)
				var c = CurrentPath[parentDirectoryStringLength];

				// Need to check that char at pos pathStringLength is a separator, 
				// else @"D:/Foo bar" is considered as a child of @"D:/Foo".
				// Note that m_PathString is normalized in ctor, hence its separator(s) are DIR_SEPARATOR_CHAR.
				if (c != MiscHelpers.DirectorySeparatorChar)
				{
					return false;
				}

				var parentPathLowerCase = parentDirectory.ToString().ToLower();
				var thisPathLowerCase = CurrentPath.ToLower();

				return thisPathLowerCase.IndexOf(parentPathLowerCase, StringComparison.Ordinal) == 0;
			}

			public bool NotEquals(object obj)
			{
				return !Equals(obj);
			}

			public override string ToString()
			{
				return CurrentPath;
			}

			private bool PrivateEquals(IPath path)
			{
				if (PathType != path.PathType)
				{
					return false;
				}

				// A FilePath could be equal to a DirectoryPath
				if (IsDirectoryPath != path.IsDirectoryPath)
				{
					return false;
				}

				return string.Compare(CurrentPath, path.ToString(), StringComparison.OrdinalIgnoreCase) == 0;
			}
		}
	}
}
