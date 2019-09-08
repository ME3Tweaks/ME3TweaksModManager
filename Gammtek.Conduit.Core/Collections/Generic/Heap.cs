using System;
using System.Collections.Generic;

namespace Gammtek.Conduit.Collections.Generic
{
	public class Heap<T>
	{
		private readonly IComparer<T> _comparer;
		private readonly IList<T> _list;

		public Heap(IList<T> list, int count, IComparer<T> comparer)
		{
			_comparer = comparer;
			_list = list;
			Count = count;
			Heapify();
		}

		public int Count { get; private set; }

		public T PopRoot()
		{
			if (Count == 0)
			{
				throw new InvalidOperationException("Empty heap.");
			}
			var root = _list[0];
			SwapCells(0, Count - 1);
			Count--;
			HeapDown(0);
			return root;
		}

		public T PeekRoot()
		{
			if (Count == 0)
			{
				throw new InvalidOperationException("Empty heap.");
			}
			return _list[0];
		}

		public void Insert(T e)
		{
			if (Count >= _list.Count)
			{
				_list.Add(e);
			}
			else
			{
				_list[Count] = e;
			}
			Count++;
			HeapUp(Count - 1);
		}

		private void Heapify()
		{
			for (var i = Parent(Count - 1); i >= 0; i--)
			{
				HeapDown(i);
			}
		}

		private void HeapUp(int i)
		{
			var elt = _list[i];
			while (true)
			{
				var parent = Parent(i);
				if (parent < 0 || _comparer.Compare(_list[parent], elt) > 0)
				{
					break;
				}
				SwapCells(i, parent);
				i = parent;
			}
		}

		private void HeapDown(int i)
		{
			while (true)
			{
				var lchild = LeftChild(i);
				if (lchild < 0)
				{
					break;
				}
				var rchild = RightChild(i);

				var child = rchild < 0
					? lchild
					: _comparer.Compare(_list[lchild], _list[rchild]) > 0 ? lchild : rchild;

				if (_comparer.Compare(_list[child], _list[i]) < 0)
				{
					break;
				}
				SwapCells(i, child);
				i = child;
			}
		}

		private int Parent(int i)
		{
			return i <= 0 ? -1 : SafeIndex((i - 1)/2);
		}

		private int RightChild(int i)
		{
			return SafeIndex(2*i + 2);
		}

		private int LeftChild(int i)
		{
			return SafeIndex(2*i + 1);
		}

		private int SafeIndex(int i)
		{
			return i < Count ? i : -1;
		}

		private void SwapCells(int i, int j)
		{
			var temp = _list[i];
			_list[i] = _list[j];
			_list[j] = temp;
		}
	}
}