using System.Collections.Generic;

namespace Gammtek.Conduit.Collections.Generic
{
	public class Buffer<T> : Queue<T>
	{
		private readonly int? _capacity;

		public Buffer()
		{
			_capacity = null;
		}

		public Buffer(int capacity)
		{
			_capacity = capacity;
		}

		public int Capacity
		{
			get
			{
				if (_capacity != null)
				{
					return (int) _capacity;
				}

				return -1;
			}
		}

		public void Add(T newElement)
		{
			if (Count == (_capacity ?? -1))
			{
				Dequeue();
			}

			Enqueue(newElement);
		}
	}
}
