namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a directory path on file system, prefixed with an environment variable.
	/// </summary>
	public interface IEnvironmentVariableDirectoryPath : IDirectoryPath, IEnvironmentVariablePath
	{
		/// <summary>
		///     Returns a new directory path prefixed with an environment variable, representing a directory with name <paramref name="directoryName" />, located
		///     in this directory.
		/// </summary>
		/// <param name="directoryName">The child directory name.</param>
		new IEnvironmentVariableDirectoryPath GetChildDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new file path prefixed with an environment variable, representing a file with name <paramref name="fileName" />, located in this
		///     directory.
		/// </summary>
		/// <param name="fileName">The child file name.</param>
		new IEnvironmentVariableFilePath GetChildFilePath(string fileName);

		/// <summary>
		///     Returns a new directory path prefixed with an environment variable, representing a directory with name <paramref name="directoryName" />, located
		///     in the parent's directory of this directory.
		/// </summary>
		/// <param name="directoryName">The sister directory name.</param>
		/// <exception cref="System.InvalidOperationException">This relative directory path doesn't have a parent directory.</exception>
		new IEnvironmentVariableDirectoryPath GetSisterDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new file path prefixed with an environment variable, representing a file with name <paramref name="fileName" />, located in the
		///     parent's directory of this directory.
		/// </summary>
		/// <param name="fileName">The sister file name.</param>
		/// <exception cref="System.InvalidOperationException">This relative directory path doesn't have a parent directory.</exception>
		new IEnvironmentVariableFilePath GetSisterFilePath(string fileName);

		/// <summary>
		///     Returns <see cref="EnvironmentVariableResolvingStatus" />.<see cref="EnvironmentVariableResolvingStatus.Success" /> if this directory path is
		///     prefixed with an
		///     environment variable that can be resolved into a drive letter or a UNC absolute directory path.
		/// </summary>
		/// <param name="resolvedPath">It is the absolute directory path resolved returned by this method.</param>
		EnvironmentVariableResolvingStatus TryResolve(out IAbsoluteDirectoryPath resolvedPath);

		/// <summary>
		///     Returns <see cref="EnvironmentVariableResolvingStatus" />.<see cref="EnvironmentVariableResolvingStatus.Success" /> if this directory path is
		///     prefixed with an
		///     environment variable that can be resolved into a drive letter or a UNC absolute directory path.
		/// </summary>
		/// <param name="resolvedPath">It is the absolute directory path resolved returned by this method.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		bool TryResolve(out IAbsoluteDirectoryPath resolvedPath, out string failureMessage);
	}
}
