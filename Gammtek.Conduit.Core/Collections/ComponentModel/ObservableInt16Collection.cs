using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gammtek.Conduit.Collections.ComponentModel
{
	public class ObservableUInt16Collection : ObservableCollection<ushort>
	{
		public ObservableUInt16Collection() {}

		public ObservableUInt16Collection(List<ushort> list)
			: base(list) {}

		public ObservableUInt16Collection([NotNull] IEnumerable<ushort> collection)
			: base(collection) {}
	}
}
