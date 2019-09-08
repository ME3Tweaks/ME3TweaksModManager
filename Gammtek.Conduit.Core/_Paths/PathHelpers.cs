using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Gammtek.Conduit.Extensions.Linq;

namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Contains extensions methods to obtain a path object from a string and to check that a string indeed represents a valid path.
	/// </summary>
	public static partial class PathHelpers
	{
		/// <summary>
		///     An array of char forbidden in string representing path.
		/// </summary>
		/// <remarks>
		///     Use this string.IndexOfAny(char[]) method to detect the presence of any of this char in a string.
		/// </remarks>
		public static char[] ForbiddenCharInPath { get; } = { '*', '|', '?', '<', '>', '"' };

		/// <summary>
		///     Path variables are formatted this way $(VariableName). Hence this getter returns the string "$(".
		/// </summary>
		public static string PathVariableBegin => @"$(";

		/// <summary>
		///     Path variables are formatted this way $(VariableName). Hence this getter returns the string ")".
		/// </summary>
		public static string PathVariableEnd => @")";

		/// <summary>
		///     Returns <i>true</i> if <paramref name="path" /> and <paramref name="otherPath" /> are both <i>null</i>, or if <paramref name="path" />.Equals(
		///     <paramref name="otherPath" />).
		/// </summary>
		/// <param name="path">The first path.</param>
		/// <param name="otherPath">The scond path.</param>
		public static bool EqualsNullSupported(this IPath path, IPath otherPath)
		{
			if (path == null)
			{
				return otherPath == null;
			}

			return otherPath != null && path.Equals(otherPath);
		}

		/// <summary>
		///     Returns <i>true</i> if <paramref name="path" /> is not null, and <paramref name="path" />.<see cref="IAbsolutePath.Exists" /> equals <i>true</i>.
		/// </summary>
		/// <param name="path">The path reference.</param>
		public static bool IsNotNullAndExists(this IAbsolutePath path)
		{
			return path != null && path.Exists;
		}

		/// <summary>
		///     Determine whether this string is a valid absolute directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToAbsoluteDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IAbsoluteDirectoryPath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid absolute path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidAbsoluteDirectoryPath(this string path)
		{
			string failureMessage;

			return IsValidAbsoluteDirectoryPath(path, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid absolute directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToAbsoluteDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IAbsoluteDirectoryPath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid absolute path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidAbsoluteDirectoryPath(this string path, out string failureMessage)
		{
			string normalizedPath;

			if (!path.TryGetNotNullNormalizedPath(out normalizedPath, out failureMessage))
			{
				return false;
			}

			if (MiscHelpers.IsUrlPath(normalizedPath))
			{
				failureMessage = @"URL paths are not accepted as absolute paths.";

				return false;
			}

			if (!AbsoluteRelativePathHelpers.IsAnAbsoluteDriveLetterPath(normalizedPath))
			{
				if (!UncPathHelper.IsAnAbsoluteUncPath(normalizedPath))
				{
					failureMessage =
						$@"The argument {nameof(path)} is not an absolute directory path because it doesn't have a drive letter syntax (like ""C:\"") nor a URN path syntax (like ""\\server\share\"").";

					return false;
				}
			}

			if (AbsoluteRelativePathHelpers.ContainsInnerSpecialDirectory(normalizedPath))
			{
				string resolvedPath;

				if (!AbsoluteRelativePathHelpers.TryResolveInnerSpecialDirectory(normalizedPath, out resolvedPath, out failureMessage))
				{
					return false;
				}
			}

			failureMessage = null;

			return true;
		}

		/// <summary>
		///     Determine whether this string is a valid file absolute path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToAbsoluteFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IAbsoluteFilePath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid absolute file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidAbsoluteFilePath(this string path)
		{
			string failureMessage;

			return IsValidAbsoluteFilePath(path, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid file absolute path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToAbsoluteFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IAbsoluteFilePath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid absolute file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidAbsoluteFilePath(this string path, out string failureMessage)
		{
			if (!path.IsValidAbsoluteDirectoryPath(out failureMessage))
			{
				return false;
			}

			var normalizedPath = MiscHelpers.NormalizePath(path);
			var isUNCPath = UncPathHelper.StartLikeUncPath(normalizedPath);

			if (isUNCPath)
			{
				// We can assret that coz we already validated that IsValidAbsoluteDirectoryPath
				//Debug.Assert(UNCPathHelper.IsAnAbsoluteUNCPath(normalizedPath));

				string serverShareStart;

				normalizedPath = UncPathHelper.ConvertUncToDriveLetter(normalizedPath, out serverShareStart);
			}

			string fileName;

			return IsThisValidDirectoryPathAValidFilePath(normalizedPath, out fileName, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IDirectoryPath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative or absolute directory path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidDirectoryPath(this string path)
		{
			string failureMessage;

			return IsValidDirectoryPath(path, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IDirectoryPath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative or absolute directory path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidDirectoryPath(this string path, out string failureMessage)
		{
			string normalizedPath;

			if (!path.TryGetNotNullNormalizedPath(out normalizedPath, out failureMessage))
			{
				return false;
			}

			if (normalizedPath.IsValidRelativeDirectoryPath()
				|| normalizedPath.IsValidAbsoluteDirectoryPath()
				|| normalizedPath.IsValidEnvVarDirectoryPath()
				|| normalizedPath.IsValidVariableDirectoryPath())
			{
				return true;
			}

			failureMessage = $@"The string ""{path}"" is not a valid directory path.";

			return false;
		}

		/// <summary>
		///     Determine whether this string is a valid directory path prefixed with an environment variable or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToEnvVarDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IEnvironmentVariableDirectoryPath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid path prefixed with an environment variable, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidEnvVarDirectoryPath(this string path)
		{
			string failureMessage;

			return IsValidEnvVarDirectoryPath(path, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid directory path prefixed with an environment variable or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToEnvVarDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IEnvironmentVariableDirectoryPath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid path prefixed with an environment variable, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidEnvVarDirectoryPath(this string path, out string failureMessage)
		{
			string normalizedPath;

			if (!path.TryGetNotNullNormalizedPath(out normalizedPath, out failureMessage))
			{
				return false;
			}

			if (!MiscHelpers.IsAnEnvVarPath(normalizedPath))
			{
				failureMessage = $@"The argument {nameof(path)} is not prefixed with an environment variable (like ""%USERPROFILE%"")";

				return false;
			}

			if (AbsoluteRelativePathHelpers.ContainsInnerSpecialDirectory(normalizedPath))
			{
				string resolvedPath;

				if (!AbsoluteRelativePathHelpers.TryResolveInnerSpecialDirectory(normalizedPath, out resolvedPath, out failureMessage))
				{
					return false;
				}
			}

			failureMessage = null;

			return true;
		}

		/// <summary>
		///     Determine whether this string is a valid file path prefixed with an environment variable or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToEnvVarFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IEnvironmentVariableFilePath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid file path prefixed with an environment variable, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidEnvVarFilePath(this string path)
		{
			string failureMessage;

			return IsValidEnvVarFilePath(path, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid file path prefixed with an environment variable or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToEnvVarFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IEnvironmentVariableFilePath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid file path prefixed with an environment variable, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidEnvVarFilePath(this string path, out string failureMessage)
		{
			if (!path.IsValidEnvVarDirectoryPath(out failureMessage))
			{
				return false;
			}

			string fileName;

			return IsThisValidDirectoryPathAValidFilePath(path, out fileName, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid file path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IFilePath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative or absolute file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidFilePath(this string path)
		{
			string failureMessage;

			return IsValidFilePath(path, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid file path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IFilePath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative or absolute file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidFilePath(this string path, out string failureMessage)
		{
			string pathStringNormalized;

			if (!path.TryGetNotNullNormalizedPath(out pathStringNormalized, out failureMessage))
			{
				return false;
			}

			if (pathStringNormalized.IsValidRelativeFilePath()
				|| pathStringNormalized.IsValidAbsoluteFilePath()
				|| pathStringNormalized.IsValidEnvVarFilePath()
				|| pathStringNormalized.IsValidVariableFilePath())
			{
				return true;
			}

			failureMessage = $@"The string ""{path}"" is not a valid file path.";

			return false;
		}

		/// <summary>
		///     Returns <i>true</i> if <paramref name="variableName" /> contains only upper/lower case letters, digits and underscore and has less than 1024
		///     characters. In such case <paramref name="variableName" /> is a valid path variable name.
		/// </summary>
		/// <param name="variableName">The string on which we test if it is a valid path variable name.</param>
		[Pure]
		public static bool IsValidPathVariableName(this string variableName)
		{
			Argument.IsNotNullOrEmpty(nameof(variableName), variableName);

			const int maxChars = 1024;
			var length = variableName.Length;

			if (length > maxChars)
			{
				return false;
			}

			for (var i = 0; i < length; i++)
			{
				var c = variableName[i];

				if (MiscHelpers.IsCharLetterOrDigitOrUnderscore(c))
				{
					continue;
				}

				return false;
			}

			return true;
		}

		/// <summary>
		///     Returns <i>true</i> if <paramref name="variableValue" /> has less than 1024 characters and has no character in
		///     <see cref="ForbiddenCharInPath" />. In such case <paramref name="variableValue" /> is a valid path variable name.
		/// </summary>
		/// <param name="variableValue">The string on which we test if it is a valid path variable value.</param>
		[Pure]
		public static bool IsValidPathVariableValue(this string variableValue)
		{
			Argument.IsNotNull(nameof(variableValue), variableValue);

			const int maxChars = 1024;
			var length = variableValue.Length;

			if (length > maxChars)
			{
				return false;
			}

			return variableValue.IndexOfAny(ForbiddenCharInPath) < 0;
		}

		/// <summary>
		///     Determine whether this string is a valid relative directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToRelativeDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IRelativeDirectoryPath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidRelativeDirectoryPath(this string path)
		{
			string failureMessage;

			return IsValidRelativeDirectoryPath(path, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid relative directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToRelativeDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IRelativeDirectoryPath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidRelativeDirectoryPath(this string path, out string failureMessage)
		{
			string normalizedPath;

			if (!path.TryGetNotNullNormalizedPath(out normalizedPath, out failureMessage))
			{
				return false;
			}

			if (!AbsoluteRelativePathHelpers.IsARelativePath(normalizedPath))
			{
				failureMessage = $"The argument {nameof(path)} is not a valid relative path.";

				return false;
			}

			/*#if DEBUG
			// TryResolveInnerSpecialDir() cannot return false for a relative path, 
			// just assert this in DEBUG mode!
			if (AbsoluteRelativePathHelpers.ContainsInnerSpecialDirectory(normalizedPath))
			{
				string resolvedPath, tempFailureMessage;

				Debug.Assert(AbsoluteRelativePathHelpers.TryResolveInnerSpecialDirectory(normalizedPath, out resolvedPath, out tempFailureMessage));
			}
			#endif*/

			failureMessage = null;

			return true;
		}

		/// <summary>
		///     Determine whether this string is a valid relative file path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToRelativeFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IRelativeFilePath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidRelativeFilePath(this string path)
		{
			string failureMessage;

			return path.IsValidRelativeFilePath(out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid relative file path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true <see cref="ToRelativeFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IRelativeFilePath" />.
		/// </remarks>
		/// <param name="path">this string</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidRelativeFilePath(this string path, out string failureMessage)
		{
			if (!path.IsValidRelativeDirectoryPath(out failureMessage))
			{
				return false;
			}

			string fileName;

			return IsThisValidDirectoryPathAValidFilePath(path, out fileName, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid directory path that contains variables.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToVariableDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IVariableDirectoryPath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid path that contains variables, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidVariableDirectoryPath(this string path)
		{
			string failureMessage;

			return IsValidVariableDirectoryPath(path, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid directory path that contains variables.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToVariableDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IVariableDirectoryPath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid path that contains variables, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidVariableDirectoryPath(this string path, out string failureMessage)
		{
			string normalizedPath;

			if (!path.TryGetNotNullNormalizedPath(out normalizedPath, out failureMessage))
			{
				return false;
			}

			IReadOnlyList<string> variables;

			if (!VariablePathHelpers.IsAVariablePath(normalizedPath, out variables, out failureMessage))
			{
				return false;
			}

			if (AbsoluteRelativePathHelpers.ContainsInnerSpecialDirectory(normalizedPath))
			{
				string resolvedPath;

				if (!AbsoluteRelativePathHelpers.TryResolveInnerSpecialDirectory(normalizedPath, out resolvedPath, out failureMessage))
				{
					return false;
				}
			}

			failureMessage = null;

			return true;
		}

		/// <summary>
		///     Determine whether this string is a valid file that contains variables, or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToVariableFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IVariableFilePath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid file path that contains variables, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidVariableFilePath(this string path)
		{
			string failureMessage;

			return IsValidVariableFilePath(path, out failureMessage);
		}

		/// <summary>
		///     Determine whether this string is a valid file that contains variables, or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToVariableFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IVariableFilePath" />.
		/// </remarks>
		/// <param name="path">This string from which is determined the path validity.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid file path that contains variables, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidVariableFilePath(this string path, out string failureMessage)
		{
			if (!path.IsValidVariableDirectoryPath(out failureMessage))
			{
				return false;
			}

			string fileName;

			if (!IsThisValidDirectoryPathAValidFilePath(path, out fileName, out failureMessage))
			{
				return false;
			}

			if (!VariablePathHelpers.DoesFileNameContainVariable(fileName))
			{
				return true;
			}

			failureMessage = $"The argument {nameof(path)} is not a file path because it doesn't have at least one parent directory.";

			return false;
		}

		/// <summary>
		///     Returns a new <see cref="IAbsoluteDirectoryPath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidAbsoluteDirectoryPath(string)" /> can be called to enfore <paramref name="path" /> validity before calling
		///     this method, and avoid any exception.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="path" /> is empty or doesn't represents a valid absolute directory path.</exception>
		public static IAbsoluteDirectoryPath ToAbsoluteDirectoryPath(this string path)
		{
			//Argument.IsNotNullOrEmpty(nameof(path), path);

			string failureMessage;
			IAbsoluteDirectoryPath directoryPath;

			if (!path.TryGetAbsoluteDirectoryPath(out directoryPath, out failureMessage))
			{
				throw new ArgumentException(failureMessage, nameof(path));
			}

			return directoryPath;
		}

		/// <summary>
		///     Returns a new <see cref="IAbsoluteFilePath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidAbsoluteFilePath(string)" /> can be called to enfore <paramref name="path" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="path" /> is empty or doesn't represents a valid absolute file path.</exception>
		public static IAbsoluteFilePath ToAbsoluteFilePath(this string path)
		{
			//Argument.IsNotNullOrEmpty(nameof(path), path);

			string failureMessage;
			IAbsoluteFilePath filePath;

			if (!path.TryGetAbsoluteFilePath(out filePath, out failureMessage))
			{
				throw new ArgumentException(failureMessage, nameof(path));
			}

			return filePath;
		}

		/// <summary>
		///     Returns a new <see cref="IDirectoryPath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidDirectoryPath(string)" /> can be called to enfore <paramref name="path" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path" /> is empty or doesn't represents a valid relative or absolute directory path or a
		///     valid directory path prefixed with an environment variable.
		/// </exception>
		public static IDirectoryPath ToDirectoryPath(this string path)
		{
			Argument.IsNotNullOrEmpty(nameof(path), path);

			string failureMessage;
			IDirectoryPath directoryPath;

			if (!path.TryGetDirectoryPath(out directoryPath, out failureMessage))
			{
				throw new ArgumentException(failureMessage, nameof(path));
			}

			return directoryPath;
		}

		/// <summary>
		///     Returns a new <see cref="IEnvironmentVariableDirectoryPath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidEnvVarDirectoryPath(string)" /> can be called to enfore <paramref name="path" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path" /> is empty or doesn't represents a valid directory path prefixed with an environment
		///     variable.
		/// </exception>
		public static IEnvironmentVariableDirectoryPath ToEnvVarDirectoryPath(this string path)
		{
			//Argument.IsNotNullOrEmpty(nameof(path), path);

			string failureMessage;
			IEnvironmentVariableDirectoryPath directoryPath;

			if (!path.TryGetEnvVarDirectoryPath(out directoryPath, out failureMessage))
			{
				throw new ArgumentException(failureMessage, nameof(path));
			}

			return directoryPath;
		}

		/// <summary>
		///     Returns a new <see cref="IEnvironmentVariableFilePath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidEnvVarFilePath(string)" /> can be called to enfore <paramref name="path" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path" /> is empty or doesn't represents a valid file path prefixed with an environment
		///     variable.
		/// </exception>
		public static IEnvironmentVariableFilePath ToEnvVarFilePath(this string path)
		{
			//Argument.IsNotNullOrEmpty(nameof(path), path);

			string failureMessage;
			IEnvironmentVariableFilePath filePath;

			if (!path.TryGetEnvVarFilePath(out filePath, out failureMessage))
			{
				throw new ArgumentException(failureMessage, nameof(path));
			}

			return filePath;
		}

		/// <summary>
		///     Returns a new <see cref="IFilePath" /> object object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidFilePath(string)" /> can be called to enfore <paramref name="path" /> validity before calling this method, and
		///     avoid any exception.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path" /> is empty or doesn't represents a valid relative or absolute file path or a valid
		///     file path prefixed with an environment variable.
		/// </exception>
		public static IFilePath ToFilePath(this string path)
		{
			//Argument.IsNotNullOrEmpty(nameof(path), path);

			string failureMessage;
			IFilePath filePath;

			if (!path.TryGetFilePath(out filePath, out failureMessage))
			{
				throw new ArgumentException(failureMessage, nameof(path));
			}

			return filePath;
		}

		/// <summary>
		///     Returns a new <see cref="IRelativeDirectoryPath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidRelativeDirectoryPath(string)" /> can be called to enfore <paramref name="path" /> validity before calling
		///     this method, and avoid any exception.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="path" /> is empty or doesn't represents a valid relative directory path.</exception>
		public static IRelativeDirectoryPath ToRelativeDirectoryPath(this string path)
		{
			//Argument.IsNotNullOrEmpty(nameof(path), path);

			string failureMessage;
			IRelativeDirectoryPath relativePath;

			if (!path.TryGetRelativeDirectoryPath(out relativePath, out failureMessage))
			{
				throw new ArgumentException(failureMessage, nameof(path));
			}

			return relativePath;
		}

		/// <summary>
		///     Returns a new <see cref="IRelativeFilePath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidRelativeFilePath(string)" /> can be called to enfore <paramref name="path" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="path" /> is empty or doesn't represents a valid relative file path.</exception>
		public static IRelativeFilePath ToRelativeFilePath(this string path)
		{
			//Argument.IsNotNullOrEmpty(nameof(path), path);

			string failureMessage;
			IRelativeFilePath relativePath;

			if (!path.TryGetRelativeFilePath(out relativePath, out failureMessage))
			{
				throw new ArgumentException(failureMessage, nameof(path));
			}

			return relativePath;
		}

		/// <summary>
		///     Returns <paramref name="path" />.ToString() is path is null, else returns the empty string.
		/// </summary>
		/// <param name="path">The path reference.</param>
		public static string ToStringOrIfNullToEmptyString(this IPath path)
		{
			return path?.ToString() ?? string.Empty;
		}

		/// <summary>
		///     Returns a new <see cref="IVariableDirectoryPath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidVariableDirectoryPath(string)" /> can be called to enfore <paramref name="path" /> validity before calling
		///     this method, and avoid any exception.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="path" /> is empty or doesn't represents a valid directory path that contains variables.</exception>
		public static IVariableDirectoryPath ToVariableDirectoryPath(this string path)
		{
			//Argument.IsNotNullOrEmpty(nameof(path), path);

			string failureMessage;
			IVariableDirectoryPath directoryPath;

			if (!path.TryGetVariableDirectoryPath(out directoryPath, out failureMessage))
			{
				throw new ArgumentException(failureMessage, nameof(path));
			}

			return directoryPath;
		}

		/// <summary>
		///     Returns a new <see cref="IVariableFilePath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidVariableFilePath(string)" /> can be called to enfore <paramref name="path" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="path" /> is empty or doesn't represents a valid file path that contains variables.</exception>
		public static IVariableFilePath ToVariableFilePath(this string path)
		{
			//Argument.IsNotNullOrEmpty(nameof(path), path);

			string failureMessage;
			IVariableFilePath filePath;

			if (!path.TryGetVariableFilePath(out filePath, out failureMessage))
			{
				throw new ArgumentException(failureMessage, nameof(path));
			}

			return filePath;
		}

		/// <summary>
		///     Try get a new <see cref="IAbsoluteDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid absolute directory path and as a consequence, the returned
		///     <paramref name="absolutePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="absolutePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureMessage">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetAbsoluteDirectoryPath(this string path, out IAbsoluteDirectoryPath absolutePath, out string failureMessage)
		{
			absolutePath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			if (!path.IsValidAbsoluteDirectoryPath(out failureMessage))
			{
				return false;
			}

			absolutePath = new AbsoluteDirectoryPath(path);

			return true;
		}

		/// <summary>
		///     Try get a new <see cref="IAbsoluteDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid absolute directory path and as a consequence, the returned
		///     <paramref name="absolutePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="absolutePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetAbsoluteDirectoryPath(this string path, out IAbsoluteDirectoryPath absolutePath)
		{
			string failureMessage;

			return path.TryGetAbsoluteDirectoryPath(out absolutePath, out failureMessage);
		}

		/// <summary>
		///     Try get a new <see cref="IAbsoluteFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid absolute file path and as a consequence, the returned
		///     <paramref name="absolutePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path string.</param>
		/// <param name="absolutePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureMessage">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetAbsoluteFilePath(this string path, out IAbsoluteFilePath absolutePath, out string failureMessage)
		{
			absolutePath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			if (!path.IsValidAbsoluteFilePath(out failureMessage))
			{
				return false;
			}

			absolutePath = new AbsoluteFilePath(path);

			return true;
		}

		//---------------------------------------------------
		//
		// string to IPath TryGet...Path extension methods, withOUT failureReason
		//
		//---------------------------------------------------

		/// <summary>
		///     Try get a new <see cref="IAbsoluteFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid absolute file path and as a consequence, the returned
		///     <paramref name="absolutePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path string.</param>
		/// <param name="absolutePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetAbsoluteFilePath(this string path, out IAbsoluteFilePath absolutePath)
		{
			string failureMessage;

			return path.TryGetAbsoluteFilePath(out absolutePath, out failureMessage);
		}

		/// <summary>
		///     Try get a new <see cref="IDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid directory path and as a consequence, the returned <paramref name="directoryPath" />
		///     is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="directoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureMessage">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetDirectoryPath(this string path, out IDirectoryPath directoryPath, out string failureMessage)
		{
			directoryPath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			if (path.IsValidAbsoluteDirectoryPath())
			{
				directoryPath = path.ToAbsoluteDirectoryPath();
				return true;
			}

			if (path.IsValidRelativeDirectoryPath())
			{
				directoryPath = path.ToRelativeDirectoryPath();
				return true;
			}

			if (path.IsValidEnvVarDirectoryPath())
			{
				directoryPath = path.ToEnvVarDirectoryPath();
				return true;
			}

			if (path.IsValidVariableDirectoryPath())
			{
				directoryPath = path.ToVariableDirectoryPath();
				return true;
			}

			path.IsValidDirectoryPath(out failureMessage);

			failureMessage = $@"The parameter pathString is not a valid directory path.{Environment.NewLine}{failureMessage}";

			return false;
		}

		/// <summary>
		///     Try get a new <see cref="IDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid directory path and as a consequence, the returned <paramref name="directoryPath" />
		///     is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="directoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetDirectoryPath(this string path, out IDirectoryPath directoryPath)
		{
			string failureMessage;

			return path.TryGetDirectoryPath(out directoryPath, out failureMessage);
		}

		/// <summary>
		///     Try get a new <see cref="IEnvironmentVariableDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid directory path prefixed with an environment variable and as a consequence, the
		///     returned <paramref name="directoryPath" /> is not null.
		/// </returns>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="directoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureMessage">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetEnvVarDirectoryPath(this string path, out IEnvironmentVariableDirectoryPath directoryPath, out string failureMessage)
		{
			directoryPath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			if (!path.IsValidEnvVarDirectoryPath(out failureMessage))
			{
				return false;
			}

			directoryPath = new EnvironmentVariableDirectoryPath(path);

			return true;
		}

		/// <summary>
		///     Try get a new <see cref="IEnvironmentVariableDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid directory path prefixed with an environment variable and as a consequence, the
		///     returned <paramref name="directoryPath" /> is not null.
		/// </returns>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="directoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetEnvVarDirectoryPath(this string path, out IEnvironmentVariableDirectoryPath directoryPath)
		{
			string failureMessage;

			return path.TryGetEnvVarDirectoryPath(out directoryPath, out failureMessage);
		}

		/// <summary>
		///     Try get a new <see cref="IEnvironmentVariableFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid file path prefixed with an environment variable and as a consequence, the returned
		///     <paramref name="filePath" /> is not null.
		/// </returns>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="filePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureMessage">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetEnvVarFilePath(this string path, out IEnvironmentVariableFilePath filePath, out string failureMessage)
		{
			filePath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			if (!path.IsValidEnvVarFilePath(out failureMessage))
			{
				return false;
			}

			filePath = new EnvironmentVariableFilePath(path);

			return true;
		}

		/// <summary>
		///     Try get a new <see cref="IEnvironmentVariableFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid file path prefixed with an environment variable and as a consequence, the returned
		///     <paramref name="filePath" /> is not null.
		/// </returns>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="filePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetEnvVarFilePath(this string path, out IEnvironmentVariableFilePath filePath)
		{
			string failureMessage;

			return path.TryGetEnvVarFilePath(out filePath, out failureMessage);
		}

		/// <summary>
		///     Try get a new <see cref="IFilePath" /> object object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid file path and as a consequence, the returned <paramref name="filePath" /> is not
		///     null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="filePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureMessage">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetFilePath(this string path, out IFilePath filePath, out string failureMessage)
		{
			filePath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			if (path.IsValidAbsoluteFilePath())
			{
				filePath = path.ToAbsoluteFilePath();

				return true;
			}

			if (path.IsValidRelativeFilePath())
			{
				filePath = path.ToRelativeFilePath();

				return true;
			}

			if (path.IsValidEnvVarFilePath())
			{
				filePath = path.ToEnvVarFilePath();

				return true;
			}

			if (path.IsValidVariableFilePath())
			{
				filePath = path.ToVariableFilePath();

				return true;
			}

			path.IsValidFilePath(out failureMessage);

			failureMessage = $@"The parameter pathString is not a valid file path.{Environment.NewLine}{failureMessage}";

			return false;
		}

		/// <summary>
		///     Try get a new <see cref="IFilePath" /> object object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid file path and as a consequence, the returned <paramref name="filePath" /> is not
		///     null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="filePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetFilePath(this string path, out IFilePath filePath)
		{
			string failureMessage;

			return path.TryGetFilePath(out filePath, out failureMessage);
		}

		/// <summary>
		///     Try get a new <see cref="IAbsoluteDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid relative directory path and as a consequence, the returned
		///     <paramref name="relativePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="relativePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureMessage">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetRelativeDirectoryPath(this string path, out IRelativeDirectoryPath relativePath, out string failureMessage)
		{
			relativePath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			if (!path.IsValidRelativeDirectoryPath(out failureMessage))
			{
				return false;
			}

			relativePath = new RelativeDirectoryPath(path);

			return true;
		}

		/// <summary>
		///     Try get a new <see cref="IAbsoluteDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid relative directory path and as a consequence, the returned
		///     <paramref name="relativePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="relativePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetRelativeDirectoryPath(this string path, out IRelativeDirectoryPath relativePath)
		{
			string failureMessage;

			return path.TryGetRelativeDirectoryPath(out relativePath, out failureMessage);
		}

		/// <summary>
		///     Try get a new <see cref="IRelativeFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid relative file path and as a consequence, the returned
		///     <paramref name="relativePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="relativePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureMessage">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetRelativeFilePath(this string path, out IRelativeFilePath relativePath, out string failureMessage)
		{
			relativePath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			if (!path.IsValidRelativeFilePath(out failureMessage))
			{
				return false;
			}

			relativePath = new RelativeFilePath(path);

			return true;
		}

		/// <summary>
		///     Try get a new <see cref="IRelativeFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid relative file path and as a consequence, the returned
		///     <paramref name="relativePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="relativePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetRelativeFilePath(this string path, out IRelativeFilePath relativePath)
		{
			string failureMessage;

			return path.TryGetRelativeFilePath(out relativePath, out failureMessage);
		}

		/// <summary>
		///     Try get a new <see cref="IVariableDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid directory path that contains variables and as a consequence, the returned
		///     <paramref name="directoryPath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="directoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureMessage">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetVariableDirectoryPath(this string path, out IVariableDirectoryPath directoryPath, out string failureMessage)
		{
			directoryPath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			if (!path.IsValidVariableDirectoryPath(out failureMessage))
			{
				return false;
			}

			directoryPath = new VariableDirectoryPath(path);

			return true;
		}

		/// <summary>
		///     Try get a new <see cref="IVariableDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid directory path that contains variables and as a consequence, the returned
		///     <paramref name="directoryPath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="directoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetVariableDirectoryPath(this string path, out IVariableDirectoryPath directoryPath)
		{
			string failureMessage;

			return path.TryGetVariableDirectoryPath(out directoryPath, out failureMessage);
		}

		/// <summary>
		///     Try get a new <see cref="IVariableFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid file path that contains variables and as a consequence, the returned
		///     <paramref name="filePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="filePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureMessage">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetVariableFilePath(this string path, out IVariableFilePath filePath, out string failureMessage)
		{
			filePath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			if (!path.IsValidVariableFilePath(out failureMessage))
			{
				return false;
			}

			filePath = new VariableFilePath(path);

			return true;
		}

		/// <summary>
		///     Try get a new <see cref="IVariableFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="path" /> is a valid file path that contains variables and as a consequence, the returned
		///     <paramref name="filePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="path">Represents the path.</param>
		/// <param name="filePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetVariableFilePath(this string path, out IVariableFilePath filePath)
		{
			string failureMessage;

			return path.TryGetVariableFilePath(out filePath, out failureMessage);
		}

		private static bool IsNullOrEmpty(Expression<Func<string>> expression, out string failureMessage)
		{
			var parameterInfo = expression.GetParameterInfo();

			if (parameterInfo.Value == null)
			{
				failureMessage = $"Argument '{parameterInfo.Name}' cannot be null.";

				return true;
			}

			if (parameterInfo.Value.Length == 0)
			{
				failureMessage = $"Argument '{parameterInfo.Name}' cannot be empty.";

				return true;
			}

			failureMessage = null;

			return false;
		}

		private static bool IsThisValidDirectoryPathAValidFilePath(this string path, out string fileName, out string failureMessage)
		{
			Argument.IsNotNullOrEmpty(nameof(path), path);
			Argument.IsValid(() => path, path.IsValidDirectoryPath());

			var normalizedPath = MiscHelpers.NormalizePath(path);

			if (normalizedPath.EndsWith(@"\.") || normalizedPath.EndsWith(@"\.."))
			{
				failureMessage = $"The argument {nameof(path)} is not a file path because it doesn't have a valid file name.";
				fileName = null;

				return false;
			}

			if (AbsoluteRelativePathHelpers.ContainsInnerSpecialDirectory(normalizedPath))
			{
				string resolvedPath, tempFailureMessage;

				AbsoluteRelativePathHelpers.TryResolveInnerSpecialDirectory(normalizedPath, out resolvedPath, out tempFailureMessage);

				normalizedPath = resolvedPath;
			}

			if (!MiscHelpers.HasParentDirectory(normalizedPath))
			{
				failureMessage = $"The argument {nameof(normalizedPath)} is not a file path because it doesn't have at least one parent directory.";
				fileName = null;

				return false;
			}

			fileName = MiscHelpers.GetLastName(normalizedPath);
			failureMessage = null;

			return true;
		}

		private static bool TryGetNotNullNormalizedPath(this string path, out string normalizedPath, out string failureMessage)
		{
			normalizedPath = null;

			if (IsNullOrEmpty(() => path, out failureMessage))
			{
				return false;
			}

			var tempNormalizedPath = MiscHelpers.NormalizePath(path);

			if (tempNormalizedPath.Length == 0)
			{
				failureMessage = $"The argument {nameof(normalizedPath)} is empty.";

				return false;
			}

			failureMessage = null;
			normalizedPath = tempNormalizedPath;

			return true;
		}
	}
}
