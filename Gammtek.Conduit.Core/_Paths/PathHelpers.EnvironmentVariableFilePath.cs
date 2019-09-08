using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class EnvironmentVariableFilePath : EnvironmentVariablePathBase, IEnvironmentVariableFilePath
		{
			internal EnvironmentVariableFilePath(string path)
				: base(path)
			{
				//Debug.Assert(path.IsValidEnvVarFilePath());
			}

			public string FileExtension => FileNameHelpers.GetFileNameExtension(CurrentPath);
			
			public string FileName => FileNameHelpers.GetFileName(CurrentPath);

			public string FileNameWithoutExtension => FileNameHelpers.GetFileNameWithoutExtension(CurrentPath);

			public override bool IsDirectoryPath => false;

			public override bool IsFilePath => true;

			public IEnvironmentVariableDirectoryPath GetSisterDirectoryPath(string directoryName)
			{
				Argument.IsNotNullOrEmpty(nameof(directoryName), directoryName);

				var path = PathBrowsingHelpers.GetSisterDirectoryPath(this, directoryName);

				return path as IEnvironmentVariableDirectoryPath;
			}
			
			public IEnvironmentVariableFilePath GetSisterFilePath(string fileName)
			{
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);

				var path = PathBrowsingHelpers.GetSisterFilePath(this, fileName);

				return path as IEnvironmentVariableFilePath;
			}

			public bool HasExtension(string extension)
			{
				Argument.IsNotNull(nameof(extension), extension);
				Argument.IsValid(nameof(extension), extension, extension.Length >= 2 && extension[0] == '.');

				return FileNameHelpers.HasExtension(CurrentPath, extension);
			}
			
			public EnvironmentVariableResolvingStatus TryResolve(out IAbsoluteFilePath resolvedPath)
			{
				resolvedPath = null;

				string path;

				if (!TryResolveEnvironmentVariable(out path))
				{
					return EnvironmentVariableResolvingStatus.UnresolvedEnvironmentVariable;
				}

				if (!path.IsValidAbsoluteFilePath())
				{
					return EnvironmentVariableResolvingStatus.CannotConvertToAbsolutePath;
				}

				resolvedPath = path.ToAbsoluteFilePath();

				return EnvironmentVariableResolvingStatus.Success;
			}

			public bool TryResolve(out IAbsoluteFilePath resolvedPath, out string failureMessage)
			{
				var resolvingStatus = TryResolve(out resolvedPath);

				switch (resolvingStatus)
				{
					default:
						//Debug.Assert(resolvingStatus == EnvironmentVariableResolvingStatus.Success);
						//Debug.Assert(resolvedPath != null);

						failureMessage = null;

						return true;
					case EnvironmentVariableResolvingStatus.UnresolvedEnvironmentVariable:
						failureMessage = GetErrorUnresolvedEnvVarFailureReason();

						return false;
					case EnvironmentVariableResolvingStatus.CannotConvertToAbsolutePath:
						failureMessage = GetErrorEnvVarResolvedButCannotConvertToAbsolutePathFailureReason();

						return false;
				}
			}

			public override EnvironmentVariableResolvingStatus TryResolve(out IAbsolutePath resolvedPath)
			{
				IAbsoluteFilePath filePath;

				var resolvingStatus = TryResolve(out filePath);

				resolvedPath = filePath;

				return resolvingStatus;
			}

			public override bool TryResolve(out IAbsolutePath resolvedPath, out string failureMessage)
			{
				IAbsoluteFilePath filePath;

				var result = TryResolve(out filePath, out failureMessage);

				resolvedPath = filePath;

				return result;
			}

			public IEnvironmentVariableFilePath UpdateExtension(string extension)
			{
				Argument.IsNotNull(nameof(extension), extension);
				Argument.IsValid(nameof(extension), extension, extension.Length >= 2 && extension[0] == '.');

				var pathString = PathBrowsingHelpers.UpdateExtension(this, extension);

				//Debug.Assert(pathString.IsValidEnvVarFilePath());

				return new EnvironmentVariableFilePath(pathString);
			}

			IDirectoryPath IFilePath.GetSisterDirectoryPath(string directoryName)
			{
				return GetSisterDirectoryPath(directoryName);
			}
			
			IFilePath IFilePath.GetSisterFilePath(string fileName)
			{
				return GetSisterFilePath(fileName);
			}

			IFilePath IFilePath.UpdateExtension(string extension)
			{
				return UpdateExtension(extension);
			}
		}
	}
}
