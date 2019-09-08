using System.Collections.Generic;

namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a file path on file system, prefixed with an environment variable.
	/// </summary>
	public interface IVariableFilePath : IFilePath, IVariablePath
	{
		/// <summary>
		///     Returns a new directory path containing variables, representing a directory with name <paramref name="directoryName" />, located in the same
		///     directory as this file.
		/// </summary>
		/// <param name="directoryName">The sister directory name.</param>
		new IVariableDirectoryPath GetSisterDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new file path containing variables, refering to a file with name <paramref name="fileName" />, located in the same directory as this
		///     file.
		/// </summary>
		/// <param name="fileName">The sister file name</param>
		new IVariableFilePath GetSisterFilePath(string fileName);

		/// <summary>
		///     Returns <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.Success" /> if
		///     <see cref="IVariablePath.AllVariables" /> of this path can be resolved from <paramref name="variables" /> and the path can be resolved into
		///     a drive letter or a UNC absolute file path.
		/// </summary>
		/// <param name="variables">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedPath">
		///     It is the absolute file path resolved obtained if this method returns <see cref="VariablePathResolvingStatus" />.
		///     <see cref="VariablePathResolvingStatus.Success" />.
		/// </param>
		VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteFilePath resolvedPath);

		/// <summary>
		///     Returns <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.Success" /> if
		///     <see cref="IVariablePath.AllVariables" /> of this path can be resolved from <paramref name="variables" /> and the path can be resolved into
		///     a drive letter or a UNC absolute file path.
		/// </summary>
		/// <param name="variables">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedPath">
		///     It is the absolute file path resolved obtained if this method returns <see cref="VariablePathResolvingStatus" />.
		///     <see cref="VariablePathResolvingStatus.Success" />.
		/// </param>
		/// <param name="unresolvedVariables">
		///     This list contains one or several variables names unresolved, if this method returns
		///     <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.UnresolvedVariable" />.
		/// </param>
		VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteFilePath resolvedPath,
			out IReadOnlyList<string> unresolvedVariables);

		/// <summary>
		///     Returns <i>true</i> if <see cref="IVariablePath.AllVariables" /> of this path can be resolved from <paramref name="variables" /> and the
		///     path can be resolved into a drive letter or a UNC absolute file path.
		/// </summary>
		/// <param name="variables">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedPath">It is the absolute file path resolved obtained if this method returns <i>true</i>.</param>
		/// <param name="failureMessage">If <i>false</i> is returned, <paramref name="failureMessage" /> contains the plain english description of the failure.</param>
		bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteFilePath resolvedPath, out string failureMessage);

		/// <summary>
		///     Returns a new file path containing variables, representing this file with its file name extension updated to <paramref name="extension" />.
		/// </summary>
		/// <param name="extension">The new file extension. It must begin with a dot followed by one or many characters.</param>
		new IVariableFilePath UpdateExtension(string extension);
	}
}
