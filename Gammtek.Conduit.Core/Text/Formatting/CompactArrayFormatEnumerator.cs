using System;
using System.Collections;
using System.Collections.Generic;

namespace Gammtek.Conduit.Text.Formatting
{
	/// <summary>
	///     Enumerator used to iterate through the last dimension of the multi-dimensional array.
	///     This class is internal and cannot be used directly.
	/// </summary>
	internal class CompactArrayFormatEnumerator : IEnumerator<object>
	{
		/// <summary>
		///     Array through which this enumerator is iterating.
		/// </summary>
		private readonly Array _array;

		/// <summary>
		///     Values of indices within the array which point to current element.
		/// </summary>
		private readonly int[] _indexValues;

		/// <summary>
		///     Constructor which initializes array through which this enumerator will iterate
		///     and initializes array of index values, all but the last dimension of the array, which are
		///     fixed during the iteration process.
		/// </summary>
		/// <param name="array">Array through which's last dimension this enumerator will enumerate.</param>
		/// <param name="headingIndices">Values of all fixed indices, i.e. all indices but the last one which is variable.</param>
		public CompactArrayFormatEnumerator(Array array, int[] headingIndices)
		{
			_array = array;

			_indexValues = new int[_array.Rank];

			for (var i = 0; i < _array.Rank - 1; i++)
			{
				_indexValues[i] = headingIndices[i];
			}
			_indexValues[_array.Rank - 1] = -1;
		}

		/// <summary>
		///     Gets current element from the last dimension of the array.
		/// </summary>
		public object Current
		{
			get { return _array.GetValue(_indexValues); }
		}

		/// <summary>
		///     Disposes this instance.
		/// </summary>
		public void Dispose()
		{
			// There's nothing to dispose from this instance
		}

		/// <summary>
		///     Gets current value from the last dimension of the array.
		/// </summary>
		object IEnumerator.Current
		{
			get { return Current; }
		}

		/// <summary>
		///     Moves to the next element in the last dimension of the array.
		/// </summary>
		/// <returns>
		///     true if next element is available;
		///     false if enumerator has reached the end of the last dimension in the array.
		/// </returns>
		public bool MoveNext()
		{
			var moved = false;

			if (_indexValues[_indexValues.Length - 1] < _array.GetLength(_indexValues.Length - 1) - 1)
			{
				_indexValues[_indexValues.Length - 1]++;
				moved = true;
			}

			return moved;
		}

		/// <summary>
		///     Resets the enumerator so that it can start from the first element of the last dimension of the array again.
		/// </summary>
		public void Reset()
		{
			_indexValues[_indexValues.Length - 1] = -1;
		}
	}
}
