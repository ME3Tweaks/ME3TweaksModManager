using System;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private static class UncPathHelper
		{
			internal const string FakeDriveLetterPrefix = @"C:";

			internal static bool IsAnAbsoluteUncPath(string normalizedPath)
			{
				Argument.IsNotNullOrEmpty(nameof(normalizedPath), normalizedPath);

				//Debug.Assert(normalizedPath.IsNormalized());

				int endIndexServerShareUnused;

				return IsAnAbsoluteUncPath(normalizedPath, out endIndexServerShareUnused);
			}

			internal static bool IsAnAbsoluteUncPath(string normalizedPath, out int lastIndex)
			{
				Argument.IsNotNullOrEmpty(nameof(normalizedPath), normalizedPath);

				//Debug.Assert(normalizedPath.IsNormalized());

				lastIndex = -1;

				// Minimum URN pathString.Length is 5 ! "\\m\s"
				if (normalizedPath.Length < 5)
				{
					return false;
				}

				// Must begin with "\\"
				if (normalizedPath.IndexOf(MiscHelpers.DoubleDirectorySeparator, StringComparison.Ordinal) != 0)
				{
					return false;
				}

				// Must have a separator after the double separator with at least a char in the middle
				// means must have a separator after index 3
				var twoDirSeparatorLength = MiscHelpers.DoubleDirectorySeparator.Length;
				var indexFirstSeparator = normalizedPath.IndexOf(MiscHelpers.DirectorySeparator, twoDirSeparatorLength, StringComparison.Ordinal);

				if (indexFirstSeparator == -1)
				{
					return false;
				}

				//Debug.Assert(indexFirstSeparator > twoDirSeparatorLength); // coz path has been normalized

				// Server cannot be "." nor ".."
				var server = normalizedPath.Substring(twoDirSeparatorLength, indexFirstSeparator - twoDirSeparatorLength);

				//Debug.Assert(server.Length > 0);

				if (server == AbsoluteRelativePathHelpers.CurrentDirectorySeparator
					|| server == AbsoluteRelativePathHelpers.ParentDirectorySeparator)
				{
					return false;
				}

				// Share cannot be "." nor ".."
				var indexSecondSeparator = normalizedPath.IndexOf(MiscHelpers.DirectorySeparator, indexFirstSeparator + 1, StringComparison.Ordinal);

				if (indexSecondSeparator == -1)
				{
					indexSecondSeparator = normalizedPath.Length;
				}

				var indexStartShare = indexFirstSeparator + 1;
				var share = normalizedPath.Substring(indexStartShare, indexSecondSeparator - indexStartShare);

				//Debug.Assert(indexSecondSeparator > indexStartShare); // Coz path has been normalized!

				//Debug.Assert(share.Length > 0);

				if (share == AbsoluteRelativePathHelpers.CurrentDirectorySeparator
					|| share == AbsoluteRelativePathHelpers.ParentDirectorySeparator)
				{
					return false;
				}

				lastIndex = indexSecondSeparator;

				return true;
			}

			internal static bool StartLikeUncPath(string normalizedPath)
			{
				Argument.IsNotNull(nameof(normalizedPath), normalizedPath);

				return normalizedPath.IndexOf(MiscHelpers.DoubleDirectorySeparator, StringComparison.Ordinal) == 0;
			}

			internal static string ConvertDriveLetterToUnc(string normalizedPath, string serverShareStart)
			{
				Argument.IsNotNull(nameof(normalizedPath), normalizedPath);
				Argument.IsNotNullOrEmpty(nameof(serverShareStart), serverShareStart);

				//Debug.Assert(driveLetterNormalizedPath.IsNormalized());
				//Debug.Assert(driveLetterNormalizedPath.StartsWith(FakeDriveLetterPrefix));

				return normalizedPath.Remove(0, FakeDriveLetterPrefix.Length).Insert(0, serverShareStart);
			}

			internal static string ConvertUncToDriveLetter(string normalizedPath, out string serverShareStart)
			{
				Argument.IsNotNull(nameof(normalizedPath), normalizedPath);

				//Debug.Assert(normalizedPath.IsNormalized());

				int lastIndex;

				IsAnAbsoluteUncPath(normalizedPath, out lastIndex);

				serverShareStart = normalizedPath.Substring(0, lastIndex);

				return normalizedPath.Remove(0, lastIndex).Insert(0, FakeDriveLetterPrefix);
			}
		}
	}
}
