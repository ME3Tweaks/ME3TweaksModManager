using System.Collections.Generic;

namespace Gammtek.Conduit.Collections.Generic
{
	public interface IPriorityQueue<T> : IEnumerable<T>
		where T : PriorityQueueNode
	{
		int Count { get; }

		T First { get; }

		int MaxSize { get; }

		void Clear();

		bool Contains(T node);

		T Dequeue();

		void Enqueue(T node, double priority);

		void Remove(T node);

		void UpdatePriority(T node, double priority);
	}
}
