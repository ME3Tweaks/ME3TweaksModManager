using System;
using System.Collections.Generic;

namespace Gammtek.Conduit.Collections.Generic
{
	public class BinaryHeap<T>
	{
		private T[] _data;

		private readonly Comparison<T> _comparison;

		public BinaryHeap(int capacity = 4, Comparison<T> comparison = null)
		{
			Size = 0;

			_data = new T[capacity];
			_comparison = comparison ?? Comparer<T>.Default.Compare;
		}

		public int Size { get; private set; }

		/// <summary>
		/// Add an item to the heap
		/// </summary>
		/// <param name="item"></param>
		public void Insert(T item)
		{
			if (Size == _data.Length)
			{
				Resize();
			}

			_data[Size] = item;

			HeapifyUp(Size);

			Size++;
		}

		/// <summary>
		/// Get the item of the root
		/// </summary>
		/// <returns></returns>
		public T Peak()
		{
			return _data[0];
		}

		/// <summary>
		/// Extract the item of the root
		/// </summary>
		/// <returns></returns>
		public T Pop()
		{
			var item = _data[0];

			Size--;
			_data[0] = _data[Size];
			HeapifyDown(0);
			
			return item;
		}

		private void Resize()
		{
			var resizedData = new T[_data.Length * 2];
			
			Array.Copy(_data, 0, resizedData, 0, _data.Length);
			
			_data = resizedData;
		}

		private void HeapifyUp(int childIdx)
		{
			while (true)
			{
				if (childIdx <= 0)
				{
					return;
				}

				var parentIdx = (childIdx - 1)/2;

				if (_comparison.Invoke(_data[childIdx], _data[parentIdx]) <= 0)
				{
					return;
				}

				// swap parent and child
				var t = _data[parentIdx];

				_data[parentIdx] = _data[childIdx];
				_data[childIdx] = t;
				childIdx = parentIdx;
			}
		}

		private void HeapifyDown(int parentIdx)
		{
			while (true)
			{
				var leftChildIdx = 2*parentIdx + 1;
				var rightChildIdx = leftChildIdx + 1;
				var largestChildIdx = parentIdx;

				if (leftChildIdx < Size && _comparison.Invoke(_data[leftChildIdx], _data[largestChildIdx]) > 0)
				{
					largestChildIdx = leftChildIdx;
				}

				if (rightChildIdx < Size && _comparison.Invoke(_data[rightChildIdx], _data[largestChildIdx]) > 0)
				{
					largestChildIdx = rightChildIdx;
				}

				if (largestChildIdx == parentIdx)
				{
					return;
				}

				var t = _data[parentIdx];

				_data[parentIdx] = _data[largestChildIdx];
				_data[largestChildIdx] = t;
				parentIdx = largestChildIdx;
			}
		}
	}
}