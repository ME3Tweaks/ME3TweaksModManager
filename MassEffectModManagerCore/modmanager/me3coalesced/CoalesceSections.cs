using System;
using System.Collections;
using System.Collections.Generic;

namespace MassEffect3.Coalesce
{
	public class CoalesceSections : IDictionary<string, CoalesceSection>
	{
		private readonly IDictionary<string, CoalesceSection> _sections;

		public CoalesceSections(IDictionary<string, CoalesceSection> sections = null)
		{
			_sections = sections ?? new Dictionary<string, CoalesceSection>();
		}

		public int Count
		{
			get { return _sections.Count; }
		}

		public bool IsReadOnly
		{
			get { return _sections.IsReadOnly; }
		}

		public ICollection<string> Keys
		{
			get { return _sections.Keys; }
		}

		public ICollection<CoalesceSection> Values
		{
			get { return _sections.Values; }
		}

		public CoalesceSection this[string key]
		{
			get { return _sections[key]; }
			set { _sections[key] = value; }
		}

		public void Add(KeyValuePair<string, CoalesceSection> item)
		{
			_sections.Add(item);
		}

		public void Add(string key, CoalesceSection value)
		{
			_sections.Add(key, value);
		}

		public void Clear()
		{
			_sections.Clear();
		}

		public void Combine(CoalesceSections sections)
		{
			if (sections == null)
			{
				throw new ArgumentNullException(nameof(sections));
			}

			foreach (var section in sections)
			{
				if (!ContainsKey(section.Key))
				{
					Add(section.Key, section.Value);
				}
				else
				{
					this[section.Key].Combine(section.Value);
				}
			}
		}

		public bool Contains(KeyValuePair<string, CoalesceSection> item)
		{
			return _sections.Contains(item);
		}

		public bool ContainsKey(string key)
		{
			return _sections.ContainsKey(key);
		}

		public void CopyTo(KeyValuePair<string, CoalesceSection>[] array, int arrayIndex)
		{
			_sections.CopyTo(array, arrayIndex);
		}

		public IEnumerator<KeyValuePair<string, CoalesceSection>> GetEnumerator()
		{
			return _sections.GetEnumerator();
		}

		public bool Remove(KeyValuePair<string, CoalesceSection> item)
		{
			return _sections.Remove(item);
		}

		public bool Remove(string key)
		{
			return _sections.Remove(key);
		}

		public bool TryGetValue(string key, out CoalesceSection value)
		{
			return _sections.TryGetValue(key, out value);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable) _sections).GetEnumerator();
		}
	}
}
