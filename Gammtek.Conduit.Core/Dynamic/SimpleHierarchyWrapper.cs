using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.Dynamic
{
	public class SimpleHierarchyWrapper : IElasticHierarchyWrapper
	{
		private readonly Dictionary<string, ElasticObject> _attributes = new Dictionary<string, ElasticObject>();
		private readonly Dictionary<string, List<ElasticObject>> _elements = new Dictionary<string, List<ElasticObject>>();

		public IEnumerable<KeyValuePair<string, ElasticObject>> Attributes
		{
			get { return _attributes; }
		}

		public bool HasAttribute(string name)
		{
			return _attributes.ContainsKey(name);
		}

		public ElasticObject Attribute(string name)
		{
			return HasAttribute(name) ? _attributes[name] : null;
		}

		public ElasticObject Element(string name)
		{
			return Elements.FirstOrDefault(item => item.InternalName == name);
		}

		public IEnumerable<ElasticObject> Elements
		{
			get
			{
				var result = from list in _elements
							 from item in list.Value
							 select item;

				return result;
			}
		}

		public void AddAttribute(string key, ElasticObject value)
		{
			_attributes.Add(key, value);
		}

		public void RemoveAttribute(string key)
		{
			_attributes.Remove(key);
		}

		public void AddElement(ElasticObject element)
		{
			if (!_elements.ContainsKey(element.InternalName))
			{
				_elements[element.InternalName] = new List<ElasticObject>();
			}

			_elements[element.InternalName].Add(element);
		}

		public void RemoveElement(ElasticObject element)
		{
			if (!_elements.ContainsKey(element.InternalName))
			{
				return;
			}

			if (_elements[element.InternalName].Contains(element))
			{
				_elements[element.InternalName].Remove(element);
			}
		}

		public object InternalContent { get; set; }

		public object InternalValue { get; set; }

		public string InternalName { get; set; }

		public ElasticObject InternalParent { get; set; }

		public void SetAttributeValue(string name, object obj)
		{
			_attributes[name].InternalValue = obj;
		}

		public object GetAttributeValue(string name)
		{
			return _attributes[name].InternalValue;
		}
	}
}
