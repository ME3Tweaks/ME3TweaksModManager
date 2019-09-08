namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a path, absolute or relative, to a file or to a directory.
	/// </summary>
	/// <remarks>
	///     Equals(), ToString() and GetHashCode() operations are overridden from System.Object and are supported by all paths objects.
	///     Equals() and GetHashCode() are string case insensitive.
	/// </remarks>
	public interface IPath
	{
		/// <summary>Gets a value indicating whether this path has a parent directory path</summary>
		/// <remarks>
		///     Root directories representing a drive, like C: or D: don't have a parent directory path.
		///     Relative path like ".\" or "..\" don't have a parent directory path.
		///     Notice that a file path necessarily has a parent directory path.
		/// </remarks>
		/// <returns>true if this path has a parent directory path, else returns false.</returns>
		bool HasParentDirectory { get; }

		/// <summary>Gets a value indicating whether this path is an absolute path.</summary>
		/// <remarks>
		///     An absolute path can be down-casted to <see cref="IAbsolutePath" />.
		///     A <see cref="IAbsolutePath" /> can be down-casted to a <see cref="IAbsoluteFilePath" /> or (exclusive) a <see cref="IAbsoluteDirectoryPath" />.
		/// </remarks>
		/// <returns><i>true</i> if this path is an absolute path, else returns false.</returns>
		bool IsAbsolutePath { get; }

		/// <summary>Gets a value indicating whether this path is a directory path.</summary>
		/// <remarks>
		///     A relative path can be down-casted to <see cref="IDirectoryPath" />.
		///     A <see cref="IDirectoryPath" /> can be down-casted to a <see cref="IRelativeDirectoryPath" /> or (exclusive) a
		///     <see cref="IAbsoluteDirectoryPath" />.
		/// </remarks>
		/// <returns><i>true</i> if this path is a directory path, else returns <i>false</i>.</returns>
		bool IsDirectoryPath { get; }

		/// <summary>Gets a value indicating whether this path is prefixed with an environment variable.</summary>
		/// <remarks>
		///     A path prefixed with an environment variable can be down-casted to <see cref="IEnvironmentVariablePath" />.
		///     A <see cref="IEnvironmentVariablePath" /> can be down-casted to a <see cref="IEnvironmentVariableFilePath" /> or (exclusive) a
		///     <see cref="IEnvironmentVariableDirectoryPath" />.
		/// </remarks>
		/// <returns><i>true</i> if this path is prefixed with an environment variable, else returns <i>false</i>.</returns>
		bool IsEnvVarPath { get; }

		/// <summary>Gets a value indicating whether this path is a file path.</summary>
		/// <remarks>
		///     A relative path can be down-casted to <see cref="IFilePath" />.
		///     A <see cref="IFilePath" /> can be down-casted to a <see cref="IRelativeFilePath" /> or (exclusive) a <see cref="IAbsoluteFilePath" />.
		/// </remarks>
		/// <returns><i>true</i> if this path is a file path, else returns <i>false</i>.</returns>
		bool IsFilePath { get; }

		/// <summary>Gets a value indicating whether this path is a relative path.</summary>
		/// <remarks>
		///     A relative path can be down-casted to <see cref="IRelativePath" />.
		///     A <see cref="IRelativePath" /> can be down-casted to a <see cref="IRelativeFilePath" /> or (exclusive) a <see cref="IRelativeDirectoryPath" />.
		/// </remarks>
		/// <returns><i>true</i> if this path is a relative path, else returns false.</returns>
		bool IsRelativePath { get; }

		/// <summary>Gets a value indicating whether this path contains variable(s).</summary>
		/// <remarks>
		///     A path contains variable(s) can be down-casted to <see cref="IVariablePath" />.
		///     A <see cref="IVariablePath" /> can be down-casted to a <see cref="IVariableFilePath" /> or (exclusive) a <see cref="IVariableDirectoryPath" />.
		/// </remarks>
		/// <returns><i>true</i> if this path contains variable(s), else returns <i>false</i>.</returns>
		bool IsVariablePath { get; }

		/// <summary>Returns the parent directory path.</summary>
		/// <exception cref="System.InvalidOperationException">
		///     This path doesn't have a parent directory path.
		///     Root directories representing a drive, like C: or D: don't have a parent directory path.
		///     Relative path like ".\" or "..\" don't have a parent directory path.
		///     Notice that a file path necessarily has a parent directory path.
		/// </exception>
		IDirectoryPath ParentDirectoryPath { get; }

		/// <summary>
		///     Gets a value indicating this path mode as defined in the enumeration <see cref="PathType" />.
		/// </summary>
		PathType PathType { get; }

		/// <summary>
		///     Gets a value indicating whether this path is a child path of <paramref name="parentDirectory" />.
		/// </summary>
		/// <remarks>This path resource nor <paramref name="parentDirectory" /> need to exist for this operation to complete properly.</remarks>
		/// <param name="parentDirectory">The parent directory.</param>
		/// <returns>true of this directory is a child directory of <paramref name="parentDirectory" />, else false.</returns>
		bool IsChildOf(IDirectoryPath parentDirectory);

		/// <summary>Returns true if obj is null, is not an IPath, or is an IPath representing a different path than this path (case insensitive).</summary>
		/// <remarks>
		///     This method is the opposite of the IPath.Equals() method, overridden from System.Object.
		///     It can be used to make the negation in !Equals more obvious.
		/// </remarks>
		bool NotEquals(object obj);
	}

	/*[ContractClassFor(typeof (IPath))]
	internal abstract class IPathContract : IPath
	{
		public bool HasParentDirectory
		{
			get { throw new NotImplementedException(); }
		}

		public bool IsAbsolutePath
		{
			get { throw new NotImplementedException(); }
		}

		public bool IsDirectoryPath
		{
			get { throw new NotImplementedException(); }
		}

		public abstract bool IsEnvVarPath { get; }

		public bool IsFilePath
		{
			get { throw new NotImplementedException(); }
		}

		public bool IsRelativePath
		{
			get { throw new NotImplementedException(); }
		}

		public abstract bool IsVariablePath { get; }

		public IDirectoryPath ParentDirectoryPath
		{
			get
			{
				Contract.Ensures(Contract.Result<IDirectoryPath>() != null, "returned path is not null");
				throw new NotImplementedException();
			}
		}

		public PathMode PathMode
		{
			get { throw new NotImplementedException(); }
		}

		public bool IsChildOf(IDirectoryPath parentDir)
		{
			Contract.Requires(parentDir != null, "parentDir must not be null");
			throw new NotImplementedException();
		}

		public bool NotEquals(object obj)
		{
			throw new NotImplementedException();
		}
	}*/
}
