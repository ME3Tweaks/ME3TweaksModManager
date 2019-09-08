using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gammtek.Conduit.Collections.ComponentModel
{
	public class ObservableInt64Collection : ObservableCollection<long>
	{
		public ObservableInt64Collection() {}

		public ObservableInt64Collection(List<long> list)
			: base(list) {}

		public ObservableInt64Collection([NotNull] IEnumerable<long> collection)
			: base(collection) {}
	}
}
