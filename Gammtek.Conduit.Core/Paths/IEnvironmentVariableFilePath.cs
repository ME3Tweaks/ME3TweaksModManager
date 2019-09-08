namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a file path on file system, prefixed with an environment variable.
	/// </summary>
	public interface IEnvironmentVariableFilePath : IFilePath, IEnvironmentVariablePath
	{
		/// <summary>
		///     Returns a new directory path prefixed with an environment variable, representing a directory with name <paramref name="directoryName" />, located
		///     in the same directory as this file.
		/// </summary>
		/// <param name="directoryName">The sister directory name.</param>
		new IEnvironmentVariableDirectoryPath GetSisterDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new file path prefixed with an environment variable, refering to a file with name <paramref name="fileName" />, located in the same
		///     directory as this file.
		/// </summary>
		/// <param name="fileName">The sister file name</param>
		new IEnvironmentVariableFilePath GetSisterFilePath(string fileName);

		/// <summary>
		///     Returns <see cref="EnvironmentVariableResolvingStatus" />.<see cref="EnvironmentVariableResolvingStatus.Success" /> if this file path is prefixed
		///     with an
		///     environment variable that can be resolved into a drive letter or a UNC absolute file path.
		/// </summary>
		/// <param name="resolvedPath">It is the absolute file path resolved returned by this method.</param>
		EnvironmentVariableResolvingStatus TryResolve(out IAbsoluteFilePath resolvedPath);

		/// <summary>
		///     Returns <see cref="EnvironmentVariableResolvingStatus" />.<see cref="EnvironmentVariableResolvingStatus.Success" /> if this file path is prefixed
		///     with an
		///     environment variable that can be resolved into a drive letter or a UNC absolute file path.
		/// </summary>
		/// <param name="resolvedPath">It is the absolute file path resolved returned by this method.</param>
		/// <param name="failureMessage">If false is returned, failureReason contains the plain english description of the failure.</param>
		bool TryResolve(out IAbsoluteFilePath resolvedPath, out string failureMessage);

		/// <summary>
		///     Returns a new file path prefixed with an environment variable, representing this file with its file name extension updated to
		///     <paramref name="extension" />.
		/// </summary>
		/// <param name="extension">The new file extension. It must begin with a dot followed by one or many characters.</param>
		new IEnvironmentVariableFilePath UpdateExtension(string extension);
	}
}
