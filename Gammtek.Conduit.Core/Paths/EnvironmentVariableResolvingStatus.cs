namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Defines the result of the <see cref="IEnvironmentVariablePath" />.
	///     <see cref="IEnvironmentVariablePath.TryResolve(out IAbsolutePath)" /> method.
	/// </summary>
	public enum EnvironmentVariableResolvingStatus
	{
		/// <summary>
		///     The environment variable has been resolved, and the resulting path is a valid absolute path.
		/// </summary>
		Success,

		/// <summary>
		///     The environment variable cannot be resolved.
		/// </summary>
		UnresolvedEnvironmentVariable,

		/// <summary>
		///     The environment variable has been resolved but the resulting path is not a valid absolute path.
		/// </summary>
		CannotConvertToAbsolutePath
	}
}
