using System;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private static class MiscHelpers
		{
			internal const char EnvironmentVariablePercent = '%';
			internal static readonly char DirectorySeparatorChar = System.IO.Path.DirectorySeparatorChar;
			internal static readonly string DirectorySeparator = System.IO.Path.DirectorySeparatorChar.ToString();
			internal static readonly string DoubleDirectorySeparator = $"{System.IO.Path.DirectorySeparatorChar}{System.IO.Path.DirectorySeparatorChar}";

			private static readonly string[] UrlSchemes =
			{
				"ftp",
				"http",
				"gopher",
				"mailto",
				"news",
				"nntp",
				"telnet",
				"wais",
				"file",
				"prospero"
			};

			internal static string GetLastName(string path)
			{
				Argument.IsNotNull(nameof(path), path);

				if (!HasParentDirectory(path))
				{
					// In case of directories like "." or "C:" return an empty string.
					return string.Empty;
				}

				var index = path.LastIndexOf(DirectorySeparatorChar);

				//Debug.Assert(index != path.Length - 1);

				return path.Substring(index + 1, path.Length - index - 1);
			}

			internal static string GetParentDirectory(string path)
			{
				Argument.IsNotNull(nameof(path), path);

				if (!HasParentDirectory(path))
				{
					throw new InvalidOperationException(@"Can't get the parent dir from the pathString """ + path + @"""");
				}

				var index = path.LastIndexOf(DirectorySeparatorChar);

				//Debug.Assert(index >= 0);

				return path.Substring(0, index);
			}

			internal static bool HasParentDirectory(string path)
			{
				Argument.IsNotNull(nameof(path), path);

				return path.Contains(DirectorySeparator);
			}

			internal static bool IsAnEnvVarPath(string normalizedPath)
			{
				Argument.IsNotNull(nameof(normalizedPath), normalizedPath);
				//Debug.Assert(normalizedPath.IsNormalized());

				// Minimum URN pathString.Length is 3 ! "%A%"
				if (normalizedPath.Length < 3)
				{
					return false;
				}

				if (normalizedPath[0] != EnvironmentVariablePercent)
				{
					return false;
				}

				// Must find a closing percent
				var closingPercentIndex = normalizedPath.IndexOf(EnvironmentVariablePercent, 1);

				if (closingPercentIndex == -1)
				{
					return false;
				}

				if (closingPercentIndex == 1)
				{
					return false;
				} // Must have some char between the two percent!

				// After closing percent it is the end of string, or we have a DIR_SEPARATOR
				if (closingPercentIndex != normalizedPath.Length - 1)
				{
					if (normalizedPath[closingPercentIndex + 1] != DirectorySeparatorChar)
					{
						return false;
					}
				}

				// Allowed char for environment variable name: Letter (upper/lower) / Number / Underscore
				for (var i = 1; i < closingPercentIndex; i++)
				{
					var c = normalizedPath[i];

					if (IsCharLetterOrDigitOrUnderscore(c))
					{
						continue;
					}

					return false;
				}

				return true;
			}

			internal static bool IsCharLetterOrDigitOrUnderscore(char c)
			{
				return char.IsLetterOrDigit(c) || c == '_';
			}

			internal static bool IsUrlPath(string path)
			{
				Argument.IsNotNull(nameof(path), path);

				var count = UrlSchemes.Length;

				for (var i = 0; i < count; i++)
				{
					var scheme = UrlSchemes[i];
					var schemeLength = scheme.Length;

					if (path.Length > schemeLength &&
						string.Compare(path.Substring(0, schemeLength), scheme, StringComparison.OrdinalIgnoreCase) == 0)
					{
						return true;
					}
				}

				return false;
			}

			internal static string NormalizePath(string path)
			{
				Argument.IsNotNullOrEmpty(nameof(path), path);

				path = path.Replace('/', DirectorySeparatorChar);
				path = path.TrimEnd(); // Remove extra spaces on the right (not on the left!)

				// EventuallyRemoveConsecutiveDirSeparator() ..
				if (path.IndexOf(DoubleDirectorySeparator, StringComparison.Ordinal) == 0)
				{
					// ... except if it is at the beginning of the pathStringNormalized, in which case it might be a URN pathStringNormalized!
					// Subtility, if we begin with 3 or more dir separator, need to keep only two.
					// Hence we eliminate just one dir separator before applying EventuallyRemoveConsecutiveSeparator().
					var pathTmp = path.Substring(1, path.Length - 1);

					pathTmp = EventuallyRemoveConsecutiveSeparator(pathTmp);
					path = DirectorySeparator + pathTmp;
				}
				else
				{
					path = EventuallyRemoveConsecutiveSeparator(path);
				}

				// Eventually Transform ".\.." prefix into ".."
				const string prefixToSimplify = @".\..";

				if (path.IndexOf(prefixToSimplify, StringComparison.Ordinal) == 0)
				{
					path = path.Remove(0, prefixToSimplify.Length);
					path = path.Insert(0, AbsoluteRelativePathHelpers.ParentDirectorySeparator);
				}

				// EventuallyRemoveEndingDirSeparator
				while (true)
				{
					var pathLength = path.Length;

					if (pathLength == 0)
					{
						return string.Empty;
					}

					var pathLengthMinusOne = pathLength - 1;
					var lastChar = path[pathLengthMinusOne];

					if (lastChar != DirectorySeparatorChar)
					{
						break;
					}

					path = path.Substring(0, pathLengthMinusOne);
				}

				return path;
			}

			private static string EventuallyRemoveConsecutiveSeparator(string path)
			{
				Argument.IsNotNull(nameof(path), path);

				while (path.IndexOf(DoubleDirectorySeparator, StringComparison.Ordinal) != -1)
				{
					path = path.Replace(DoubleDirectorySeparator, DirectorySeparator);
				}

				return path;
			}
		}
	}
}
