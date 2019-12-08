using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MassEffect3.Coalesce
{
    [Localizable(false)]
    public class CoalesceProperty : IList<CoalesceValue>
	{
		public const int DefaultValueType = 2;
		public const int NullValueType = 1;

		private readonly IList<CoalesceValue> _values;

		public CoalesceProperty(string name = null, IList<CoalesceValue> values = null)
		{
			_values = values ?? new List<CoalesceValue>();
			Name = name ?? "CoalesceProperty";
		}

		public int Count
		{
			get { return _values.Count; }
		}

		public bool IsReadOnly
		{
			get { return _values.IsReadOnly; }
		}

		public string Name { get; set; }

		public IDictionary<string, string> Settings { get; set; }

		public CoalesceValue this[int index]
		{
			get { return _values[index]; }
			set { _values[index] = value; }
		}

		public void Add(CoalesceValue item)
		{
			_values.Add(item);
		}

		public void Clear()
		{
			_values.Clear();
		}

		public bool Contains(CoalesceValue item)
		{
			return _values.Contains(item);
		}

		public void CopyTo(CoalesceValue[] array, int arrayIndex)
		{
			_values.CopyTo(array, arrayIndex);
		}

		public IEnumerator<CoalesceValue> GetEnumerator()
		{
			return _values.GetEnumerator();
		}

		public int IndexOf(CoalesceValue item)
		{
			return _values.IndexOf(item);
		}

		public void Insert(int index, CoalesceValue item)
		{
			_values.Insert(index, item);
		}

		public bool Remove(CoalesceValue item)
		{
			return _values.Remove(item);
		}

		public void RemoveAt(int index)
		{
			_values.RemoveAt(index);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable) _values).GetEnumerator();
		}
	}
}
