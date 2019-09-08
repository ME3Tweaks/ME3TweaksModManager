namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a path to a file on file system.
	/// </summary>
	/// <remarks>
	///     The path can be relative or absolute.
	///     In case of an absolute IFilePath, the path represented can exist or not.
	///     The extension method <see cref="PathHelpers.ToFilePath(string)" /> can be called to create a new IFilePath object from a string.
	/// </remarks>
	public interface IFilePath : IPath
	{
		/// <summary>
		///     Gets a string representing the file name extension.
		/// </summary>
		/// <returns>
		///     Returns the file name extension if any, else returns an empty string.
		/// </returns>
		string FileExtension { get; }

		/// <summary>
		///     Gets a string representing the file name with its extension if any.
		/// </summary>
		/// <returns>
		///     Returns the file name with its extension if any.
		/// </returns>
		string FileName { get; }

		/// <summary>
		///     Gets a string representing the file name without its extension if any.
		/// </summary>
		/// <returns>
		///     Returns the file name without its extension if any.
		/// </returns>
		string FileNameWithoutExtension { get; }

		/// <summary>
		///     Returns a new directory path representing a directory with name <paramref name="directoryName" />, located in the same directory as this file.
		/// </summary>
		/// <remarks>This file nor the returned directory need to exist for this operation to complete properly.</remarks>
		/// <param name="directoryName">The sister directory name.</param>
		IDirectoryPath GetSisterDirectoryPath(string directoryName);

		/// <summary>
		///     Returns a new file path refering to a file with name <paramref name="fileName" />, located in the same directory as this file.
		/// </summary>
		/// <remarks>This file nor the returned file need to exist for this operation to complete properly.</remarks>
		/// <param name="fileName">The sister file name</param>
		IFilePath GetSisterFilePath(string fileName);

		/// <summary>
		///     Gets a value indicating whether this file name has the extension, <paramref name="extension" />.
		/// </summary>
		/// <param name="extension">The file extension. It must begin with a dot followed by one or many characters.</param>
		/// <returns>true if this file name has the extension, <paramref name="extension" />.</returns>
		bool HasExtension(string extension);

		/// <summary>
		///     Returns a new file path representing this file with its file name extension updated to <paramref name="extension" />.
		/// </summary>
		/// <remarks>
		///     The returned file nor this file need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="extension">The new file extension. It must begin with a dot followed by one or many characters.</param>
		IFilePath UpdateExtension(string extension);
	}

	/*[ContractClassFor(typeof (IFilePath))]
	internal abstract class IFilePathContract : IFilePath
	{
		public string FileExtension
		{
			get
			{
				Contract.Ensures(Contract.Result<string>() != null, "returned string is not null");
				throw new NotImplementedException();
			}
		}

		public string FileName
		{
			get { throw new NotImplementedException(); }
		}

		public string FileNameWithoutExtension
		{
			get { throw new NotImplementedException(); }
		}

		public abstract bool HasParentDirectory { get; }

		public abstract bool IsAbsolutePath { get; }

		public abstract bool IsDirectoryPath { get; }

		public abstract bool IsEnvVarPath { get; }

		public abstract bool IsFilePath { get; }

		public abstract bool IsRelativePath { get; }

		public abstract bool IsVariablePath { get; }

		public abstract IDirectoryPath ParentDirectoryPath { get; }

		public abstract PathMode PathMode { get; }

		public IDirectoryPath GetSisterDirectoryWithName(string directoryName)
		{
			Contract.Requires(directoryName != null, "directoryName must not be null");
			Contract.Requires(directoryName.Length > 0, "directoryName must not be empty");
			throw new NotImplementedException();
		}

		public IFilePath GetSisterFileWithName(string fileName)
		{
			Contract.Requires(fileName != null, "fileName must not be null");
			Contract.Requires(fileName.Length > 0, "fileName must not be empty");
			throw new NotImplementedException();
		}

		public bool HasExtension(string extension)
		{
			Contract.Requires(extension != null, "extension must not be null");
			Contract.Requires(extension.Length >= 2, "extension must have at least two characters");
			Contract.Requires(extension[0] == '.', "extension first character must be a dot");
			throw new NotImplementedException();
		}

		public abstract bool IsChildOf(IDirectoryPath parentDirectory);

		public abstract bool NotEquals(object obj);

		public IFilePath UpdateExtension(string newExtension)
		{
			Contract.Requires(newExtension != null, "newExtension must not be null");
			Contract.Requires(newExtension.Length >= 2, "newExtension must have at least two characters");
			Contract.Requires(newExtension[0] == '.', "newExtension first character must be a dot");
			throw new NotImplementedException();
		}
	}*/
}
