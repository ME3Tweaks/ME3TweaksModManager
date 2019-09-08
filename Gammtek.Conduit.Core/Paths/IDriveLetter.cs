using System.IO;

namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a drive on file system.
	/// </summary>
	public interface IDriveLetter
	{
		/// <summary>
		///     Returns a DriveInfo object representing this drive.
		/// </summary>
		/// <exception cref="DriveNotFoundException">This drive doesn't refer to an existing drive.</exception>
		/// <seealso cref="P:Gammtek.Conduit.Paths.IAbsoluteDirectoryPath.DirectoryInfo" />
		/// <seealso cref="P:Gammtek.Conduit.Paths.IAbsoluteFilePath.FileInfo" />
		DriveInfo DriveInfo { get; }

		/// <summary>
		///     Returns the letter character of this drive.
		/// </summary>
		/// <remarks>
		///     The letter returned can be upper or lower case.
		/// </remarks>
		char Letter { get; }

		/// <summary>Returns true if obj is null, is not an IDrive, or is an IDrive representing a different drive than this drive (case insensitive).</summary>
		bool NotEquals(object obj);
	}
}
