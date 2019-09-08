namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Defines a path mode, absolute, relative or prefixed with an environment variable.
	/// </summary>
	/// <remarks>
	///     Since the a PathMode value can be variable, this enumeration can favor a generic way of coding in certain situations, by replacing calls to
	///     getters like <see cref="P:NDepend.Path.IPath.IsAbsolutePath" />, <see cref="P:NDepend.Path.IPath.IsRelativePath" /> or
	///     <see cref="P:NDepend.Path.IPath.IsEnvVarPath" /> by calls to <see cref="IPath.PathType" />.
	/// </remarks>
	public enum PathType
	{
		/// <summary>
		///     Represents a absolute path.
		/// </summary>
		Absolute = 0,

		/// <summary>
		///     Represents a relative path.
		/// </summary>
		Relative = 1,

		/// <summary>
		///     Represents a path prefixed with an environment variable.
		/// </summary>
		EnvVar = 2,

		/// <summary>
		///     Represents a path that contains variable(s).
		/// </summary>
		Variable = 3
	}
}
