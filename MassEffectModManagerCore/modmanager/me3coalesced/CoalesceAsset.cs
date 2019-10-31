using System;

namespace MassEffect3.Coalesce
{
	public class CoalesceAsset
	{
		public CoalesceAsset(string name = null, CoalesceSections sections = null)
		{
			Name = name ?? "";
			Sections = sections ?? new CoalesceSections();
		}

		public string Id { get; set; }

		public string Name { get; set; }

		public CoalesceSections Sections { get; set; }

		public string Source { get; set; }

		public bool CompareId(CoalesceAsset asset, bool ignoreCase = false)
		{
			if (asset == null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			return CompareId(asset.Id, ignoreCase);
		}

		public bool CompareId(string id, bool ignoreCase = false)
		{
			if (!HasId || string.IsNullOrEmpty(id))
			{
				return false;
			}

			if (ignoreCase && Id.Equals(id, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return !ignoreCase && Id.Equals(id);
		}

		public bool CompareName(CoalesceAsset asset, bool ignoreCase = false)
		{
			if (asset == null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			return CompareName(asset.Name, ignoreCase);
		}

		public bool CompareName(string name, bool ignoreCase = false)
		{
			if (!HasName || string.IsNullOrEmpty(name))
			{
				return false;
			}

			if (ignoreCase && Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return !ignoreCase && Name.Equals(name);
		}

		public bool CompareSource(CoalesceAsset asset, bool ignoreCase = false)
		{
			if (asset == null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			return CompareSource(asset.Source, ignoreCase);
		}

		public bool CompareSource(string source, bool ignoreCase = false)
		{
			if (!HasSource || string.IsNullOrEmpty(source))
			{
				return false;
			}

			if (ignoreCase && Source.Equals(source, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return !ignoreCase && Source.Equals(source);
		}

		public bool HasId => !string.IsNullOrEmpty(Id);

	    public bool HasName => !string.IsNullOrEmpty(Name);

	    public bool HasSource => !string.IsNullOrEmpty(Source);

	    public void Combine(CoalesceAsset asset)
		{
			if (asset == null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			foreach (var section in asset.Sections)
			{
				if (!Sections.ContainsKey(section.Key))
				{
					Sections.Add(section.Key, section.Value);
				}
				else
				{
					Sections[section.Key].Combine(section.Value);
				}
			}
		}

		public void MergeRight(CoalesceAsset asset)
		{
			if (asset == null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			throw new NotImplementedException();
		}
	}
}