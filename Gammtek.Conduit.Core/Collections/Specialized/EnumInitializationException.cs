using System;

namespace Gammtek.Conduit.Collections.Specialized
{
	public class EnumInitializationException : Exception
	{
		public EnumInitializationException(string message)
			: base(message)
		{}
	}
}
