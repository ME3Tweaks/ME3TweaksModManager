using System.Collections.Generic;

namespace MassEffect3.Coalesce
{
	public abstract class CoalesceFile
	{

		protected CoalesceFile(string source = "", string name = "", string id = "",
			IList<CoalesceAsset> assets = null, CoalesceSettings settings = null)
		{
			Assets = assets ?? new List<CoalesceAsset>();
			Id = id ?? "";
			Name = name ?? "";
			Settings = settings ?? new CoalesceSettings();
			Source = source ?? "";
		}

		public IList<CoalesceAsset> Assets { get; set; }

		public string Id { get; set; }

		public string Name { get; set; }

		public CoalesceSettings Settings { get; set; }

		public string Source { get; set; }
	}
}
