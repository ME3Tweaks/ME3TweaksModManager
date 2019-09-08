using System;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private abstract class AbsolutePathBase : PathBase, IAbsolutePath
		{
			protected AbsolutePathBase(string path)
				: base(path)
			{
				Type = UncPathHelper.IsAnAbsoluteUncPath(CurrentPath) 
					? AbsolutePathType.UNC 
					: AbsolutePathType.DriveLetter;
			}

			public IPathDriveLetter DriveLetter
			{
				get
				{
					//Debug.Assert(IsAbsolutePath);
					//Debug.Assert(CurrentPath.Length > 0);

					if (Type != AbsolutePathType.DriveLetter)
					{
						throw new InvalidOperationException($"The property {nameof(DriveLetter)} must be called on a path that is a type of DriveLetter.");
					}

					var driveName = CurrentPath[0].ToString();

					//Debug.Assert(PathHelpers.DriveLetter.IsValidDriveName(driveName));

					return new PathDriveLetter(driveName);
				}
			}

			public abstract bool Exists { get; }

			public override bool HasParentDirectory
			{
				get
				{
					var path = CurrentPath;

					if (Type != AbsolutePathType.UNC)
					{
						return MiscHelpers.HasParentDirectory(path);
					}

					string serverShareStart;

					path = UncPathHelper.ConvertUncToDriveLetter(CurrentPath, out serverShareStart);

					return MiscHelpers.HasParentDirectory(path);
				}
			}

			public override bool IsAbsolutePath => true;

			public override bool IsEnvVarPath => false;

			public override bool IsRelativePath => false;

			public override bool IsVariablePath => false;

			public AbsolutePathType Type { get; }

			public override IDirectoryPath ParentDirectoryPath => (this as IAbsolutePath).ParentDirectoryPath;

			public override PathType PathType => PathType.Absolute;

			public string UNCServer
			{
				get
				{
					if (Type != AbsolutePathType.UNC)
					{
						throw new InvalidOperationException("The property getter UNCServer must be called on a pathString of kind UNC.");
					}

					// Here we already checked the m_PathString is UNC, hence it is like "\\server\share" with maybe a file path after!
					//Debug.Assert(CurrentPath.IndexOf(MiscHelpers.TWO_DIR_SEPARATOR_STRING, StringComparison.Ordinal) == 0);

					var twoSeparatorLength = MiscHelpers.DoubleDirectorySeparator.Length;
					var index = CurrentPath.IndexOf(MiscHelpers.DirectorySeparatorChar, twoSeparatorLength);

					//Debug.Assert(index > twoSeparatorLength);

					return CurrentPath.Substring(twoSeparatorLength, index - twoSeparatorLength);
				}
			}

			public string UNCShare
			{
				get
				{
					if (Type != AbsolutePathType.UNC)
					{
						throw new InvalidOperationException("The property getter UNCShare must be called on a pathString of kind UNC.");
					}

					// Here we already checked the m_PathString is UNC, hence it is like "\\server\share" with maybe a file path after!
					//Debug.Assert(CurrentPath.IndexOf(MiscHelpers.TWO_DIR_SEPARATOR_STRING, StringComparison.Ordinal) == 0);

					var indexShareBegin = CurrentPath.IndexOf(MiscHelpers.DirectorySeparatorChar, 2);

					//Debug.Assert(indexShareBegin > 2);

					indexShareBegin++;

					var indexShareEnd = CurrentPath.IndexOf(MiscHelpers.DirectorySeparatorChar, indexShareBegin);

					if (indexShareEnd == -1)
					{
						indexShareEnd = CurrentPath.Length;
					}

					//Debug.Assert(indexShareEnd > indexShareBegin);

					return CurrentPath.Substring(indexShareBegin, indexShareEnd - indexShareBegin);
				}
			}

			IAbsoluteDirectoryPath IAbsolutePath.ParentDirectoryPath => MiscHelpers.GetParentDirectory(CurrentPath).ToAbsoluteDirectoryPath();

			public abstract bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory);

			public abstract bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory, out string failureMessage);

			public abstract IRelativePath GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory);

			public bool OnSameVolumeThan(IAbsolutePath otherAbsolutePath)
			{
				Argument.IsNotNull(nameof(otherAbsolutePath), otherAbsolutePath);

				if (Type != otherAbsolutePath.Type)
				{
					return false;
				}

				if (Type == AbsolutePathType.DriveLetter)
				{
					return DriveLetter.Equals(otherAbsolutePath.DriveLetter);
				}
				
				return string.Compare(UNCServer, otherAbsolutePath.UNCServer, StringComparison.OrdinalIgnoreCase) == 0 &&
					   string.Compare(UNCShare, otherAbsolutePath.UNCShare, StringComparison.OrdinalIgnoreCase) == 0;
			}
		}
	}
}
