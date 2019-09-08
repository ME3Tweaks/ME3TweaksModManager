using System.Collections.Generic;

namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a directory path on file system, prefixed with an environment variable.
	/// </summary>
	public interface IVariableDirectoryPath : IDirectoryPath, IVariablePath
	{
		/// <summary>
		///     Returns a new directory path containing variables, representing a directory with name <paramref name="directoryName" />, located in this
		///     directory.
		/// </summary>
		/// <param name="directoryName">The child directory name.</param>
		new IVariableDirectoryPath GetChildDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new file path containing variables, representing a file with name <paramref name="fileName" />, located in this directory.
		/// </summary>
		/// <param name="fileName">The child file name.</param>
		new IVariableFilePath GetChildFilePath(string fileName);

		/// <summary>
		///     Returns a new directory path containing variables, representing a directory with name <paramref name="directoryName" />, located in the parent's
		///     directory of this directory.
		/// </summary>
		/// <param name="directoryName">The sister directory name.</param>
		/// <exception cref="System.InvalidOperationException">This relative directory path doesn't have a parent directory.</exception>
		new IVariableDirectoryPath GetSisterDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new file path containing variables, representing a file with name <paramref name="fileName" />, located in the parent's directory of
		///     this directory.
		/// </summary>
		/// <param name="fileName">The sister file name.</param>
		/// <exception cref="System.InvalidOperationException">This relative directory path doesn't have a parent directory.</exception>
		new IVariableFilePath GetSisterFilePath(string fileName);

		/// <summary>
		///     Returns <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.Success" /> if
		///     <see cref="IVariablePath.AllVariables" /> of this path can be resolved from <paramref name="variables" /> and the path can be resolved into
		///     a drive letter or a UNC absolute directory path.
		/// </summary>
		/// <param name="variables">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedPath">
		///     It is the absolute directory path resolved obtained if this method returns
		///     <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.Success" />.
		/// </param>
		VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteDirectoryPath resolvedPath);

		/// <summary>
		///     Returns <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.Success" /> if
		///     <see cref="IVariablePath.AllVariables" /> of this path can be resolved from <paramref name="variables" /> and the path can be resolved into
		///     a drive letter or a UNC absolute file path.
		/// </summary>
		/// <param name="variables">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedPath">
		///     It is the absolute directory path resolved obtained if this method returns
		///     <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.Success" />.
		/// </param>
		/// <param name="unresolvedVariables">
		///     This list contains one or several variables names unresolved, if this method returns
		///     <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.UnresolvedVariable" />.
		/// </param>
		VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteDirectoryPath resolvedPath,
			out IReadOnlyList<string> unresolvedVariables);

		/// <summary>
		///     Returns <i>true</i> if <see cref="IVariablePath.AllVariables" /> of this path can be resolved from <paramref name="variables" /> and the
		///     path can be resolved into a drive letter or a UNC absolute directory path.
		/// </summary>
		/// <param name="variables">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedPath">It is the absolute directory path resolved obtained if this method returns <i>true</i>.</param>
		/// <param name="failureMessage">If <i>false</i> is returned, <paramref name="failureMessage" /> contains the plain english description of the failure.</param>
		bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteDirectoryPath resolvedPath, out string failureMessage);
	}

	/*[ContractClassFor(typeof (IVariableDirectoryPath))]
	internal abstract class IVariableDirectoryPathContract : IVariableDirectoryPath
	{
		public abstract IReadOnlyList<string> AllVariables { get; }

		public abstract string DirectoryName { get; }

		public abstract bool HasParentDirectory { get; }

		public abstract bool IsAbsolutePath { get; }

		public abstract bool IsDirectoryPath { get; }

		public abstract bool IsEnvVarPath { get; }

		public abstract bool IsFilePath { get; }

		public abstract bool IsRelativePath { get; }

		public abstract bool IsVariablePath { get; }

		public abstract IDirectoryPath ParentDirectoryPath { get; }

		public abstract PathMode PathMode { get; }

		public abstract string PrefixVariable { get; }

		IVariableDirectoryPath IVariablePath.ParentDirectoryPath
		{
			get { throw new NotImplementedException(); }
		}

		public IVariableDirectoryPath GetSisterDirectoryWithName(string directoryName)
		{
			Contract.Requires(directoryName != null, "directoryName must not be null");
			Contract.Requires(directoryName.Length > 0, "directoryName must not be empty");
			throw new NotImplementedException();
		}

		public IVariableFilePath GetSisterFileWithName(string fileName)
		{
			Contract.Requires(fileName != null, "fileName must not be null");
			Contract.Requires(fileName.Length > 0, "fileName must not be empty");
			throw new NotImplementedException();
		}

		public IVariableDirectoryPath GetChildDirectoryWithName(string directoryName)
		{
			Contract.Requires(directoryName != null, "directoryName must not be null");
			Contract.Requires(directoryName.Length > 0, "directoryName must not be empty");
			throw new NotImplementedException();
		}

		public IVariableFilePath GetChildFileWithName(string fileName)
		{
			Contract.Requires(fileName != null, "fileName must not be null");
			Contract.Requires(fileName.Length > 0, "fileName must not be empty");
			throw new NotImplementedException();
		}

		public abstract bool IsChildOf(IDirectoryPath parentDirectory);

		public abstract bool NotEquals(object obj);

		public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variablesValues,
			out IAbsoluteDirectoryPath pathDirectoryResolved)
		{
			Contract.Requires(variablesValues != null, "variablesValues must not be null");
			throw new NotImplementedException();
		}

		public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variablesValues,
			out IAbsoluteDirectoryPath pathDirectoryResolved, out IReadOnlyList<string> unresolvedVariables)
		{
			Contract.Requires(variablesValues != null, "variablesValues must not be null");
			throw new NotImplementedException();
		}

		public bool TryResolve(IEnumerable<KeyValuePair<string, string>> variablesValues, out IAbsoluteDirectoryPath pathDirectoryResolved,
			out string failureReason)
		{
			Contract.Requires(variablesValues != null, "variablesValues must not be null");
			throw new NotImplementedException();
		}

		public abstract VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variablesValues, out IAbsolutePath pathResolved);

		public abstract VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variablesValues, out IAbsolutePath pathResolved,
			out IReadOnlyList<string> unresolvedVariables);

		public abstract bool TryResolve(IEnumerable<KeyValuePair<string, string>> variablesValues, out IAbsolutePath pathResolved, out string failureReason);

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
