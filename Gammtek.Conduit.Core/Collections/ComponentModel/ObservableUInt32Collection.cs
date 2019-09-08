using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gammtek.Conduit.Collections.ComponentModel
{
	public class ObservableUInt32Collection : ObservableCollection<uint>
	{
		public ObservableUInt32Collection() {}

		public ObservableUInt32Collection(List<uint> list)
			: base(list) {}

		public ObservableUInt32Collection([NotNull] IEnumerable<uint> collection)
			: base(collection) {}
	}
}
