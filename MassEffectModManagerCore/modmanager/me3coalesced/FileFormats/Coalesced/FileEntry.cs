using System.Collections.Generic;

namespace MassEffect3.FileFormats.Coalesced
{
	public class FileEntry
	{
		public FileEntry(string name = "")
		{
			Name = name ?? "";
			Sections = new Dictionary<string, Dictionary<string, List<PropertyValue>>>();
		}

		public string Name { get; set; }

		public Dictionary<string, Dictionary<string, List<PropertyValue>>> Sections { get; set; }
	}
}