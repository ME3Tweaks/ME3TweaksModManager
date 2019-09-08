using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class EnvironmentVariableDirectoryPath : EnvironmentVariablePathBase, IEnvironmentVariableDirectoryPath
		{
			internal EnvironmentVariableDirectoryPath(string path)
				: base(path)
			{
				//Debug.Assert(path.IsValidEnvVarDirectoryPath());
			}
			
			public string DirectoryName => MiscHelpers.GetLastName(CurrentPath);

			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public IEnvironmentVariableDirectoryPath GetChildDirectoryPath(string directoryName)
			{
				Argument.IsNotNullOrEmpty(nameof(directoryName), directoryName);

				var pathString = PathBrowsingHelpers.GetChildDirectoryPath(this, directoryName);

				//Debug.Assert(pathString.IsValidEnvVarDirectoryPath());

				return new EnvironmentVariableDirectoryPath(pathString);
			}

			public IEnvironmentVariableFilePath GetChildFilePath(string fileName)
			{
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);

				var pathString = PathBrowsingHelpers.GetChildFilePath(this, fileName);

				//Debug.Assert(pathString.IsValidEnvVarFilePath());

				return new EnvironmentVariableFilePath(pathString);
			}

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
			
			public EnvironmentVariableResolvingStatus TryResolve(out IAbsoluteDirectoryPath resolvedPath)
			{
				resolvedPath = null;

				string path;

				if (!TryResolveEnvironmentVariable(out path))
				{
					return EnvironmentVariableResolvingStatus.UnresolvedEnvironmentVariable;
				}

				if (!path.IsValidAbsoluteDirectoryPath())
				{
					return EnvironmentVariableResolvingStatus.CannotConvertToAbsolutePath;
				}

				resolvedPath = path.ToAbsoluteDirectoryPath();

				return EnvironmentVariableResolvingStatus.Success;
			}

			public bool TryResolve(out IAbsoluteDirectoryPath resolvedPath, out string failureMessage)
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
				IAbsoluteDirectoryPath directoryPath;

				var resolvingStatus = TryResolve(out directoryPath);

				resolvedPath = directoryPath;

				return resolvingStatus;
			}

			public override bool TryResolve(out IAbsolutePath resolvedPath, out string failureMessage)
			{
				IAbsoluteDirectoryPath directoryPath;

				var result = TryResolve(out directoryPath, out failureMessage);

				resolvedPath = directoryPath;

				return result;
			}

			IDirectoryPath IDirectoryPath.GetChildDirectoryPath(string directoryName)
			{
				return GetChildDirectoryPath(directoryName);
			}

			IFilePath IDirectoryPath.GetChildFilePath(string fileName)
			{
				return GetChildFilePath(fileName);
			}

			IDirectoryPath IDirectoryPath.GetSisterDirectoryPath(string directoryName)
			{
				return GetSisterDirectoryPath(directoryName);
			}
			
			IFilePath IDirectoryPath.GetSisterFilePath(string fileName)
			{
				return GetSisterFilePath(fileName);
			}
		}
	}
}
