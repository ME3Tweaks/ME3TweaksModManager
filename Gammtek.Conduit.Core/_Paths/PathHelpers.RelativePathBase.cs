namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private abstract class RelativePathBase : PathBase, IRelativePath
		{
			protected RelativePathBase(string path)
				: base(path) {}

			public override bool IsAbsolutePath => false;

			public override bool IsEnvVarPath => false;

			public override bool IsRelativePath => true;

			public override bool IsVariablePath => false;

			public override IDirectoryPath ParentDirectoryPath => (this as IRelativePath).ParentDirectoryPath;

			public override PathType PathType => PathType.Relative;

			IRelativeDirectoryPath IRelativePath.ParentDirectoryPath => MiscHelpers.GetParentDirectory(CurrentPath).ToRelativeDirectoryPath();

			public bool CanGetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				Argument.IsNotNull(nameof(path), path);

				string absolutePath, failureMessage;

				return AbsoluteRelativePathHelpers.TryGetAbsolutePathFrom(path, this, out absolutePath, out failureMessage);
			}

			public bool CanGetAbsolutePathFrom(IAbsoluteDirectoryPath path, out string failureMessage)
			{
				Argument.IsNotNull(nameof(path), path);

				string absolutePath;

				return AbsoluteRelativePathHelpers.TryGetAbsolutePathFrom(path, this, out absolutePath, out failureMessage);
			}
			
			public abstract IAbsolutePath GetAbsolutePathFrom(IAbsoluteDirectoryPath path);
		}
	}
}
