using System;

namespace Gammtek.Conduit.Windows
{
	[Flags]
	public enum AutoComplete : uint
	{
		Default = 0U,
		Filesystem = 1U,
		UrlHistory = 2U,
		UrlMru = 4U,
		UrlAll = UrlMru | UrlHistory,
		UseTab = 8U,
		FileSystemOnly = 16U,
		FileSystemDirectories = 32U,
		AutoSuggestForceOn = 268435456U,
		AutoSuggestForceOff = 536870912U,
		AutoAppendForceOn = 1073741824U,
		AutoAppendForceOff = 2147483648U
	}
}