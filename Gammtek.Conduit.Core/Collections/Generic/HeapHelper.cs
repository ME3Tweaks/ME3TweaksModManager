using System;
using System.Collections.Generic;

namespace Gammtek.Conduit.Collections.Generic
{
	public static class HeapHelper
	{
		/// <summary>
		///     Convert IList to a max-heap from bottom up such that each node maintains the
		///     max-heap property (data[Parent[index]] >= data[index] where Parent = index / 2).
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		public static void BuildMaxHeap<T>(IList<T> data) 
			where T : IComparable, IComparable<T>
		{
			var heapSize = data.Count;

			for (var index = (heapSize / 2) - 1; index > -1; index--)
			{
				MaxHeapify(data, index, heapSize);
			}
		}

		/// <summary>
		///     Convert IList to a min-heap from bottom up such that each node maintains the
		///     min-heap property (data[Parent[index]] &lt;= data[index] where Parent = index / 2).
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		public static void BuildMinHeap<T>(IList<T> data)
			where T : IComparable, IComparable<T>
		{
			var heapSize = data.Count;

			for (var index = (heapSize / 2) - 1; index > -1; index--)
			{
				MinHeapify(data, index, heapSize);
			}
		}

		/// <summary>
		///     Extrax max and re-heapify with decremented heapSize.
		///     Caller must remember to decrement local heap size.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		/// <param name="heapSize"></param>
		/// <returns></returns>
		public static T ExtractMax<T>(IList<T> data, int heapSize)
			where T : IComparable, IComparable<T>
		{
			heapSize--;

			if (heapSize < 0)
			{
				throw new IndexOutOfRangeException();
			}

			var max = data[0];
			data[0] = data[heapSize];

			if (heapSize > 0)
			{
				MaxHeapify(data, 0, heapSize);
			}

			return max;
		}

		/// <summary>
		///     Extrax min and re-heapify with decremented heapSize.
		///     Caller must remember to decrement local heap size.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		/// <param name="heapSize"></param>
		/// <returns></returns>
		public static T ExtractMin<T>(IList<T> data, int heapSize)
			where T : IComparable, IComparable<T>
		{
			heapSize--;

			if (heapSize < 0)
			{
				throw new IndexOutOfRangeException();
			}

			var max = data[0];
			data[0] = data[heapSize];

			if (heapSize > 0)
			{
				MinHeapify(data, 0, heapSize);
			}

			return max;
		}

		public static bool MaxContains<T>(IList<T> data, T item, int index, int heapSize)
			where T : IComparable, IComparable<T>
		{
			if (index >= heapSize)
			{
				return false;
			}

			if (index == 0)
			{
				if (Equals(data[index], default(T)))
				{
					if (Equals(item, default(T)))
					{
						return true;
					}
				}
				else
				{
					var rootComp = data[index].CompareTo(item);

					if (rootComp == 0)
					{
						return true;
					}

					if (rootComp < 0)
					{
						return false;
					}
				}
			}

			var left = HeapLeft(index);
			var leftComp = 0;

			if (left < heapSize)
			{
				if (Equals(data[left], default(T)))
				{
					if (Equals(item, default(T)))
					{
						return true;
					}
				}
				else
				{
					leftComp = data[left].CompareTo(item);
				
					if (leftComp == 0)
					{
						return true;
					}
				}
			}

			var right = HeapRight(index);
			var rightComp = 0;

			if (right < heapSize)
			{
				if (Equals(data[right], default(T)))
				{
					if (Equals(item, default(T)))
					{
						return true;
					}
				}
				else
				{
					rightComp = data[right].CompareTo(item);
				
					if (rightComp == 0)
					{
						return true;
					}
				}
			}

			if (leftComp < 0 && rightComp < 0)
			{
				return false;
			}

			var leftResult = false;
			
			if (leftComp > 0)
			{
				leftResult = MaxContains(data, item, left, heapSize);
			}
		
			if (leftResult)
			{
				return true;
			}

			var rightResult = false;
		
			if (rightComp > 0)
			{
				rightResult = MaxContains(data, item, right, heapSize);
			}
		
			return rightResult;
		}

		/// <summary>
		///     Maintain max-heap property for data at index location for specified heap size
		///     such that data[Parent[index]] &gt;= data[index] where Parent = index / 2.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		/// <param name="index"></param>
		/// <param name="heapSize"></param>
		public static void MaxHeapify<T>(IList<T> data, int index, int heapSize)
			where T : IComparable, IComparable<T>
		{
			while (true)
			{
				var largest = index;
				var left = HeapLeft(index);
				var right = HeapRight(index);

				if (left < heapSize && (!Equals(data[left], default(T)) && data[left].CompareTo(data[index]) > 0))
				{
					largest = left;
				}

				if (right < heapSize && (!Equals(data[right], default(T)) && data[right].CompareTo(data[largest]) > 0))
				{
					largest = right;
				}

				if (largest == index)
				{
					return;
				}

				//exchange data[index] with data[largest}
				var tempRef = data[index];
				data[index] = data[largest];
				data[largest] = tempRef;
				index = largest;
			}
		}

		public static void MaxIncrease<T>(IList<T> data, int index, T item)
			where T : IComparable, IComparable<T>
		{
			if (Equals(item, default(T)) || item.CompareTo(data[index]) < 0)
			{
				throw new ArgumentException(@"New item is smaller than the current item", nameof(item));
			}

			data[index] = item;
			var parent = HeapParent(index);

			while (index > 0
				   && (Equals(data[parent], default(T))
					   || data[parent].CompareTo(data[index]) < 0))
			{
				//exchange data[index] with data[parent}
				var tempRef = data[index];
				data[index] = data[parent];
				data[parent] = tempRef;
				index = parent;
				parent = HeapParent(index);
			}
		}

		/// <summary>
		///     Insert item into max heap. Caller must remember to increment heapSize locally.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		/// <param name="item"></param>
		/// <param name="minOfT"></param>
		/// <param name="heapSize"></param>
		public static void MaxInsert<T>(IList<T> data, T item, T minOfT, int heapSize)
			where T : IComparable, IComparable<T>
		{
			heapSize++;

			if (heapSize - 1 < data.Count)
			{
				data[heapSize - 1] = minOfT;
			}
			else
			{
				data.Add(minOfT);
			}

			MaxIncrease(data, heapSize - 1, item);
		}

		public static bool MinContains<T>(IList<T> data, T item, int index, int heapSize)
			where T : IComparable, IComparable<T>
		{
			if (index >= heapSize)
			{
				return false;
			}

			if (index == 0)
			{
				if (Equals(data[index], default(T)))
				{
					if (Equals(item, default(T)))
					{
						return true;
					}
				}
				else
				{
					var rootComp = data[index].CompareTo(item);

					if (rootComp == 0)
					{
						return true;
					}

					if (rootComp > 0)
					{
						return false;
					}
				}
			}

			var left = HeapLeft(index);
			var leftComp = 0;

			if (left < heapSize)
			{
				if (Equals(data[left], default(T)))
				{
					if (Equals(item, default(T)))
					{
						return true;
					}
				}
				else
				{
					leftComp = data[left].CompareTo(item);

					if (leftComp == 0)
					{
						return true;
					}
				}
			}

			var right = HeapRight(index);
			var rightComp = 0;

			if (right < heapSize)
			{
				if (Equals(data[right], default(T)))
				{
					if (Equals(item, default(T)))
					{
						return true;
					}
				}
				else
				{
					rightComp = data[right].CompareTo(item);
					
					if (rightComp == 0)
					{
						return true;
					}
				}
			}

			if (leftComp > 0 && rightComp > 0)
			{
				return false;
			}

			var leftResult = false;

			if (leftComp < 0)
			{
				leftResult = MinContains(data, item, left, heapSize);
			}

			if (leftResult)
			{
				return true;
			}

			var rightResult = false;

			if (rightComp < 0)
			{
				rightResult = MinContains(data, item, right, heapSize);
			}

			return rightResult;
		}

		public static void MinDecrease<T>(IList<T> data, int index, T item)
			where T : IComparable, IComparable<T>
		{
			if (Equals(item, default(T)) || item.CompareTo(data[index]) > 0)
			{
				throw new ArgumentException(@"New item is greater than the current item", nameof(item));
			}

			data[index] = item;
			var parent = HeapParent(index);
			
			while (index > 0
				   && (Equals(data[index], default(T))
					   || data[index].CompareTo(data[parent]) < 0))
			{
				//exchange data[index] with data[parent}
				var tempRef = data[index];
				data[index] = data[parent];
				data[parent] = tempRef;
				index = parent;
				parent = HeapParent(index);
			}
		}

		/// <summary>
		///     Maintain min-heap property for data at index location for specified heap size
		///     such that data[Parent[index]] &lt;= data[index]
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		/// <param name="index"></param>
		/// <param name="heapSize"></param>
		public static void MinHeapify<T>(IList<T> data, int index, int heapSize)
			where T : IComparable, IComparable<T>
		{
			while (true)
			{
				var smallest = index;
				var left = HeapLeft(index);
				var right = HeapRight(index);

				if (left < heapSize && (Equals(data[left], default(T)) || data[left].CompareTo(data[index]) < 0))
				{
					smallest = left;
				}

				if (right < heapSize && (Equals(data[right], default(T)) || data[right].CompareTo(data[smallest]) < 0))
				{
					smallest = right;
				}

				if (smallest == index)
				{
					return;
				}

				//exchange data[index] with data[largest}
				var tempRef = data[index];
				data[index] = data[smallest];
				data[smallest] = tempRef;
				index = smallest;
			}
		}

		/// <summary>
		///     Insert item into min heap. Caller must remember to increment heapSize locally.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		/// <param name="item"></param>
		/// <param name="maxOfT"></param>
		/// <param name="heapSize"></param>
		public static void MinInsert<T>(IList<T> data, T item, T maxOfT, int heapSize)
			where T : IComparable, IComparable<T>
		{
			heapSize++;

			if (heapSize - 1 < data.Count)
			{
				data[heapSize - 1] = maxOfT;
			}
			else
			{
				data.Add(maxOfT);
			}

			MinDecrease(data, heapSize - 1, item);
		}

		private static int HeapLeft(int i)
		{
			return (i << 1) + 1; //i * 2 + 1
		}

		private static int HeapParent(int i)
		{
			return i >> 1; // i / 2
		}

		private static int HeapRight(int i)
		{
			return (i << 1) + 2; //i * 2 + 2
		}
	}
}
