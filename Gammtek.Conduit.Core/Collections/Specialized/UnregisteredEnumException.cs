using System;

namespace Gammtek.Conduit.Collections.Specialized
{
	public class UnregisteredEnumException : Exception
	{
		public UnregisteredEnumException(string message)
			: base(message)
		{}
	}
}
