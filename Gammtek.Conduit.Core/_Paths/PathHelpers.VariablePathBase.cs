using System;
using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private abstract class VariablePathBase : PathBase, IVariablePath
		{
			protected VariablePathBase(string path)
				: base(path)
			{
				//Debug.Assert(path.IsNormalized());

				//Debug.Assert(CurrentPath != null);
				//Debug.Assert(CurrentPath.Length > 0);
				//Debug.Assert(CurrentPath.IsNormalized());

				// It is important to use m_PathString and not pathString in IsAVariablePath() !
				// Indeed, since InnerSpecialDir have been resolved, some variable might have disappeared
				// like if pathString was "$(v1)\$(v2)\.." and m_PathString became "$(v1)"
				IReadOnlyList<string> variables;
				string failureMessage;

				VariablePathHelpers.IsAVariablePath(CurrentPath, out variables, out failureMessage);

				//Debug.Assert(b);
				//Debug.Assert(variables != null);
				//Debug.Assert(variables.Count > 0);
				//Debug.Assert(variables.All(v => v != null));
				//Debug.Assert(variables.All(v => v.Length > 0));

				AllVariables = variables;
			}

			public IReadOnlyList<string> AllVariables { get; }

			public override bool HasParentDirectory => VariablePathHelpers.HasParentDirectory(CurrentPath);

			public override bool IsAbsolutePath => false;

			public override bool IsEnvVarPath => false;

			public override bool IsRelativePath => false;

			public override bool IsVariablePath => true;

			public override IDirectoryPath ParentDirectoryPath => (this as IVariablePath).ParentDirectoryPath;

			public override PathType PathType => PathType.Variable;
			
			public string PrefixVariable => AllVariables[0];
			
			IVariableDirectoryPath IVariablePath.ParentDirectoryPath => VariablePathHelpers.GetParentDirectory(CurrentPath).ToVariableDirectoryPath();

			public abstract VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath);

			public abstract VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath,
				out IReadOnlyList<string> unresolvedVariables);

			public abstract bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath, out string failureMessage);

			protected string GetVariableResolvedButCannotConvertToAbsolutePathFailureReason(IEnumerable<KeyValuePair<string, string>> variables, string fileOrDirectory)
			{
				Argument.IsNotNull(nameof(variables), variables);
				Argument.IsNotNullOrEmpty(nameof(fileOrDirectory), fileOrDirectory);

				// Need to obtain again pathStringResolved to include it into the failureReason!
				string resolvedPath;
				IReadOnlyList<string> unresolvedVariables;

				TryResolve(variables, out resolvedPath, out unresolvedVariables);

				//Debug.Assert(b);

				return @"All variable(s) have been resolved, but the resulting string {" + resolvedPath + "} cannot be converted to an absolute "
					   + fileOrDirectory + " path.";
			}

			protected bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out string resolvedPath, out IReadOnlyList<string> unresolvedVariables)
			{
				Argument.IsNotNull(nameof(variables), variables);

				var path = CurrentPath;

				var unresolvedVariablesList = new List<string>();
				var variablesToResolve = AllVariables.Count;

				//Debug.Assert(nbVariablesToResolve > 0);

				var variablesList = variables as IList<KeyValuePair<string, string>> ?? variables.ToList();

				for (var i = 0; i < variablesToResolve; i++)
				{
					var variableNameToResolve = AllVariables[i];

					//Debug.Assert(variableNameToResolve != null);
					//Debug.Assert(variableNameToResolve.Length > 0);

					var resolved = false;

					foreach (var pair in variablesList)
					{
						var pairVariableName = pair.Key;

						// Support these two cases!
						if (pairVariableName == null)
						{
							continue;
						}

						if (pairVariableName.Length == 0)
						{
							continue;
						} // 

						if (string.Compare(pairVariableName, variableNameToResolve, StringComparison.OrdinalIgnoreCase) != 0)
						{
							continue;
						} // true for ignore case! variable names are case insensitive

						resolved = true;

						var variableValue = pair.Value ?? string.Empty;

						path = VariablePathHelpers.ReplaceVariableWithValue(path, variableNameToResolve, variableValue);
					}

					if (!resolved)
					{
						unresolvedVariablesList.Add(variableNameToResolve);
					}
				}

				if (unresolvedVariablesList.Count > 0)
				{
					unresolvedVariables = unresolvedVariablesList.ToReadOnlyWrappedList();
					resolvedPath = null;

					return false;
				}

				unresolvedVariables = null;
				resolvedPath = path;

				return true;
			}
		}
	}
}
