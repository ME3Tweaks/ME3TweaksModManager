namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a path on file system, prefixed with an environment variable.
	/// </summary>
	public interface IEnvironmentVariablePath : IPath
	{
		/// <summary>
		///     Gets the environment variable string, prefixed and suffixed with two percents char.
		/// </summary>
		string EnvironmentVariable { get; }

		/// <summary>
		///     Returns a new directory path prefixed with an environment variable, representing the parent directory of this path prefixed with an environment
		///     variable.
		/// </summary>
		/// <exception cref="System.InvalidOperationException">This path prefixed with an environment variable has no parent directory.</exception>
		new IEnvironmentVariableDirectoryPath ParentDirectoryPath { get; }

		/// <summary>
		///     Returns <see cref="EnvironmentVariableResolvingStatus" />.<see cref="EnvironmentVariableResolvingStatus.Success" /> if this path is prefixed with
		///     an environment
		///     variable that can be resolved into a drive letter or a UNC absolute path.
		/// </summary>
		/// <param name="resolvedPath">It is the absolute path resolved returned by this method.</param>
		EnvironmentVariableResolvingStatus TryResolve(out IAbsolutePath resolvedPath);

		/// <summary>
		///     Returns <i>true</i> if this path is prefixed with an environment variable that can be resolved into a drive letter or a UNC absolute path.
		/// </summary>
		/// <param name="resolvedPath">It is the absolute path resolved returned by this method.</param>
		/// <param name="failureMessage">If <i>false</i> is returned, <paramref name="failureMessage" /> contains the plain english description of the failure.</param>
		bool TryResolve(out IAbsolutePath resolvedPath, out string failureMessage);
	}
}
