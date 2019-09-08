using System;

namespace Gammtek.Conduit
{
	public static class ThrowHelper
	{
		[ContractAnnotation("=> halt")]
		public static void ThrowArgumentNullException([InvokerParameterName] string paramName)
		{
			throw new ArgumentNullException(paramName);
		}

		[ContractAnnotation("=> halt")]
		public static void ThrowArgumentOutOfRangeException([InvokerParameterName] string paramName)
		{
			throw new ArgumentOutOfRangeException(paramName);
		}

		[ContractAnnotation("=> halt")]
		public static void ThrowDivideByZeroException()
		{
			throw new DivideByZeroException();
		}

		[ContractAnnotation("obj:null => halt")]
		public static void ThrowExceptionIfNull([InvokerParameterName] string paramName, object obj)
		{
			if (obj == null)
			{
				throw new ArgumentNullException(paramName);
			}
		}
	}
}
