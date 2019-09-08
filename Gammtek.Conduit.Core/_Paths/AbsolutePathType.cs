namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Defines the kind of an absolute path.
	/// </summary>
	public enum AbsolutePathType
	{
		/// <summary>
		///     Represents an absolute path prefixed with a drive letter like "C:\".
		/// </summary>
		DriveLetter,

		/// <summary>
		///     Represents a UNC absolute path with a syntax like "\\server\share\path".
		/// </summary>
		/// <remarks>
		///     Notice the related properties <see cref="IAbsolutePath" />.<see cref="IAbsolutePath.UNCServer" /> and <see cref="IAbsolutePath" />.
		///     <see cref="IAbsolutePath.UNCShare" />.
		/// </remarks>
		UNC
	}
}
