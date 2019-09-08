using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Gammtek.Conduit.Data;
using Gammtek.Conduit.Extensions.Linq;

namespace Gammtek.Conduit
{
	public partial class Argument
	{
		/// <summary>
		///     Checks whether the specified <paramref name="expression" /> value implements the specified <paramref name="interfaceType" />.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="interfaceType">The type of the interface to check for.</param>
		[DebuggerStepThrough]
		public static void ImplementsInterface<T>(Expression<Func<T>> expression, Type interfaceType)
			where T : class
		{
			var parameterInfo = expression.GetParameterInfo();

			if (parameterInfo.Value is Type)
			{
				ImplementsInterface(parameterInfo.Name, parameterInfo.Value as Type, interfaceType);
			}
			else
			{
				ImplementsInterface(parameterInfo.Name, parameterInfo.Value.GetType(), interfaceType);
			}
		}

		/// <summary>
		///     Checks whether the specified <paramref name="expression" /> value implements at least one of the specified <paramref name="interfaceTypes" />.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="interfaceTypes">The types of the interfaces to check for.</param>
		[DebuggerStepThrough]
		public static void ImplementsOneOfTheInterfaces<T>(Expression<Func<T>> expression, Type[] interfaceTypes)
			where T : class
		{
			var parameterInfo = expression.GetParameterInfo();

			if (parameterInfo.Value is Type)
			{
				ImplementsOneOfTheInterfaces(parameterInfo.Name, parameterInfo.Value as Type, interfaceTypes);
			}
			else
			{
				ImplementsOneOfTheInterfaces(parameterInfo.Name, parameterInfo.Value.GetType(), interfaceTypes);
			}
		}

		/// <summary>
		///     Determines whether the specified argument match with a given pattern.
		/// </summary>
		/// <param name="expression">The expression.</param>
		/// <param name="pattern">The pattern.</param>
		/// <param name="regexOptions">The regular expression options.</param>
		[DebuggerStepThrough]
		public static void IsMatch(Expression<Func<string>> expression, string pattern, RegexOptions regexOptions = RegexOptions.None)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsMatch(parameterInfo.Name, parameterInfo.Value, pattern, regexOptions);
		}

		/// <summary>
		///     Determines whether the specified argument has a maximum value.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="maximumValue">The maximum value.</param>
		/// <param name="validation">The validation function to call for validation.</param>
		[DebuggerStepThrough]
		public static void IsMaximum<T>(Expression<Func<T>> expression, T maximumValue, Func<T, T, bool> validation)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsMaximum(parameterInfo.Name, parameterInfo.Value, maximumValue, validation);
		}

		/// <summary>
		///     Determines whether the specified argument has a maximum value.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="maximumValue">The maximum value.</param>
		[DebuggerStepThrough]
		public static void IsMaximum<T>(Expression<Func<T>> expression, T maximumValue)
			where T : IComparable
		{
			var parameterInfo = expression.GetParameterInfo();

			IsMaximum(parameterInfo.Name, parameterInfo.Value, maximumValue);
		}

		/// <summary>
		///     Determines whether the specified argument has a minimum value.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="minimumValue">The minimum value.</param>
		/// <param name="validation">The validation function to call for validation.</param>
		[DebuggerStepThrough]
		public static void IsMinimal<T>(Expression<Func<T>> expression, T minimumValue, Func<T, T, bool> validation)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsMinimal(parameterInfo.Name, parameterInfo.Value, minimumValue, validation);
		}

		/// <summary>
		///     Determines whether the specified argument has a minimum value.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="minimumValue">The minimum value.</param>
		[DebuggerStepThrough]
		public static void IsMinimal<T>(Expression<Func<T>> expression, T minimumValue)
			where T : IComparable
		{
			var parameterInfo = expression.GetParameterInfo();

			IsMinimal(parameterInfo.Name, parameterInfo.Value, minimumValue);
		}

		/// <summary>
		///     Determines whether the specified argument is not empty.
		/// </summary>
		/// <param name="expression">The expression.</param>
		[DebuggerStepThrough]
		public static void IsNotEmpty(Expression<Func<Guid>> expression)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsNotEmpty(parameterInfo.Name, parameterInfo.Value);
		}

		/// <summary>
		///     Determines whether the specified argument doesn't match with a given pattern.
		/// </summary>
		/// <param name="expression">The expression.</param>
		/// <param name="pattern">The pattern.</param>
		/// <param name="regexOptions">The regular expression options.</param>
		[DebuggerStepThrough]
		public static void IsNotMatch(Expression<Func<string>> expression, string pattern, RegexOptions regexOptions = RegexOptions.None)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsNotMatch(parameterInfo.Name, parameterInfo.Value, pattern, regexOptions);
		}

		/// <summary>
		///     Determines whether the specified argument is not <c>null</c>.
		/// </summary>
		/// <typeparam name="T">The parameter type.</typeparam>
		/// <param name="expression">The expression.</param>
		[DebuggerStepThrough]
		public static void IsNotNull<T>(Expression<Func<T>> expression)
			where T : class
		{
			var parameterInfo = expression.GetParameterInfo();

			IsNotNull(parameterInfo.Name, parameterInfo.Value);
		}

		/// <summary>
		///     Determines whether the specified argument is not <c>null</c> or empty.
		/// </summary>
		/// <param name="expression">The expression.</param>
		[DebuggerStepThrough]
		public static void IsNotNullOrEmpty(Expression<Func<string>> expression)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsNotNullOrEmpty(parameterInfo.Name, parameterInfo.Value);
		}

		/// <summary>
		///     Determines whether the specified argument is not <c>null</c> or empty.
		/// </summary>
		/// <param name="expression">The expression.</param>
		[DebuggerStepThrough]
		public static void IsNotNullOrEmpty(Expression<Func<Guid?>> expression)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsNotNullOrEmpty(parameterInfo.Name, parameterInfo.Value);
		}

		/// <summary>
		///     Determines whether the specified argument is not <c>null</c> or an empty array (.Length == 0).
		/// </summary>
		/// <param name="expression">The expression</param>
		[DebuggerStepThrough]
		public static void IsNotNullOrEmptyArray(Expression<Func<Array>> expression)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsNotNullOrEmptyArray(parameterInfo.Name, parameterInfo.Value);
		}

		/// <summary>
		///     Determines whether the specified argument is not <c>null</c> or a whitespace.
		/// </summary>
		/// <param name="expression">The expression.</param>
		[DebuggerStepThrough]
		public static void IsNotNullOrWhitespace(Expression<Func<string>> expression)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsNotNullOrWhitespace(parameterInfo.Name, parameterInfo.Value);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="expression" /> value is not of any of the specified <paramref name="notRequiredTypes" />.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="notRequiredTypes">The types to check for.</param>
		[DebuggerStepThrough]
		public static void IsNotOfOneOfTheTypes<T>(Expression<Func<T>> expression, Type[] notRequiredTypes)
			where T : class
		{
			var parameterInfo = expression.GetParameterInfo();

			if (parameterInfo.Value is Type)
			{
				IsNotOfOneOfTheTypes(parameterInfo.Name, parameterInfo.Value as Type, notRequiredTypes);
			}
			else
			{
				IsNotOfOneOfTheTypes(parameterInfo.Name, parameterInfo.Value.GetType(), notRequiredTypes);
			}
		}

		/// <summary>
		///     Checks whether the specified <paramref name="expression" /> value is not of the specified <paramref name="notRequiredType" />.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="notRequiredType">The type to check for.</param>
		[DebuggerStepThrough]
		public static void IsNotOfType<T>(Expression<Func<T>> expression, Type notRequiredType)
			where T : class
		{
			var parameterInfo = expression.GetParameterInfo();

			if (parameterInfo.Value is Type)
			{
				IsNotOfType(parameterInfo.Name, parameterInfo.Value as Type, notRequiredType);
			}
			else
			{
				IsNotOfType(parameterInfo.Name, parameterInfo.Value.GetType(), notRequiredType);
			}
		}

		/// <summary>
		///     Determines whether the specified argument is not out of range.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="minimumValue">The minimum value.</param>
		/// <param name="maximumValue">The maximum value.</param>
		/// <param name="validation">The validation function to call for validation.</param>
		[DebuggerStepThrough]
		public static void IsNotOutOfRange<T>(Expression<Func<T>> expression, T minimumValue, T maximumValue, Func<T, T, T, bool> validation)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsNotOutOfRange(parameterInfo.Name, parameterInfo.Value, minimumValue, maximumValue, validation);
		}

		/// <summary>
		///     Determines whether the specified argument is not out of range.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="minimumValue">The minimum value.</param>
		/// <param name="maximumValue">The maximum value.</param>
		[DebuggerStepThrough]
		public static void IsNotOutOfRange<T>(Expression<Func<T>> expression, T minimumValue, T maximumValue)
			where T : IComparable
		{
			var parameterInfo = expression.GetParameterInfo();

			IsNotOutOfRange(parameterInfo.Name, parameterInfo.Value, minimumValue, maximumValue);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="expression" /> value is of at least one of the specified <paramref name="requiredTypes" />.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="expression">The expression type.</param>
		/// <param name="requiredTypes">The types to check for.</param>
		[DebuggerStepThrough]
		public static void IsOfOneOfTheTypes<T>(Expression<Func<T>> expression, Type[] requiredTypes)
			where T : class
		{
			var parameterInfo = expression.GetParameterInfo();

			if (parameterInfo.Value is Type)
			{
				IsOfOneOfTheTypes(parameterInfo.Name, parameterInfo.Value as Type, requiredTypes);
			}
			else
			{
				IsOfOneOfTheTypes(parameterInfo.Name, parameterInfo.Value.GetType(), requiredTypes);
			}
		}

		/// <summary>
		///     Checks whether the specified <paramref name="expression" /> value is of the specified <paramref name="requiredType" />.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="requiredType">The type to check for.</param>
		[DebuggerStepThrough]
		public static void IsOfType<T>(Expression<Func<T>> expression, Type requiredType)
			where T : class
		{
			var parameterInfo = expression.GetParameterInfo();

			if (parameterInfo.Value is Type)
			{
				IsOfType(parameterInfo.Name, parameterInfo.Value as Type, requiredType);
			}
			else
			{
				IsOfType(parameterInfo.Name, parameterInfo.Value.GetType(), requiredType);
			}
		}

		/// <summary>
		///     Determines whether the specified argument is valid.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="validation">The validation function.</param>
		[DebuggerStepThrough]
		public static void IsValid<T>(Expression<Func<T>> expression, Func<T, bool> validation)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsValid(parameterInfo.Name, parameterInfo.Value, validation);
		}

		/// <summary>
		///     Determines whether the specified argument is valid.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="validation">The validation function.</param>
		[DebuggerStepThrough]
		public static void IsValid<T>(Expression<Func<T>> expression, Func<bool> validation)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsValid(parameterInfo.Name, parameterInfo.Value, validation);
		}

		/// <summary>
		///     Determines whether the specified argument is valid.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="validation">The validation result.</param>
		[DebuggerStepThrough]
		public static void IsValid<T>(Expression<Func<T>> expression, bool validation)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsValid(parameterInfo.Name, parameterInfo.Value, validation);
		}

		/// <summary>
		///     Determines whether the specified argument is valid.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="validator">The validator.</param>
		[DebuggerStepThrough]
		public static void IsValid<T>(Expression<Func<T>> expression, IValueValidator<T> validator)
		{
			var parameterInfo = expression.GetParameterInfo();

			IsValid(parameterInfo.Name, parameterInfo.Value, validator);
		}

		/*/// <summary>
		///     Gets the parameter info for the expression.
		/// </summary>
		/// <typeparam name="T">The type of the parameter.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <returns>The <see cref="ParameterInfo{T}" />.</returns>
		private static ParameterInfo<T> GetParameterInfo<T>(Expression<Func<T>> expression)
		{
			IsNotNull(nameof(expression), expression);

			var parameterExpression = (MemberExpression) expression.Body;
			var parameterInfo = new ParameterInfo<T>(parameterExpression.Member.Name, expression.Compile().Invoke());

			return parameterInfo;
		}

		/// <summary>
		///     The parameter info.
		/// </summary>
		private class ParameterInfo<T>
		{
			/// <summary>
			///     Initializes a new instance of the <see cref="ParameterInfo{T}" /> class.
			/// </summary>
			/// <param name="name">The parameter name.</param>
			/// <param name="value">The parameter value.</param>
			public ParameterInfo(string name, T value)
			{
				Name = name;
				Value = value;
			}

			/// <summary>
			///     Gets the parameter name.
			/// </summary>
			public string Name { get; }

			/// <summary>
			///     Gets the parameter value.
			/// </summary>
			public T Value { get; }
		}*/
	}
}
