using System;
using System.Collections.Generic;
using System.Text;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private static class AbsoluteRelativePathHelpers
		{
			internal const string CurrentDirectorySeparator = ".";
			internal const string ParentDirectorySeparator = "..";

			internal static bool ContainsInnerSpecialDirectory(string path)
			{
				Argument.IsNotNullOrEmpty(nameof(path), path);

				//Debug.Assert(path == MiscHelpers.NormalizePath(path));

				var pathDirs = path.Split(MiscHelpers.DirectorySeparatorChar);
				var nextDoubleDotParentDirIsInnerSpecial = false;
				var nextSingleDotCurrentDirIsInnerSpecial = false;
				var count = pathDirs.Length;

				for (var i = 0; i < count; i++)
				{
					var pathDir = pathDirs[i];

					if (pathDir == CurrentDirectorySeparator)
					{
						if (nextSingleDotCurrentDirIsInnerSpecial)
						{
							return true;
						}
					}
					else if (pathDir == ParentDirectorySeparator)
					{
						if (nextDoubleDotParentDirIsInnerSpecial)
						{
							return true;
						}
					}
					else
					{
						nextDoubleDotParentDirIsInnerSpecial = true;
					}

					nextSingleDotCurrentDirIsInnerSpecial = true;
				}

				return false;
			}

			internal static bool IsAnAbsoluteDriveLetterPath(string normalizedPath)
			{
				Argument.IsNotNullOrEmpty(nameof(normalizedPath), normalizedPath);
				//Debug.Assert(normalizedPath.IsNormalized());

				if (normalizedPath.Length == 1)
				{
					return false;
				}

				if (!char.IsLetter(normalizedPath[0]))
				{
					return false;
				}

				if (normalizedPath[1] != ':')
				{
					return false;
				}

				return normalizedPath.Length < 3
					   || normalizedPath[2] == '\\';
			}

			internal static bool IsARelativePath(string normalizedPath)
			{
				Argument.IsNotNullOrEmpty(nameof(normalizedPath), normalizedPath);
				//Debug.Assert(normalizedPath.IsNormalized());

				// First char must be a dot
				if (normalizedPath[0] != '.')
				{
					return false;
				}

				if (normalizedPath.Length == 1)
				{
					return true;
				}

				// Second char must be a dot or a separator!
				var secondChar = normalizedPath[1];

				if (secondChar == MiscHelpers.DirectorySeparatorChar)
				{
					return true;
				}

				if (secondChar != '.')
				{
					return false;
				}

				if (normalizedPath.Length == 2)
				{
					return true;
				}

				// Third char must be a separator!
				var thirdChar = normalizedPath[2];

				return thirdChar == MiscHelpers.DirectorySeparatorChar;
			}

			internal static string NormalizeAndResolveInnerSpecialDir(string path)
			{
				Argument.IsNotNullOrEmpty(nameof(path), path);

				var pathStringNormalized = MiscHelpers.NormalizePath(path);

				if (!ContainsInnerSpecialDirectory(pathStringNormalized))
				{
					return pathStringNormalized;
				}

				string pathStringNormalizedResolved, failureReasonUnused;

				TryResolveInnerSpecialDirectory(pathStringNormalized, out pathStringNormalizedResolved, out failureReasonUnused);

				return pathStringNormalizedResolved;
			}

			internal static bool TryGetAbsolutePathFrom(IAbsoluteDirectoryPath pathFrom, IPath pathTo, out string absolutePath, out string failureMessage)
			{
				//Argument.IsNotNull(nameof(pathFrom), pathFrom);
				//Argument.IsNotNull(nameof(pathTo), pathTo);
				//Debug.Assert(pathTo.IsRelativePath);

				if (pathFrom.Type == AbsolutePathType.DriveLetter)
				{
					// Only work with Directory 
					if (pathTo.IsFilePath)
					{
						pathTo = pathTo.ParentDirectoryPath;
					}

					return TryGetAbsolutePath(pathFrom.ToString(), pathTo.ToString(), out absolutePath, out failureMessage);
				}

				//Debug.Assert(pathFrom.Kind == AbsolutePathKind.UNC);

				string uncServerShareStart;
				string pathResultDriveLetter;

				var pathFromString = pathFrom.ToString();
				var fakePathFromString = UncPathHelper.ConvertUncToDriveLetter(pathFromString, out uncServerShareStart);
				var fakePathFrom = fakePathFromString.ToAbsoluteDirectoryPath();

				//Debug.Assert(fakePathFromString.IsValidAbsoluteDirectoryPath());

				if (!TryGetAbsolutePathFrom(fakePathFrom, pathTo, out pathResultDriveLetter, out failureMessage))
				{
					failureMessage = failureMessage.Replace(UncPathHelper.FakeDriveLetterPrefix, uncServerShareStart);
					absolutePath = null;

					return false;
				}

				//Debug.Assert(pathResultDriveLetter != null);
				//Debug.Assert(pathResultDriveLetter.Length > 0);
				//Debug.Assert(pathResultDriveLetter.StartsWith(UNCPathHelper.FAKE_DRIVE_LETTER_PREFIX));

				absolutePath = UncPathHelper.ConvertDriveLetterToUnc(pathResultDriveLetter, uncServerShareStart);

				return true;
			}

			internal static bool TryGetRelativePath(IAbsoluteDirectoryPath pathFrom, IAbsolutePath pathTo, out string relativePath, out string failureMessage)
			{
				//Argument.IsNotNull(nameof(pathFrom), pathFrom);
				//Argument.IsNotNull(nameof(pathTo), pathTo);

				if (!pathFrom.OnSameVolumeThan(pathTo))
				{
					failureMessage = @"Cannot compute relative path from 2 paths that are not on the same volume 
   PathFrom = """ + pathFrom + @"""
   PathTo   = """ + pathTo + @"""";

					relativePath = null;

					return false;
				}

				// Only work with Directory 
				if (pathTo.IsFilePath)
				{
					pathTo = pathTo.ParentDirectoryPath;
				}

				relativePath = GetPathRelativeTo(pathFrom.ToString(), pathTo.ToString());
				failureMessage = null;

				return true;
			}

			internal static bool TryResolveInnerSpecialDirectory(string normalizedPath, out string resolvedPath, out string failureMessage)
			{
				//Argument.IsNotNullOrEmpty(nameof(normalizedPath), normalizedPath);

				//Debug.Assert(normalizedPath.IsNormalized());
				//Debug.Assert(ContainsInnerSpecialDirectory(normalizedPath));

				var isPathRelative = IsARelativePath(normalizedPath);
				var originalPathNormalized = normalizedPath;
				var isUNCPath = UncPathHelper.StartLikeUncPath(normalizedPath);
				string serverShareStart = null;

				if (isUNCPath)
				{
					// We can assert that coz already checked in IsValidAbsoluteDirectory()
					//Debug.Assert(UNCPathHelper.IsAnAbsoluteUNCPath(normalizedPath));

					normalizedPath = UncPathHelper.ConvertUncToDriveLetter(normalizedPath, out serverShareStart);
				}

				var pathDirectories = normalizedPath.Split(MiscHelpers.DirectorySeparatorChar);
				var rootDirectory = pathDirectories[0];

				//Debug.Assert(pathDirs.Length > 0);

				if (isUNCPath)
				{
					rootDirectory = serverShareStart;
				}

				var result = TryResolveInnerSpecialDirectory(pathDirectories, isPathRelative, out resolvedPath);

				if (!result)
				{
					failureMessage =
						$@"The pathString {{{originalPathNormalized}}} references the parent dir \..\ of the root dir {{{rootDirectory}}}, it cannot be resolved.";

					return false;
				}

				//Debug.Assert(resolvedPath != null);
				//Debug.Assert(resolvedPath.IsNormalized());

				if (isUNCPath)
				{
					resolvedPath = UncPathHelper.ConvertDriveLetterToUnc(resolvedPath, serverShareStart);
				}

				failureMessage = null;

				return true;
			}

			private static string GetPathRelativeTo(string pathFrom, string pathTo)
			{
				//Argument.IsNotNull(nameof(pathFrom), pathFrom);
				//Argument.IsNotNull(nameof(pathTo), pathTo);

				//Debug.Assert(IsAnAbsolutePath(pathFrom));
				//Debug.Assert(IsAnAbsolutePath(pathTo));

				// Don't return .\ but just . to remain compliant
				if (string.Compare(pathFrom, pathTo, StringComparison.OrdinalIgnoreCase) == 0)
				{
					return CurrentDirectorySeparator;
				}

				var relativeDirs = new List<string>();
				var pathFromDirs = pathFrom.Split(MiscHelpers.DirectorySeparatorChar);
				var pathToDirs = pathTo.Split(MiscHelpers.DirectorySeparatorChar);
				var length = Math.Min(pathFromDirs.Length, pathToDirs.Length);
				var lastCommonRoot = -1;

				// find common root
				for (var i = 0; i < length; i++)
				{
					if (string.Compare(pathFromDirs[i], pathToDirs[i], StringComparison.OrdinalIgnoreCase) != 0)
					{
						break;
					}

					lastCommonRoot = i;
				}

				// The lastCommon root problem is handled by the calling method and cannot be tested
				//Debug.Assert(lastCommonRoot != -1);

				// add relative folders in from pathStringNormalized
				for (var i = lastCommonRoot + 1; i < pathFromDirs.Length; i++)
				{
					if (pathFromDirs[i].Length > 0)
					{
						relativeDirs.Add(ParentDirectorySeparator);
					}
				}

				if (relativeDirs.Count == 0)
				{
					relativeDirs.Add(CurrentDirectorySeparator);
				}

				// add to folders to pathStringNormalized
				for (var i = lastCommonRoot + 1; i < pathToDirs.Length; i++)
				{
					relativeDirs.Add(pathToDirs[i]);
				}

				// create relative pathStringNormalized
				var relativeParts = new string[relativeDirs.Count];

				relativeDirs.CopyTo(relativeParts);

				var relativePath = string.Join(MiscHelpers.DirectorySeparator, relativeParts);

				return relativePath;
			}

			private static bool IsAnAbsolutePath(string normalizedPath)
			{
				//Debug.Assert(normalizedPath != null);
				//Debug.Assert(normalizedPath.IsNormalized());

				return IsAnAbsoluteDriveLetterPath(normalizedPath) || UncPathHelper.IsAnAbsoluteUncPath(normalizedPath);
			}

			private static bool TryGetAbsolutePath(string pathFrom, string pathTo, out string absolutePath, out string failureMessage)
			{
				//Debug.Assert(pathFrom != null);
				//Debug.Assert(pathFrom.IsNormalized());
				//Debug.Assert(IsAnAbsolutePath(pathFrom));
				//Debug.Assert(pathTo != null);
				//Debug.Assert(pathTo.IsNormalized());
				//Debug.Assert(IsARelativePath(pathTo));

				var pathFromDirectories = pathFrom.Split(MiscHelpers.DirectorySeparatorChar);
				var pathToDirectories = pathTo.Split(MiscHelpers.DirectorySeparatorChar);
				var parentDirToGoBackInPathFrom = 0;
				var specialDirToGoUpInPathTo = 0;

				for (var i = 0; i < pathToDirectories.Length; i++)
				{
					if (pathToDirectories[i] == ParentDirectorySeparator)
					{
						parentDirToGoBackInPathFrom++;
						specialDirToGoUpInPathTo++;
					}
					else if (pathToDirectories[i] == CurrentDirectorySeparator)
					{
						specialDirToGoUpInPathTo++;
					}
					else
					{
						break;
					}
				}

				// check nbParentDirToGoBackInPathFrom is valid
				if (parentDirToGoBackInPathFrom >= pathFromDirectories.Length)
				{
					failureMessage = @"Cannot resolve pathTo.TryGetAbsolutePath(pathFrom) because there are too many parent dirs in pathTo:
   PathFrom = """ + pathFrom + @"""
   PathTo   = """ + pathTo + @"""";

					absolutePath = null;

					return false;
				}

				// Apply nbParentDirToGoBackInPathFrom to extract part from pathFrom
				var dirsExtractedFromPathFrom = new string[(pathFromDirectories.Length - parentDirToGoBackInPathFrom)];

				for (var i = 0; i < pathFromDirectories.Length - parentDirToGoBackInPathFrom; i++)
				{
					dirsExtractedFromPathFrom[i] = pathFromDirectories[i];
				}

				var partExtractedFromPathFrom = string.Join(MiscHelpers.DirectorySeparator, dirsExtractedFromPathFrom);
				var dirsExtractedFromPathTo = new string[(pathToDirectories.Length - specialDirToGoUpInPathTo)];

				for (var i = 0; i < pathToDirectories.Length - specialDirToGoUpInPathTo; i++)
				{
					dirsExtractedFromPathTo[i] = pathToDirectories[i + specialDirToGoUpInPathTo];
				}

				var partExtractedFromPathTo = string.Join(MiscHelpers.DirectorySeparator, dirsExtractedFromPathTo);

				// Concatenate the 2 parts extracted from pathFrom and pathTo
				absolutePath = partExtractedFromPathFrom + MiscHelpers.DirectorySeparator + partExtractedFromPathTo;
				failureMessage = null;

				return true;
			}

			private static bool TryResolveInnerSpecialDirectory(string[] pathDirectories, bool isPathRelative, out string resolvedPath)
			{
				//Argument.IsNotNull(nameof(pathDirectories), pathDirectories);

				var nbPathDirs = pathDirectories.Length;

				//Debug.Assert(nbPathDirs > 0);

				var directoryStack = new Stack<string>();
				var normalDirHasAlreadyBeenFound = false;

				for (var i = 0; i < nbPathDirs; i++)
				{
					var dir = pathDirectories[i];

					//Debug.Assert(dir != null);
					//Debug.Assert(dir.Length > 0);

					switch (dir)
					{
						case CurrentDirectorySeparator:
							if (i > 0)
							{
								// Just ignore InnerSpecial SingleDot, except the first one!
								continue;
							}

							//Debug.Assert(isPathRelative); // "." is pushed only in bPathIsRelative case!

							directoryStack.Push(dir);

							break;
						case ParentDirectorySeparator:
							if (!normalDirHasAlreadyBeenFound)
							{
								//Debug.Assert(isPathRelative); // ".." is pushed only in bPathIsRelative case!

								directoryStack.Push(dir);

								continue;
							}

							// We can assert this coz bNextDoubleDotParentDirIsInnerSpecial is true + next test!
							//Debug.Assert(dirStack.Count > 0);

							if (!isPathRelative && directoryStack.Count == 1)
							{
								// Here we reached a problem coz we are trying to get parent dir, of first dir (that can be "C:\"  "%EnvVar%"   "$(SolutionDir)"  )
								resolvedPath = null;

								return false;
							}

							var directoryToRemove = directoryStack.Peek();

							switch (directoryToRemove)
							{
								case CurrentDirectorySeparator:
									//Debug.Assert(isPathRelative); // "." is pushed only in bPathIsRelative case...
									//Debug.Assert(dirStack.Count == 1); // ... when it is at the beginning of the path!

									directoryStack.Pop();
									directoryStack.Push(ParentDirectorySeparator);

									continue;
								case ParentDirectorySeparator:
									//Debug.Assert(isPathRelative); // ".." is pushed only in bPathIsRelative case!

									// No prob here, we just push the "..\" since the result can be a relative path!
									directoryStack.Push(ParentDirectorySeparator);

									continue;
								default:
									// dirToRemove is a normal named dir, it is not ".\" nor "..\"
									// just pop it!
									directoryStack.Pop();

									continue;
							}
						default:
							// dir is a normal named dir, it is not ".\" nor "..\"
							directoryStack.Push(dir);

							normalDirHasAlreadyBeenFound = true;

							continue;
					}
				}

				// Concatenate the dirs
				var stringBuilder = new StringBuilder();

				// Notice that the dirs are reverse ordered, that's why we use Insert(0,
				foreach (var dir in directoryStack)
				{
					stringBuilder.Insert(0, MiscHelpers.DirectorySeparator);
					stringBuilder.Insert(0, dir);
				}

				// Remove the last DIR_SEPARATOR
				stringBuilder.Length = stringBuilder.Length - 1;
				resolvedPath = stringBuilder.ToString();

				return true;
			}

			/*private enum TryResolveInnerSpecialDirectoryResult
			{
				Success,
				ParentOfRootDirectoryResolved
			}*/
		}
	}
}
