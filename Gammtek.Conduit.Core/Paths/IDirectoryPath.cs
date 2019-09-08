namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a path to a directory on file system.
	/// </summary>
	/// <remarks>
	///     The path can be relative or absolute.
	///     The extension method <see cref="PathHelpers.ToDirectoryPath(string)" /> can be called to create a new IDirectoryPath object from a string.
	/// </remarks>
	public interface IDirectoryPath : IPath
	{
		/// <summary>
		///     Gets a string representing the directory name.
		/// </summary>
		/// <remarks>
		///     If the directory is a root volume, like C:, returns an empty string.
		/// </remarks>
		/// <returns>
		///     The directory name.
		/// </returns>
		string DirectoryName { get; }

		/// <summary>
		///     Returns a new directory path representing a directory with name <paramref name="directoryName" />, located in this directory.
		/// </summary>
		/// <remarks>This directory nor the returned directory need to exist for this operation to complete properly.</remarks>
		/// <param name="directoryName">The child directory name.</param>
		/// <exception cref="System.InvalidOperationException">This directory path doesn't have a parent directory.</exception>
		IDirectoryPath GetChildDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new file path representing a file with name <paramref name="fileName" />, located in this directory.
		/// </summary>
		/// <remarks>This directory nor the returned file need to exist for this operation to complete properly.</remarks>
		/// <param name="fileName">The child file name.</param>
		IFilePath GetChildFilePath(string fileName);

		/// <summary>
		///     Returns a new directory path representing a directory with name <paramref name="directoryName" />, located in the parent's directory of this
		///     directory.
		/// </summary>
		/// <remarks>This directory nor the returned directory need to exist for this operation to complete properly.</remarks>
		/// <param name="directoryName">The sister directory name.</param>
		/// <exception cref="System.InvalidOperationException">This directory path doesn't have a parent directory.</exception>
		IDirectoryPath GetSisterDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new file path representing a file with name <paramref name="fileName" />, located in the parent's directory of this directory.
		/// </summary>
		/// <remarks>This directory nor the returned file need to exist for this operation to complete properly.</remarks>
		/// <param name="fileName">The sister file name.</param>
		/// <exception cref="System.InvalidOperationException">This directory path doesn't have a parent directory.</exception>
		IFilePath GetSisterFilePath(string fileName);
	}
}
