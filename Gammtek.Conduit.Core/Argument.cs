using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Gammtek.Conduit.Data;
using Gammtek.Conduit.Extensions.Reflection;

namespace Gammtek.Conduit
{
	public partial class Argument
	{
		/*/// <summary>
		/// The <see cref="ILog">log</see> object.
		/// </summary>
		private static ILog Log { get; } = LogManager.GetCurrentClassLogger();*/

		/// <summary>
		///     Checks whether the specified <paramref name="instance" /> implements the specified <paramref name="interfaceType" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="instance">The instance to check.</param>
		/// <param name="interfaceType">The type of the interface to check for.</param>
		[DebuggerStepThrough]
		public static void ImplementsInterface([InvokerParameterName] string paramName, object instance, Type interfaceType)
		{
			IsNotNull(nameof(instance), instance);

			ImplementsInterface(paramName, instance.GetType(), interfaceType);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="instance" /> implements the specified <typeparamref name="TInterface" />.
		/// </summary>
		/// <typeparam name="TInterface">The type of the T interface.</typeparam>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="instance">The instance to check.</param>
		[DebuggerStepThrough]
		public static void ImplementsInterface<TInterface>([InvokerParameterName] string paramName, object instance)
			where TInterface : class
		{
			var interfaceType = typeof (TInterface);

			ImplementsInterface(paramName, instance, interfaceType);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="type" /> implements the specified <paramref name="interfaceType" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="type">The type to check.</param>
		/// <param name="interfaceType">The type of the interface to check for.</param>
		[DebuggerStepThrough]
		public static void ImplementsInterface([InvokerParameterName] string paramName, Type type, Type interfaceType)
		{
			IsNotNull(nameof(type), type);
			IsNotNull(nameof(interfaceType), interfaceType);

			if (type.GetInterfacesEx().Any(iType => iType == interfaceType))
			{
				return;
			}

			var error = $"Type '{type.Name}' should implement interface '{interfaceType.Name}', but does not";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="instance" /> implements at least one of the specified <paramref name="interfaceTypes" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="instance">The instance to check.</param>
		/// <param name="interfaceTypes">The types of the interfaces to check for.</param>
		[DebuggerStepThrough]
		public static void ImplementsOneOfTheInterfaces([InvokerParameterName] string paramName, object instance, Type[] interfaceTypes)
		{
			IsNotNull(nameof(instance), instance);

			ImplementsOneOfTheInterfaces(paramName, instance.GetType(), interfaceTypes);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="type" /> implements at least one of the the specified <paramref name="interfaceTypes" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="type">The type to check.</param>
		/// <param name="interfaceTypes">The types of the interfaces to check for.</param>
		[DebuggerStepThrough]
		public static void ImplementsOneOfTheInterfaces([InvokerParameterName] string paramName, Type type, Type[] interfaceTypes)
		{
			IsNotNull(nameof(type), type);
			IsNotNullOrEmptyArray(nameof(interfaceTypes), interfaceTypes);

			if (interfaceTypes.Any(interfaceType => type.GetInterfacesEx().Any(iType => iType == interfaceType)))
			{
				return;
			}

			var errorBuilder = new StringBuilder();

			//errorBuilder.AppendLine("Type '{0}' should implement at least one of the following interfaces, but does not:");
			errorBuilder.AppendLine($"Type '{type.Name}' should implement at least one of the following interfaces, but does not:");

			foreach (var interfaceType in interfaceTypes)
			{
				errorBuilder.AppendLine($"  * {interfaceType.FullName}");
			}

			var error = errorBuilder.ToString();

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="type" /> inherits from the <paramref name="baseType" />.
		/// </summary>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="type">The type.</param>
		/// <param name="baseType">The base type.</param>
		[DebuggerStepThrough]
		public static void InheritsFrom([InvokerParameterName] string paramName, Type type, Type baseType)
		{
			IsNotNull(nameof(type), type);
			IsNotNull(nameof(baseType), baseType);

			var runtimeBaseType = type.GetBaseTypeEx();

			do
			{
				if (runtimeBaseType == baseType)
				{
					return;
				}

				// Prevent some endless while loops
				if (runtimeBaseType == typeof (object))
				{
					// Break, no return because this should cause an exception
					break;
				}

				runtimeBaseType = type.GetBaseTypeEx();
			} while (runtimeBaseType != null);

			var error = $"Type '{type.Name}' should have type '{baseType.Name}' as base class, but does not";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="instance" /> inherits from the <paramref name="baseType" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="instance">The instance.</param>
		/// <param name="baseType">The base type.</param>
		[DebuggerStepThrough]
		public static void InheritsFrom([InvokerParameterName] string paramName, object instance, Type baseType)
		{
			IsNotNull(nameof(instance), instance);

			InheritsFrom(paramName, instance.GetType(), baseType);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="instance" /> inherits from the specified <typeparamref name="TBase" />.
		/// </summary>
		/// <typeparam name="TBase">The base type.</typeparam>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="instance">The instance.</param>
		[DebuggerStepThrough]
		public static void InheritsFrom<TBase>([InvokerParameterName] string paramName, object instance)
			where TBase : class
		{
			var baseType = typeof (TBase);

			InheritsFrom(paramName, instance, baseType);
		}

		/// <summary>
		///     Determines whether the specified argument match with a given pattern.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="paramValue">The param value.</param>
		/// <param name="pattern">The pattern.</param>
		/// <param name="regexOptions">The regular expression options.</param>
		[DebuggerStepThrough]
		public static void IsMatch([InvokerParameterName] string paramName, string paramValue, string pattern, RegexOptions regexOptions = RegexOptions.None)
		{
			IsNotNull(nameof(paramValue), paramValue);
			IsNotNull(nameof(pattern), pattern);

			if (Regex.IsMatch(paramValue, pattern, regexOptions))
			{
				return;
			}

			var error = $"Argument '{paramName}' doesn't match with pattern '{pattern}'";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Determines whether the specified argument has a maximum value.
		/// </summary>
		/// <typeparam name="T">Type of the argument.</typeparam>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		/// <param name="maximumValue">The maximum value.</param>
		/// <param name="validation">The function to call for validation.</param>
		[DebuggerStepThrough]
		public static void IsMaximum<T>([InvokerParameterName] string paramName, T paramValue, T maximumValue, Func<T, T, bool> validation)
		{
			if (validation(paramValue, maximumValue))
			{
				return;
			}

			//var error = $"Argument '{ObjectToStringHelper.ToString(paramName)}' should be at maximum {maximumValue}";
			var error = $"Argument '{paramName}' should be at maximum {maximumValue}";

			//Log.Error(error);
			throw new ArgumentOutOfRangeException(paramName, error);
		}

		/// <summary>
		///     Determines whether the specified argument has a maximum value.
		/// </summary>
		/// <typeparam name="T">Type of the argument.</typeparam>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		/// <param name="maximumValue">The maximum value.</param>
		[DebuggerStepThrough]
		public static void IsMaximum<T>([InvokerParameterName] string paramName, T paramValue, T maximumValue)
			where T : IComparable
		{
			IsMaximum(paramName, paramValue, maximumValue,
				(innerParamValue, innerMaximumValue) => innerParamValue.CompareTo(innerMaximumValue) <= 0);
		}

		/// <summary>
		///     Determines whether the specified argument has a minimum value.
		/// </summary>
		/// <typeparam name="T">Type of the argument.</typeparam>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		/// <param name="minimumValue">The minimum value.</param>
		/// <param name="validation">The function to call for validation.</param>
		[DebuggerStepThrough]
		public static void IsMinimal<T>([InvokerParameterName] string paramName, T paramValue, T minimumValue, Func<T, T, bool> validation)
		{
			IsNotNull(nameof(validation), validation);

			if (validation(paramValue, minimumValue))
			{
				return;
			}

			//var error = $"Argument '{ObjectToStringHelper.ToString(paramName)}' should be minimal {minimumValue}";
			var error = $"Argument '{paramName}' should be minimal {minimumValue}";

			//Log.Error(error);
			throw new ArgumentOutOfRangeException(paramName, error);
		}

		/// <summary>
		///     Determines whether the specified argument has a minimum value.
		/// </summary>
		/// <typeparam name="T">Type of the argument.</typeparam>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		/// <param name="minimumValue">The minimum value.</param>
		[DebuggerStepThrough]
		public static void IsMinimal<T>([InvokerParameterName] string paramName, T paramValue, T minimumValue)
			where T : IComparable
		{
			IsMinimal(paramName, paramValue, minimumValue,
				(innerParamValue, innerMinimumValue) => innerParamValue.CompareTo(innerMinimumValue) >= 0);
		}

		/// <summary>
		///     Determines whether the specified argument is not empty.
		/// </summary>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		[DebuggerStepThrough]
		public static void IsNotEmpty([InvokerParameterName] string paramName, Guid paramValue)
		{
			if (paramValue != Guid.Empty)
			{
				return;
			}

			//var error = $"Argument '{ObjectToStringHelper.ToString(paramName)}' cannot be Guid.Empty";
			var error = $"Argument '{paramName}' cannot be Guid.Empty";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Determines whether the specified argument doesn't match with a given pattern.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="paramValue">The para value.</param>
		/// <param name="pattern">The pattern.</param>
		/// <param name="regexOptions">The regular expression options.</param>
		[DebuggerStepThrough]
		public static void IsNotMatch([InvokerParameterName] string paramName, string paramValue, string pattern,
			RegexOptions regexOptions = RegexOptions.None)
		{
			IsNotNull(nameof(paramValue), paramValue);
			IsNotNull(nameof(pattern), pattern);

			if (!Regex.IsMatch(paramValue, pattern, regexOptions))
			{
				return;
			}

			var error = $"Argument '{paramName}' matches with pattern '{pattern}'";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Determines whether the specified argument is not <c>null</c>.
		/// </summary>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		[DebuggerStepThrough]
		[ContractAnnotation("paramValue:null => halt")]
		public static void IsNotNull([InvokerParameterName] string paramName, object paramValue)
		{
			if (paramValue != null)
			{
				return;
			}

			//var error = $"Argument '{ObjectToStringHelper.ToString(paramName)}' cannot be null";
			var error = $"Argument '{paramName}' cannot be null";

			//Log.Error(error);
			throw new ArgumentNullException(paramName, error);
		}

		/// <summary>
		///     Determines whether the specified argument is not <c>null</c> or empty.
		/// </summary>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		[DebuggerStepThrough]
		[ContractAnnotation("paramValue:null => halt")]
		public static void IsNotNullOrEmpty([InvokerParameterName] string paramName, string paramValue)
		{
			if (!string.IsNullOrEmpty(paramValue))
			{
				return;
			}

			//var error = $"Argument '{ObjectToStringHelper.ToString(paramName)}' cannot be null or empty";
			var error = $"Argument '{paramName}' cannot be null or empty";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Determines whether the specified argument is not <c>null</c> or empty.
		/// </summary>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		[DebuggerStepThrough]
		[ContractAnnotation("paramValue:null => halt")]
		public static void IsNotNullOrEmpty([InvokerParameterName] string paramName, Guid? paramValue)
		{
			if (paramValue.HasValue && paramValue.Value != Guid.Empty)
			{
				return;
			}

			//var error = $"Argument '{ObjectToStringHelper.ToString(paramName)}' cannot be null or Guid.Empty";
			var error = $"Argument '{paramName}' cannot be null or Guid.Empty";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Determines whether the specified argument is not <c>null</c> or an empty array (.Length == 0).
		/// </summary>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		[DebuggerStepThrough]
		[ContractAnnotation("paramValue:null => halt")]
		public static void IsNotNullOrEmptyArray([InvokerParameterName] string paramName, Array paramValue)
		{
			if ((paramValue != null) && (paramValue.Length != 0))
			{
				return;
			}

			//var error = $"Argument '{ObjectToStringHelper.ToString(paramName)}' cannot be null or an empty array";
			var error = $"Argument '{paramName}' cannot be null or an empty array";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Determines whether the specified argument is not <c>null</c> or a whitespace.
		/// </summary>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		[DebuggerStepThrough]
		[ContractAnnotation("paramValue:null => halt")]
		public static void IsNotNullOrWhitespace([InvokerParameterName] string paramName, string paramValue)
		{
			if (string.IsNullOrEmpty(paramValue) || (string.CompareOrdinal(paramValue.Trim(), string.Empty) == 0))
			{
				//var error = $"Argument '{ObjectToStringHelper.ToString(paramName)}' cannot be null or whitespace";
				var error = $"Argument '{paramName}' cannot be null or whitespace";

				//Log.Error(error);
				throw new ArgumentException(error, paramName);
			}
		}

		/// <summary>
		///     Checks whether the specified <paramref name="instance" /> is not of any of the specified <paramref name="notRequiredTypes" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="instance">The instance to check.</param>
		/// <param name="notRequiredTypes">The types to check for.</param>
		[DebuggerStepThrough]
		public static void IsNotOfOneOfTheTypes([InvokerParameterName] string paramName, object instance, Type[] notRequiredTypes)
		{
			IsNotNull(nameof(instance), instance);

			IsNotOfOneOfTheTypes(paramName, instance.GetType(), notRequiredTypes);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="type" /> is not of any of the specified <paramref name="notRequiredTypes" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="type">The type to check.</param>
		/// <param name="notRequiredTypes">The types to check for.</param>
		[DebuggerStepThrough]
		public static void IsNotOfOneOfTheTypes([InvokerParameterName] string paramName, Type type, Type[] notRequiredTypes)
		{
			IsNotNull(nameof(type), type);
			IsNotNullOrEmptyArray(nameof(notRequiredTypes), notRequiredTypes);

			if (type.IsCOMObjectEx())
			{
				return;
			}

			foreach (var error in from notRequiredType in notRequiredTypes
				where notRequiredType.IsAssignableFromEx(type)
				select $"Type '{type.Name}' should not be of type '{notRequiredType.Name}', but is")
			{
				//Log.Error(error);
				throw new ArgumentException(error, paramName);
			}
		}

		/// <summary>
		///     Checks whether the specified <paramref name="instance" /> is not of the specified <paramref name="notRequiredType" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="instance">The instance to check.</param>
		/// <param name="notRequiredType">The type to check for.</param>
		[DebuggerStepThrough]
		public static void IsNotOfType([InvokerParameterName] string paramName, object instance, Type notRequiredType)
		{
			IsNotNull(nameof(instance), instance);

			IsNotOfType(paramName, instance.GetType(), notRequiredType);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="type" /> is not of the specified <paramref name="notRequiredType" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="type">The type to check.</param>
		/// <param name="notRequiredType">The type to check for.</param>
		[DebuggerStepThrough]
		public static void IsNotOfType([InvokerParameterName] string paramName, Type type, Type notRequiredType)
		{
			IsNotNull(nameof(type), type);
			IsNotNull(nameof(notRequiredType), notRequiredType);

			if (type.IsCOMObjectEx())
			{
				return;
			}

			if (!notRequiredType.IsAssignableFromEx(type))
			{
				return;
			}

			var error = $"Type '{type.Name}' should not be of type '{notRequiredType.Name}', but is";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Determines whether the specified argument is not out of range.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		/// <param name="minimumValue">The minimum value.</param>
		/// <param name="maximumValue">The maximum value.</param>
		/// <param name="validation">The function to call for validation.</param>
		[DebuggerStepThrough]
		public static void IsNotOutOfRange<T>([InvokerParameterName] string paramName, T paramValue, T minimumValue, T maximumValue,
			Func<T, T, T, bool> validation)
		{
			IsNotNull(nameof(validation), validation);

			if (validation(paramValue, minimumValue, maximumValue))
			{
				return;
			}

			//var error = $"Argument '{ObjectToStringHelper.ToString(paramName)}' should be between {minimumValue} and {maximumValue}";
			var error = $"Argument '{paramName}' should be between {minimumValue} and {maximumValue}";

			//Log.Error(error);
			throw new ArgumentOutOfRangeException(paramName, error);
		}

		/// <summary>
		///     Determines whether the specified argument is not out of range.
		/// </summary>
		/// <typeparam name="T">Type of the argument.</typeparam>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">Value of the parameter.</param>
		/// <param name="minimumValue">The minimum value.</param>
		/// <param name="maximumValue">The maximum value.</param>
		[DebuggerStepThrough]
		public static void IsNotOutOfRange<T>([InvokerParameterName] string paramName, T paramValue, T minimumValue, T maximumValue)
			where T : IComparable
		{
			IsNotOutOfRange(paramName, paramValue, minimumValue, maximumValue,
				(innerParamValue, innerMinimumValue, innerMaximumValue) =>
					innerParamValue.CompareTo(innerMinimumValue) >= 0 && innerParamValue.CompareTo(innerMaximumValue) <= 0);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="instance" /> is of at least one of the specified <paramref name="requiredTypes" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="instance">The instance to check.</param>
		/// <param name="requiredTypes">The types to check for.</param>
		[DebuggerStepThrough]
		public static void IsOfOneOfTheTypes([InvokerParameterName] string paramName, object instance, Type[] requiredTypes)
		{
			IsNotNull(nameof(instance), instance);

			IsOfOneOfTheTypes(paramName, instance.GetType(), requiredTypes);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="type" /> is of at least one of the specified <paramref name="requiredTypes" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="type">The type to check.</param>
		/// <param name="requiredTypes">The types to check for.</param>
		[DebuggerStepThrough]
		public static void IsOfOneOfTheTypes([InvokerParameterName] string paramName, Type type, Type[] requiredTypes)
		{
			IsNotNull(nameof(type), type);
			IsNotNullOrEmptyArray(nameof(requiredTypes), requiredTypes);

			if (type.IsCOMObjectEx())
			{
				return;
			}

			if (requiredTypes.Any(requiredType => requiredType.IsAssignableFromEx(type)))
			{
				return;
			}

			var errorBuilder = new StringBuilder();

			//errorBuilder.AppendLine("Type '{0}' should implement at least one of the following types, but does not:");
			errorBuilder.AppendLine($"Type '{type.Name}' should implement at least one of the following types, but does not:");

			foreach (var requiredType in requiredTypes)
			{
				errorBuilder.AppendLine($"  * {requiredType.FullName}");
			}

			var error = errorBuilder.ToString();

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="instance" /> is of the specified <paramref name="requiredType" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="instance">The instance to check.</param>
		/// <param name="requiredType">The type to check for.</param>
		[DebuggerStepThrough]
		public static void IsOfType([InvokerParameterName] string paramName, object instance, Type requiredType)
		{
			IsNotNull(nameof(instance), instance);

			IsOfType(paramName, instance.GetType(), requiredType);
		}

		/// <summary>
		///     Checks whether the specified <paramref name="type" /> is of the specified <paramref name="requiredType" />.
		/// </summary>
		/// <param name="paramName">Name of the param.</param>
		/// <param name="type">The type to check.</param>
		/// <param name="requiredType">The type to check for.</param>
		[DebuggerStepThrough]
		public static void IsOfType([InvokerParameterName] string paramName, Type type, Type requiredType)
		{
			IsNotNull(nameof(type), type);
			IsNotNull(nameof(requiredType), requiredType);

			if (type.IsCOMObjectEx())
			{
				return;
			}

			if (requiredType.IsAssignableFromEx(type))
			{
				return;
			}

			var error = $"Type '{type.Name}' should be of type '{requiredType.Name}', but is not";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}

		/// <summary>
		///     Checks whether the passed in boolean check is <c>true</c>. If not, this method will throw a <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="isSupported">if set to <c>true</c>, the action is supported; otherwise <c>false</c>.</param>
		/// <param name="errorFormat">The error format.</param>
		/// <param name="args">The arguments for the string format.</param>
		[DebuggerStepThrough]
		public static void IsSupported(bool isSupported, string errorFormat, params object[] args)
		{
			IsNotNullOrWhitespace(nameof(errorFormat), errorFormat);

			if (isSupported)
			{
				return;
			}

			var error = string.Format(errorFormat, args);

			//Log.Error(error);
			throw new NotSupportedException(error);
		}

		/// <summary>
		///     Determines whether the specified argument is valid.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">The parameter value.</param>
		/// <param name="validation">The function to call for validation.</param>
		[DebuggerStepThrough]
		public static void IsValid<T>([InvokerParameterName] string paramName, T paramValue, Func<bool> validation)
		{
			IsNotNull(nameof(validation), validation);

			IsValid(paramName, paramValue, validation.Invoke());
		}

		/// <summary>
		///     Determines whether the specified argument is valid.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">The parameter value.</param>
		/// <param name="validation">The function to call for validation.</param>
		[DebuggerStepThrough]
		public static void IsValid<T>([InvokerParameterName] string paramName, T paramValue, Func<T, bool> validation)
		{
			IsNotNull(nameof(validation), validation);

			IsValid(paramName, paramValue, validation.Invoke(paramValue));
		}

		/// <summary>
		///     Determines whether the specified argument is valid.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">The parameter value.</param>
		/// <param name="validator">The validator.</param>
		[DebuggerStepThrough]
		public static void IsValid<T>([InvokerParameterName] string paramName, T paramValue, IValueValidator<T> validator)
		{
			IsNotNull(nameof(validator), validator);

			IsValid(paramName, paramValue, validator.IsValid(paramValue));
		}

		/// <summary>
		///     Determines whether the specified argument is valid.
		/// </summary>
		/// <typeparam name="T">The value type.</typeparam>
		/// <param name="paramName">Name of the parameter.</param>
		/// <param name="paramValue">The parameter value.</param>
		/// <param name="validation">The validation function result.</param>
		[DebuggerStepThrough]
		public static void IsValid<T>([InvokerParameterName] string paramName, T paramValue, bool validation)
		{
			IsNotNull(nameof(paramValue), paramValue);

			if (validation)
			{
				return;
			}

			//var error = $"Argument '{ObjectToStringHelper.ToString(paramName)}' is not valid";
			var error = $"Argument '{paramName}' is not valid";

			//Log.Error(error);
			throw new ArgumentException(error, paramName);
		}
	}
}
