using System.Collections.Generic;

namespace Gammtek.Conduit.Collections.Generic
{
	public class HeapSort<T>
	{
		public static void Sort(IList<T> list, IComparer<T> comparer)
		{
			var heap = new Heap<T>(list, list.Count, comparer);

			while (heap.Count > 0)
			{
				heap.PopRoot();
			}
		}
	}
}
