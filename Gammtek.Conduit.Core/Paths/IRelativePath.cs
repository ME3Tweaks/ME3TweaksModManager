namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a relative path to a file or directory.
	/// </summary>
	public interface IRelativePath : IPath
	{
		/// <summary>
		///     Returns a new relative directory path representing the parent directory of this relative path.
		/// </summary>
		/// <exception cref="System.InvalidOperationException">This relative path has no parent directory.</exception>
		new IRelativeDirectoryPath ParentDirectoryPath { get; }

		/// <summary>
		///     Gets a value indicating whether this relative path can be resolved from <paramref name="pivotDirectory" />.
		/// </summary>
		/// <remarks>
		///     An absolute path cannot be resolved for example if <paramref name="pivotDirectory" /> is "C:\Dir1" and this relative path is "..\..\Dir2".
		/// </remarks>
		/// <param name="pivotDirectory">The pivot directory from which the absolute path should be computed.</param>
		/// <returns>true if this relative path can be resolved from <paramref name="pivotDirectory" />, else returns false.</returns>
		bool CanGetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory);

		/// <summary>
		///     Gets a value indicating whether this relative path can be resolved from <paramref name="pivotDirectory" />.
		/// </summary>
		/// <remarks>
		///     An absolute path cannot be resolved for example if <paramref name="pivotDirectory" /> is "C:\Dir1" and this relative path is "..\..\Dir2".
		/// </remarks>
		/// <param name="pivotDirectory">The pivot directory from which the absolute path should be computed.</param>
		/// <param name="failureMessage">If this method return <i>false</i>, it contains the plain-english description of the cause of this failure.</param>
		/// <returns>true if this relative path can be resolved from <paramref name="pivotDirectory" />, else returns false.</returns>
		bool CanGetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory, out string failureMessage);

		/// <summary>
		///     A new absolute path representing this relative path resolved from <paramref name="pivotDirectory" />.
		/// </summary>
		/// <remarks>
		///     If this path is "..\Dir2\File.txt" and <paramref name="pivotDirectory" /> is "C:\Dir1\Dir3", the returned relative file is
		///     "C:\Dir1\Dir2\File.txt".
		///     If this path is "..\Dir2" and <paramref name="pivotDirectory" /> is "C:\Dir1\Dir3", the returned relative file is "C:\Dir1\Dir2".
		///     This method is hidden in <see cref="T:NDepend.Path.IAbsoluteFilePath" /> and <see cref="T:NDepend.Path.IAbsoluteDirectoryPath" /> to get a typed
		///     result.
		///     The returned file or directory path nor <paramref name="pivotDirectory" /> need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="pivotDirectory">The pivot directory from which the absolute path is computed.</param>
		/// <exception cref="System.ArgumentException">
		///     An absolute path cannot be resolved from <paramref name="pivotDirectory" />.
		///     This can happen for example if <paramref name="pivotDirectory" /> is "C:\Dir1" and this relative path is "..\..\Dir2".
		/// </exception>
		/// <returns>A new absolute file path representing this relative file resolved from <paramref name="pivotDirectory" />.</returns>
		IAbsolutePath GetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory);
	}
}
