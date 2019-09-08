using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Gammtek.Conduit.Extensions.Linq
{
	/// <summary>
	///     Extension for <see cref="Expression" />.
	/// </summary>
	public static class ExpressionExtensions
	{
		/// <summary>
		///     Converts an expression into a <see cref="MemberInfo" />.
		/// </summary>
		/// <param name="expression">The expression to convert.</param>
		/// <returns>The member info.</returns>
		public static MemberInfo GetMemberInfo(this Expression expression)
		{
			var lambda = (LambdaExpression) expression;

			MemberExpression memberExpression;

			var body = lambda.Body as UnaryExpression;

			if (body != null)
			{
				var unaryExpression = body;
				memberExpression = (MemberExpression) unaryExpression.Operand;
			}
			else
			{
				memberExpression = (MemberExpression) lambda.Body;
			}

			return memberExpression.Member;
		}

		/// <summary>
		///     Gets the parameter info for the expression.
		/// </summary>
		/// <typeparam name="T">The type of the parameter.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <returns>The <see cref="ParameterInfo{T}" />.</returns>
		public static ParameterInfo<T> GetParameterInfo<T>(this Expression<Func<T>> expression)
		{
			Argument.IsNotNull(nameof(expression), expression);

			var parameterExpression = (MemberExpression) expression.Body;
			var parameterInfo = new ParameterInfo<T>(parameterExpression.Member.Name, expression.Compile().Invoke());

			return parameterInfo;
		}

		/// <summary>
		///     The parameter info.
		/// </summary>
		public class ParameterInfo<T>
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
		}
	}
}
