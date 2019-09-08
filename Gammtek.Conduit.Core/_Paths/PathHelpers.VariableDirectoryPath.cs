using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class VariableDirectoryPath : VariablePathBase, IVariableDirectoryPath
		{
			internal VariableDirectoryPath(string path)
				: base(path)
			{
				//Debug.Assert(path.IsValidVariableDirectoryPath());
			}
			
			public string DirectoryName => MiscHelpers.GetLastName(CurrentPath);

			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public IVariableDirectoryPath GetChildDirectoryPath(string directoryName)
			{
				Argument.IsNotNullOrEmpty(nameof(directoryName), directoryName);

				var pathString = PathBrowsingHelpers.GetChildDirectoryPath(this, directoryName);

				//Debug.Assert(pathString.IsValidVariableDirectoryPath());

				return new VariableDirectoryPath(pathString);
			}

			public IVariableFilePath GetChildFilePath(string fileName)
			{
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);

				var pathString = PathBrowsingHelpers.GetChildFilePath(this, fileName);

				//Debug.Assert(pathString.IsValidVariableFilePath());

				return new VariableFilePath(pathString);
			}

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
			
			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables,
				out IAbsoluteDirectoryPath resolvedPath)
			{
				Argument.IsNotNull(nameof(variables), variables);

				IReadOnlyList<string> unresolvedVariables;

				return TryResolve(variables, out resolvedPath, out unresolvedVariables);
			}

			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables,
				out IAbsoluteDirectoryPath resolvedPath, out IReadOnlyList<string> unresolvedVariables)
			{
				Argument.IsNotNull(nameof(variables), variables);

				string path;

				if (!TryResolve(variables, out path, out unresolvedVariables))
				{
					resolvedPath = null;

					return VariablePathResolvingStatus.UnresolvedVariable;
				}

				if (!path.IsValidAbsoluteDirectoryPath())
				{
					resolvedPath = null;

					return VariablePathResolvingStatus.CannotConvertToAbsolutePath;
				}

				resolvedPath = path.ToAbsoluteDirectoryPath();

				return VariablePathResolvingStatus.Success;
			}

			public bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteDirectoryPath resolvedPath,
				out string failureMessage)
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
						failureMessage = GetVariableResolvedButCannotConvertToAbsolutePathFailureReason(variablesList, "directory");

						return false;
				}
			}

			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath)
			{
				IAbsoluteDirectoryPath directoryPath;

				var resolvingStatus = TryResolve(variables, out directoryPath);

				resolvedPath = directoryPath;

				return resolvingStatus;
			}

			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath,
				out IReadOnlyList<string> unresolvedVariables)
			{
				IAbsoluteDirectoryPath directoryPath;

				var resolvingStatus = TryResolve(variables, out directoryPath, out unresolvedVariables);

				resolvedPath = directoryPath;

				return resolvingStatus;
			}

			public override bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath, out string failureMessage)
			{
				IAbsoluteDirectoryPath directoryPath
					;
				var b = TryResolve(variables, out directoryPath, out failureMessage);

				resolvedPath = directoryPath;

				return b;
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
