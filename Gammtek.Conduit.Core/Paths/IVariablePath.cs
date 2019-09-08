using System.Collections.Generic;

namespace Gammtek.Conduit.Paths
{
	/// <summary>
	///     Represents a path containing variable(s) defined with the syntax (case-insensitive) <i>$(VariableName)</i>. Such path must be prefixed with a
	///     variable and can then contain more variables.
	/// </summary>
	public interface IVariablePath : IPath
	{
		/// <summary>
		///     Gets all variables contained in this path, ordered from first one (the prefix variable) to the last one.
		/// </summary>
		/// <remarks>
		///     For example, for the path  <i>$(Variable1Name)\$(Variable2Name)</i> this property getter returns <i>["Variable1Name","Variable2Name"]</i>.
		/// </remarks>
		IReadOnlyList<string> AllVariables { get; }

		/// <summary>
		///     Returns a new path containing variables, representing the parent directory of this path containing variables.
		/// </summary>
		/// <exception cref="System.InvalidOperationException">This path containing variables has no parent directory.</exception>
		new IVariableDirectoryPath ParentDirectoryPath { get; }

		/// <summary>
		///     Gets the prefix variable name of this path.
		/// </summary>
		/// <remarks>
		///     For example, for the path  <i>$(VariableName)\Dir</i> this property getter returns <i>"VariableName"</i>.
		/// </remarks>
		string PrefixVariable { get; }

		/// <summary>
		///     Returns <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.Success" /> if <see cref="AllVariables" /> of this path
		///     can be resolved from <paramref name="variables" /> cref="variablesValues"/> and the path can be resolved into a drive letter or a UNC
		///     absolute path.
		/// </summary>
		/// <param name="variables">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedPath">
		///     It is the absolute path resolved obtained if this method returns <see cref="VariablePathResolvingStatus" />.
		///     <see cref="VariablePathResolvingStatus.Success" />.
		/// </param>
		VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath);

		/// <summary>
		///     Returns <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.Success" /> if <see cref="AllVariables" /> of this path
		///     can be resolved from <paramref name="variables" /> and the path can be resolved into a drive letter or a UNC absolute path.
		/// </summary>
		/// <param name="variables">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedPath">
		///     It is the absolute path resolved obtained if this method returns <see cref="VariablePathResolvingStatus" />.
		///     <see cref="VariablePathResolvingStatus.Success" />.
		/// </param>
		/// <param name="unresolvedVariables">
		///     This list contains one or several variables names unresolved, if this method returns
		///     <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.UnresolvedVariable" />.
		/// </param>
		VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath,
			out IReadOnlyList<string> unresolvedVariables);

		/// <summary>
		///     Returns <i>true</i> if <see cref="AllVariables" /> of this path can be resolved from <paramref name="variables" /> and the path can be
		///     resolved into a drive letter or a UNC absolute path.
		/// </summary>
		/// <param name="variables">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedPath">It is the absolute path resolved obtained if this method returns <i>true</i>.</param>
		/// <param name="failureMessage">If <i>false</i> is returned, <paramref name="failureMessage" /> contains the plain english description of the failure.</param>
		bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath, out string failureMessage);
	}
}
