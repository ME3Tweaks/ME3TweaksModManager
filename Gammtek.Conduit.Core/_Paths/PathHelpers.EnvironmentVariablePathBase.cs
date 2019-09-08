using System;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private abstract class EnvironmentVariablePathBase : PathBase, IEnvironmentVariablePath
		{
			protected EnvironmentVariablePathBase(string path)
				: base(path)
			{
				EnvironmentVariable = ComputeEnvironmentVariable();

				//Debug.Assert(EnvVar != null);
				//Debug.Assert(EnvVar.Length >= 3);
				//Debug.Assert(EnvVar[0] == MiscHelpers.ENVVAR_PERCENT);
				//Debug.Assert(EnvVar[EnvVar.Length - 1] == MiscHelpers.ENVVAR_PERCENT);
			}

			public string EnvironmentVariable { get; }

			public override bool IsAbsolutePath => false;

			public override bool IsEnvVarPath => true;

			public override bool IsRelativePath => false;

			public override bool IsVariablePath => false;

			public override IDirectoryPath ParentDirectoryPath => (this as IEnvironmentVariablePath).ParentDirectoryPath;

			public override PathType PathType => PathType.EnvVar;
			
			IEnvironmentVariableDirectoryPath IEnvironmentVariablePath.ParentDirectoryPath => MiscHelpers.GetParentDirectory(CurrentPath).ToEnvVarDirectoryPath();
			
			public abstract EnvironmentVariableResolvingStatus TryResolve(out IAbsolutePath resolvedPath);

			public abstract bool TryResolve(out IAbsolutePath resolvedPath, out string failureMessage);

			protected string GetErrorEnvVarResolvedButCannotConvertToAbsolutePathFailureReason()
			{
				string envVarValue;

				TryExpandEnvironmentVariables(out envVarValue);

				//Debug.Assert(b); // If the error EnvVarResolvedButCanConvertToAbsolutePath occurs

				// it means envVar can be resolved!
				return "The environment variable " + EnvironmentVariable + " is resolved into the value {" + envVarValue
					   + "} but this value cannot be the prefix of an absolute path.";
			}

			protected string GetErrorUnresolvedEnvVarFailureReason()
			{
				return "Can't resolve the environment variable " + EnvironmentVariable + ".";
			}
			
			protected bool TryResolveEnvironmentVariable(out string resolvedPath)
			{
				string envVarValue;

				if (!TryExpandEnvironmentVariables(out envVarValue))
				{
					resolvedPath = null;

					return false;
				}

				//Debug.Assert(envVarValue != null);
				//Debug.Assert(envVarValue.Length > 0);

				var envVarWith2PercentsLength = EnvironmentVariable.Length;
				
				//Debug.Assert(CurrentPath.Length >= envVarWith2PercentsLength);

				var pathWithoutEnvVar = CurrentPath.Substring(envVarWith2PercentsLength, CurrentPath.Length - envVarWith2PercentsLength);

				resolvedPath = envVarValue + pathWithoutEnvVar;

				return true;
			}

			private string ComputeEnvironmentVariable()
			{
				//Debug.Assert(MiscHelpers.IsAnEnvVarPath(CurrentPath));
				//Debug.Assert(CurrentPath[0] == MiscHelpers.ENVVAR_PERCENT);

				var indexClose = CurrentPath.IndexOf(MiscHelpers.EnvironmentVariablePercent, 1);

				//Debug.Assert(indexClose > 1);

				return CurrentPath.Substring(0, indexClose + 1);
			}

			private bool TryExpandEnvironmentVariables(out string variableValue)
			{
				variableValue = Environment.ExpandEnvironmentVariables(EnvironmentVariable);

				return string.IsNullOrEmpty(variableValue)
					&& variableValue != EnvironmentVariable;
			}
		}
	}
}
