using System;
using System.Collections.Generic;
using System.Text;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private static class VariablePathHelpers
		{
			internal static bool DoesFileNameContainVariable(string fileName)
			{
				Argument.IsNotNullOrEmpty(nameof(fileName), fileName);
				//Debug.Assert(!fileName.Contains(MiscHelpers.DirectorySeparator));

				string variableNameUnused, failureReasonUnused;
				int indexOutUnused;
				var result = TryGetNextVariable(0, fileName, out variableNameUnused, out indexOutUnused, out failureReasonUnused);

				// Don't allow fileName to contains "$(" even if in theory it'd be possible
				// But user shouldn't mess up with variable path containing the string "$(".
				return result != TryGetNextVariableResult.VariableNotFound;
			}

			internal static string GetParentDirectory(string path)
			{
				Argument.IsNotNull(nameof(path), path);

				if (!HasParentDirectory(path))
				{
					throw new InvalidOperationException(@"Can't get the parent dir from the pathString """ + path + @"""");
				}

				var index = path.LastIndexOf(MiscHelpers.DirectorySeparatorChar);

				//Debug.Assert(index >= 0);

				return path.Substring(0, index);
			}

			internal static string GetUnresolvedVariableFailureReason(IReadOnlyList<string> unresolvedVariables)
			{
				Argument.IsNotNull(nameof(unresolvedVariables), unresolvedVariables);

				var nbUnresolvedVariables = unresolvedVariables.Count;

				//Debug.Assert(nbUnresolvedVariables > 0);

				var sb = new StringBuilder("The following variable");

				sb.Append(unresolvedVariables.Count > 1 ? "s" : "");
				sb.Append(" cannot be resolved: ");

				for (var i = 0; i < nbUnresolvedVariables; i++)
				{
					sb.Append(PathVariableBegin);

					var unresolvedVariableName = unresolvedVariables[i];

					sb.Append(unresolvedVariableName);
					sb.Append(PathVariableEnd);

					if (i < nbUnresolvedVariables - 1)
					{
						sb.Append(" ");
					}
				}

				return sb.ToString();
			}

			internal static bool HasParentDirectory(string path)
			{
				Argument.IsNotNull(nameof(path), path);

				if (!MiscHelpers.HasParentDirectory(path))
				{
					return false;
				}

				var lastName = MiscHelpers.GetLastName(path);

				// Here, we ensure that there is no variable defined into the last fileName or directoryName!
				return !DoesFileNameContainVariable(lastName);
			}

			internal static bool IsAVariablePath(string normalizedPath, out IReadOnlyList<string> variables, out string failureMessage)
			{
				Argument.IsNotNullOrEmpty(nameof(normalizedPath), normalizedPath);
				//Debug.Assert(normalizedPath.IsNormalized());

				int indexOut;
				string variableName;
				string failureReasonTmp;

				var result = TryGetNextVariable(0, normalizedPath, out variableName, out indexOut, out failureReasonTmp);

				switch (result)
				{
					default:
						//Debug.Assert(result == TryGetNextVariableResult.VariableFound);
						//Debug.Assert(variableName != null);
						//Debug.Assert(variableName.Length > 0);

						if (indexOut - variableName.Length - PathVariableBegin.Length - PathVariableEnd.Length != 0)
						{
							failureMessage = "The variable $(" + variableName + "} must be located at the beginning of the path string.";
							variables = null;

							return false;
						}

						break;

					case TryGetNextVariableResult.VariableNotFound:
						failureMessage = @"A variable with the syntax $(variableName) must be defined at the beginning of the path string.";
						variables = null;

						return false;

					case TryGetNextVariableResult.SyntaxError:
						goto SYNTAX_ERROR_FOUND;
				}

				var variablesList = new List<string> { variableName };

				while (true)
				{
					var indexIn = indexOut;

					result = TryGetNextVariable(indexIn, normalizedPath, out variableName, out indexOut, out failureReasonTmp);

					switch (result)
					{
						default:
							//Debug.Assert(result == TryGetNextVariableResult.VariableFound);
							//Debug.Assert(variableName != null);
							//Debug.Assert(variableName.Length > 0);

							// Add the variable name only if it has not already been added (string insensitive)
							// don't use a hashset coz: 1) we don't expect plenty of variables 2) it would disturb the order of variable!
							var bAlreadyAdded = false;
							foreach (var variableNameAlreadyAdded in variablesList)
							{
								if (String.Compare(variableNameAlreadyAdded, variableName, StringComparison.OrdinalIgnoreCase) != 0)
								{
									// true for ignoreCase!
									continue;
								}

								bAlreadyAdded = true;
							}

							if (!bAlreadyAdded)
							{
								variablesList.Add(variableName);
							}

							continue; // Try find next variable!

						case TryGetNextVariableResult.VariableNotFound:

							// Ok, no more variable!
							variables = variablesList.ToReadOnlyWrappedList();
							failureMessage = null;
							return true;

						case TryGetNextVariableResult.SyntaxError:
							goto SYNTAX_ERROR_FOUND;
					}
				}

				SYNTAX_ERROR_FOUND:
				//Debug.Assert(failureReasonTmp != null);
				//Debug.Assert(failureReasonTmp.Length > 0);

				failureMessage = @"Variable syntax error : " + failureReasonTmp;
				variables = null;

				return false;
			}

			internal static string ReplaceVariableWithValue(string path, string variableName, string variableValue)
			{
				Argument.IsNotNullOrEmpty(nameof(path), path);
				Argument.IsNotNullOrEmpty(nameof(variableName), variableName);
				Argument.IsNotNull(nameof(variableValue), variableValue);

				var variableNameDecorated = PathVariableBegin + variableName + PathVariableEnd;

				string pathStringTmp1;
				var index = 0;

				TryReplaceVariableWithValue(index, path, variableNameDecorated, variableValue, out pathStringTmp1);

				//Debug.Assert(b); // At least one occurence of the variable must be found and replaced!

				while (true)
				{
					string pathStringTmp2;

					if (!TryReplaceVariableWithValue(index, pathStringTmp1, variableNameDecorated, variableValue, out pathStringTmp2))
					{
						break;
					}

					pathStringTmp1 = pathStringTmp2;

					// Do increase index with variableValue.Length.
					// Because we don't want to support recursive scenarios that can end up in infinite loop!
					// like when "$(variableName)" get replaced with "$(variableName)" or even "XYZ$(variableName)XYZ"
					index += variableValue.Length;
				}

				return pathStringTmp1;
			}

			private static TryGetNextVariableResult TryGetNextVariable(int index, string normalizedPath, out string variableName, out int indexResult,
				out string failureMessage)
			{
				Argument.IsNotNullOrEmpty(nameof(normalizedPath), normalizedPath);
				Argument.IsNotOutOfRange(nameof(index), index, 0, normalizedPath.Length - 1); // 0 <= index < normalizedPath.Length

				var indexBegin = normalizedPath.IndexOf(PathVariableBegin, index, StringComparison.Ordinal);

				if (indexBegin == -1)
				{
					indexResult = -1;
					variableName = null;
					failureMessage = null;

					return TryGetNextVariableResult.VariableNotFound;
				}

				var indexVariableNameBegin = indexBegin + PathVariableBegin.Length;
				var indexvariableNameEnd = normalizedPath.IndexOf(PathVariableEnd, indexVariableNameBegin, StringComparison.Ordinal);

				if (indexvariableNameEnd == -1)
				{
					indexResult = -1;
					variableName = null;
					failureMessage = @"Found variable opening ""$("" without closing parenthesis, at position " + indexBegin + ".";

					return TryGetNextVariableResult.SyntaxError;
				}

				//Debug.Assert(indexvariableNameEnd >= indexVariableNameBegin);

				if (indexvariableNameEnd == indexVariableNameBegin)
				{
					indexResult = -1;
					variableName = null;
					failureMessage = @"Found variable with empty name at position " + indexBegin + ".";

					return TryGetNextVariableResult.SyntaxError;
				}

				variableName = normalizedPath.Substring(indexVariableNameBegin, indexvariableNameEnd - indexVariableNameBegin);

				//Debug.Assert(variableName.Length > 0);

				// Allowed char for variableName: Letter (upper/lower) / Number / Underscore
				if (!variableName.IsValidPathVariableName())
				{
					indexResult = -1;
					failureMessage = @"Found variable with name " + PathVariableBegin + variableName + PathVariableEnd
									 + ". A variable name must contain only upper/lower case letters, digits and underscore characters.";
					variableName = null;

					return TryGetNextVariableResult.SyntaxError;
				}

				indexResult = indexvariableNameEnd + PathVariableEnd.Length;
				failureMessage = null;

				return TryGetNextVariableResult.VariableFound;
			}

			private static bool TryReplaceVariableWithValue(int index, string path, string variableName, string variableValue, out string pathResult)
			{
				Argument.IsNotNullOrEmpty(nameof(path), path);
				Argument.IsNotOutOfRange(nameof(index), index, 0, path.Length - 1); // 0 <= index < path.Length
				Argument.IsNotNullOrEmpty(nameof(variableName), variableName);
				Argument.IsNotNull(nameof(variableValue), variableValue);

				// Path variable name are case insensitive.
				var indexVariable = path.IndexOf(variableName, index, StringComparison.InvariantCultureIgnoreCase);

				if (indexVariable == -1)
				{
					pathResult = null;

					return false;
				}

				pathResult = path.Remove(indexVariable, variableName.Length);

				if (variableValue.Length > 0)
				{
					pathResult = pathResult.Insert(indexVariable, variableValue);
				}

				return true;
			}

			private enum TryGetNextVariableResult
			{
				VariableFound,
				VariableNotFound,
				SyntaxError
			}
		}
	}
}
