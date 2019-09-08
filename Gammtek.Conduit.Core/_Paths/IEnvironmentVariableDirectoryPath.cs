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

	/*[ContractClassFor(typeof (IEnvVarDirectoryPath))]
	internal abstract class IEnvVarDirectoryPathContract : IEnvVarDirectoryPath
	{
		public abstract string DirectoryName { get; }

		public abstract string EnvVar { get; }

		public abstract bool HasParentDirectory { get; }

		public abstract bool IsAbsolutePath { get; }

		public abstract bool IsDirectoryPath { get; }

		public abstract bool IsEnvVarPath { get; }

		public abstract bool IsFilePath { get; }

		public abstract bool IsRelativePath { get; }

		public abstract bool IsVariablePath { get; }

		public abstract IDirectoryPath ParentDirectoryPath { get; }

		public abstract PathMode PathMode { get; }

		IEnvVarDirectoryPath IEnvVarPath.ParentDirectoryPath
		{
			get { throw new NotImplementedException(); }
		}

		public IEnvVarDirectoryPath GetSisterDirectoryWithName(string directoryName)
		{
			Contract.Requires(directoryName != null, "directoryName must not be null");
			Contract.Requires(directoryName.Length > 0, "directoryName must not be empty");
			throw new NotImplementedException();
		}

		public IEnvVarFilePath GetSisterFileWithName(string fileName)
		{
			Contract.Requires(fileName != null, "fileName must not be null");
			Contract.Requires(fileName.Length > 0, "fileName must not be empty");
			throw new NotImplementedException();
		}

		public IEnvVarDirectoryPath GetChildDirectoryWithName(string directoryName)
		{
			Contract.Requires(directoryName != null, "directoryName must not be null");
			Contract.Requires(directoryName.Length > 0, "directoryName must not be empty");
			throw new NotImplementedException();
		}

		public IEnvVarFilePath GetChildFileWithName(string fileName)
		{
			Contract.Requires(fileName != null, "fileName must not be null");
			Contract.Requires(fileName.Length > 0, "fileName must not be empty");
			throw new NotImplementedException();
		}

		public abstract bool IsChildOf(IDirectoryPath parentDirectory);

		public abstract bool NotEquals(object obj);

		public EnvVarPathResolvingStatus TryResolve(out IAbsoluteDirectoryPath pathDirectoryResolved)
		{
			throw new NotImplementedException();
		}

		public bool TryResolve(out IAbsoluteDirectoryPath pathDirectoryResolved, out string failureReason)
		{
			throw new NotImplementedException();
		}

		public abstract EnvVarPathResolvingStatus TryResolve(out IAbsolutePath pathResolved);

		public abstract bool TryResolve(out IAbsolutePath pathResolved, out string failureReason);

		IDirectoryPath IDirectoryPath.GetSisterDirectoryWithName(string directoryName)
		{
			throw new NotImplementedException();
		}

		IFilePath IDirectoryPath.GetSisterFileWithName(string fileName)
		{
			throw new NotImplementedException();
		}

		IDirectoryPath IDirectoryPath.GetChildDirectoryWithName(string directoryName)
		{
			throw new NotImplementedException();
		}

		IFilePath IDirectoryPath.GetChildFileWithName(string fileName)
		{
			throw new NotImplementedException();
		}
	}*/
}
