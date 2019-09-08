using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Gammtek.Conduit.Collections.Generic
{
	public sealed class HeapPriorityQueue<T> : IPriorityQueue<T>
		where T : PriorityQueueNode
	{
		private readonly T[] _nodes;
		private long _numNodesEverEnqueued;

		public HeapPriorityQueue(int maxNodes)
		{
			Count = 0;
			_nodes = new T[maxNodes + 1];
			_numNodesEverEnqueued = 0;
		}

		public int Count { get; private set; }

		public T First
		{
			get { return _nodes[1]; }
		}

		public int MaxSize
		{
			get { return _nodes.Length - 1; }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Clear()
		{
			for (var i = 1; i < _nodes.Length; i++)
			{
				_nodes[i] = null;
			}

			Count = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(T node)
		{
			return (_nodes[node.QueueIndex] == node);
		}

		public T Dequeue()
		{
			var returnMe = _nodes[1];

			Remove(returnMe);

			return returnMe;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Enqueue(T node, double priority)
		{
			node.Priority = priority;

			Count++;

			_nodes[Count] = node;
			node.QueueIndex = Count;
			node.InsertionIndex = _numNodesEverEnqueued++;

			CascadeUp(_nodes[Count]);
		}

		public IEnumerator<T> GetEnumerator()
		{
			for (var i = 1; i <= Count; i++)
			{
				yield return _nodes[i];
			}
		}

		public bool IsValidQueue()
		{
			for (var i = 1; i < _nodes.Length; i++)
			{
				if (_nodes[i] == null)
				{
					continue;
				}

				var childLeftIndex = 2 * i;

				if (childLeftIndex < _nodes.Length && _nodes[childLeftIndex] != null && HasHigherPriority(_nodes[childLeftIndex], _nodes[i]))
				{
					return false;
				}

				var childRightIndex = childLeftIndex + 1;

				if (childRightIndex < _nodes.Length && _nodes[childRightIndex] != null && HasHigherPriority(_nodes[childRightIndex], _nodes[i]))
				{
					return false;
				}
			}

			return true;
		}

		public void Remove(T node)
		{
			if (!Contains(node))
			{
				return;
			}

			if (Count <= 1)
			{
				_nodes[1] = null;
				Count = 0;

				return;
			}

			var wasSwapped = false;
			var formerLastNode = _nodes[Count];

			if (node.QueueIndex != Count)
			{
				Swap(node, formerLastNode);

				wasSwapped = true;
			}

			Count--;
			_nodes[node.QueueIndex] = null;

			if (wasSwapped)
			{
				OnNodeUpdated(formerLastNode);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UpdatePriority(T node, double priority)
		{
			node.Priority = priority;

			OnNodeUpdated(node);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CascadeDown(T node)
		{
			var finalQueueIndex = node.QueueIndex;

			while (true)
			{
				var newParent = node;
				var childLeftIndex = 2 * finalQueueIndex;

				if (childLeftIndex > Count)
				{
					node.QueueIndex = finalQueueIndex;
					_nodes[finalQueueIndex] = node;

					break;
				}

				var childLeft = _nodes[childLeftIndex];

				if (HasHigherPriority(childLeft, newParent))
				{
					newParent = childLeft;
				}

				var childRightIndex = childLeftIndex + 1;

				if (childRightIndex <= Count)
				{
					var childRight = _nodes[childRightIndex];

					if (HasHigherPriority(childRight, newParent))
					{
						newParent = childRight;
					}
				}

				if (newParent != node)
				{
					_nodes[finalQueueIndex] = newParent;

					var temp = newParent.QueueIndex;
					newParent.QueueIndex = finalQueueIndex;
					finalQueueIndex = temp;
				}
				else
				{
					node.QueueIndex = finalQueueIndex;
					_nodes[finalQueueIndex] = node;

					break;
				}
			}
		}

		private void CascadeUp(T node)
		{
			var parent = node.QueueIndex / 2;

			while (parent >= 1)
			{
				var parentNode = _nodes[parent];
				if (HasHigherPriority(parentNode, node))
				{
					break;
				}

				Swap(node, parentNode);

				parent = node.QueueIndex / 2;
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool HasHigherPriority(T higher, T lower)
		{
			return (higher.Priority < lower.Priority ||
					(higher.Priority.Equals(lower.Priority) && higher.InsertionIndex < lower.InsertionIndex));
		}

		private void OnNodeUpdated(T node)
		{
			var parentIndex = node.QueueIndex / 2;
			var parentNode = _nodes[parentIndex];

			if (parentIndex > 0 && HasHigherPriority(node, parentNode))
			{
				CascadeUp(node);
			}
			else
			{
				CascadeDown(node);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Swap(T node1, T node2)
		{
			_nodes[node1.QueueIndex] = node2;
			_nodes[node2.QueueIndex] = node1;

			var temp = node1.QueueIndex;
			node1.QueueIndex = node2.QueueIndex;
			node2.QueueIndex = temp;
		}
	}
}
