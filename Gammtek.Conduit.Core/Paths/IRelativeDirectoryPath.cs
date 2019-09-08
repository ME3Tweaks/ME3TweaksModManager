namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a relative directory path.
	/// </summary>
	/// <remarks>
	///     The extension method <see cref="PathHelpers.ToRelativeDirectoryPath(string)" /> can be called to create a new IRelativeDirectoryPath object from
	///     a string.
	/// </remarks>
	public interface IRelativeDirectoryPath : IDirectoryPath, IRelativePath
	{
		/// <summary>
		///     Resolve this relative directory from <paramref name="pivotDirectory" />. If this directory is "..\Dir2" and <paramref name="pivotDirectory" /> is
		///     "C:\Dir1\Dir3", the returned absolute directory is "C:\Dir1\Dir2".
		/// </summary>
		/// <remarks>
		///     The returned directory nor <paramref name="pivotDirectory" /> need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="pivotDirectory">The pivot directory from which the absolute path is computed.</param>
		/// <exception cref="System.ArgumentException">
		///     An absolute path cannot be resolved from <paramref name="pivotDirectory" />.
		///     This can happen for example if <paramref name="pivotDirectory" /> is "C:\Dir1" and this relative directory path is "..\..\Dir2".
		/// </exception>
		/// <returns>A new absolute directory path representing this relative directory resolved from <paramref name="pivotDirectory" />.</returns>
		new IAbsoluteDirectoryPath GetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory);

		/// <summary>
		///     Returns a new relative directory path representing a directory with name <paramref name="directoryName" />, located in this directory.
		/// </summary>
		/// <param name="directoryName">The child directory name.</param>
		new IRelativeDirectoryPath GetChildDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new relative file path representing a file with name <paramref name="fileName" />, located in this directory.
		/// </summary>
		/// <param name="fileName">The child file name.</param>
		new IRelativeFilePath GetChildFilePath(string fileName);

		/// <summary>
		///     Returns a new relative directory path representing a directory with name <paramref name="directoryName" />, located in the parent's directory of
		///     this directory.
		/// </summary>
		/// <param name="directoryName">The sister directory name.</param>
		/// <exception cref="System.InvalidOperationException">This relative directory path doesn't have a parent directory.</exception>
		new IRelativeDirectoryPath GetSisterDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new relative file path representing a file with name <paramref name="fileName" />, located in the parent's directory of this directory.
		/// </summary>
		/// <param name="fileName">The sister file name.</param>
		/// <exception cref="System.InvalidOperationException">This relative directory path doesn't have a parent directory.</exception>
		new IRelativeFilePath GetSisterFilePath(string fileName);
	}
}
