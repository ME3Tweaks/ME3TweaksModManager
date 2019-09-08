using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class VariableFilePath : VariablePathBase, IVariableFilePath
		{
			internal VariableFilePath(string path)
				: base(path)
			{
				//Debug.Assert(path.IsValidVariableFilePath());
			}

			public string FileExtension => FileNameHelpers.GetFileNameExtension(CurrentPath);
			
			public string FileName => FileNameHelpers.GetFileName(CurrentPath);

			public string FileNameWithoutExtension => FileNameHelpers.GetFileNameWithoutExtension(CurrentPath);

			public override bool IsDirectoryPath => false;

			public override bool IsFilePath => true;

			public IVariableDirectoryPath GetSisterDirectoryPath(string directoryName)
			{
				Argument.IsNotNullOrEmpty(nameof(directoryName), directoryName);

				var path = PathBrowsingHelpers.GetSisterDirectoryPath(this, directoryName);

				return path as IVariableDirectoryPath;
			}
			
			public IVariableFilePath GetSisterFilePath(string fileName)
			{
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);

				var path = PathBrowsingHelpers.GetSisterFilePath(this, fileName);

				return path as IVariableFilePath;
			}

			public bool HasExtension(string extension)
			{
				Argument.IsNotNull(nameof(extension), extension);
				Argument.IsValid(nameof(extension), extension, extension.Length >= 2 && extension[0] == '.');

				return FileNameHelpers.HasExtension(CurrentPath, extension);
			}
			
			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteFilePath resolvedPath)
			{
				Argument.IsNotNull(nameof(variables), variables);

				IReadOnlyList<string> unresolvedVariables;

				return TryResolve(variables, out resolvedPath, out unresolvedVariables);
			}

			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteFilePath resolvedPath,
				out IReadOnlyList<string> unresolvedVariables)
			{
				Argument.IsNotNull(nameof(variables), variables);

				string path;

				if (!TryResolve(variables, out path, out unresolvedVariables))
				{
					resolvedPath = null;

					return VariablePathResolvingStatus.UnresolvedVariable;
				}

				if (!path.IsValidAbsoluteFilePath())
				{
					resolvedPath = null;

					return VariablePathResolvingStatus.CannotConvertToAbsolutePath;
				}

				resolvedPath = path.ToAbsoluteFilePath();

				return VariablePathResolvingStatus.Success;
			}

			public bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteFilePath resolvedPath, out string failureMessage)
			{
				Argument.IsNotNull(nameof(variables), variables);

				IReadOnlyList<string> unresolvedVariables;

				var variablesList = variables as IList<KeyValuePair<string, string>> ?? variables.ToList();
				var status = TryResolve(variablesList, out resolvedPath, out unresolvedVariables);

				switch (status)
				{
					default:
						//Debug.Assert(status == VariablePathResolvingStatus.Success);

						failureMessage = null;

						return true;
					case VariablePathResolvingStatus.UnresolvedVariable:
						//Debug.Assert(unresolvedVariables != null);
						//Debug.Assert(unresolvedVariables.Count > 0);

						failureMessage = VariablePathHelpers.GetUnresolvedVariableFailureReason(unresolvedVariables);

						return false;
					case VariablePathResolvingStatus.CannotConvertToAbsolutePath:
						failureMessage = GetVariableResolvedButCannotConvertToAbsolutePathFailureReason(variablesList, "file");

						return false;
				}
			}

			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath)
			{
				IAbsoluteFilePath filePath;

				var resolvingStatus = TryResolve(variables, out filePath);

				resolvedPath = filePath;

				return resolvingStatus;
			}

			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath,
				out IReadOnlyList<string> unresolvedVariables)
			{
				IAbsoluteFilePath filePath;

				var resolvingStatus = TryResolve(variables, out filePath, out unresolvedVariables);

				resolvedPath = filePath;

				return resolvingStatus;
			}

			public override bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath, out string failureMessage)
			{
				IAbsoluteFilePath filePath;

				var result = TryResolve(variables, out filePath, out failureMessage);

				resolvedPath = filePath;

				return result;
			}

			public IVariableFilePath UpdateExtension(string extension)
			{
				Argument.IsNotNull(nameof(extension), extension);
				Argument.IsValid(nameof(extension), extension, extension.Length >= 2 && extension[0] == '.');

				var path = PathBrowsingHelpers.UpdateExtension(this, extension);

				//Debug.Assert(pathString.IsValidVariableFilePath());

				return new VariableFilePath(path);
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
