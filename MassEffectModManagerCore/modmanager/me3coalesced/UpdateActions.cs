using System;

namespace MassEffect3.Coalesce
{
	[Flags]
	public enum UpdateActions
	{
		Add,
		Clear,
		Ignore,
		Remove,
		Update
	}
}
