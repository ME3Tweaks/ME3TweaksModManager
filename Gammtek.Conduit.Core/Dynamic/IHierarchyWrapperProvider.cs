using System.Collections.Generic;

namespace Gammtek.Conduit.Dynamic
{
	public interface IHierarchyWrapperProvider<T>
	{
		IEnumerable<KeyValuePair<string, T>> Attributes { get; }

		IEnumerable<T> Elements { get; }

		object InternalValue { get; set; }

		object InternalContent { get; set; }

		string InternalName { get; set; }

		T InternalParent { get; set; }

		void AddAttribute(string key, T value);

		void AddElement(T element);

		T Attribute(string name);

		T Element(string name);

		object GetAttributeValue(string name);

		bool HasAttribute(string name);

		void RemoveAttribute(string key);

		void RemoveElement(T element);

		void SetAttributeValue(string name, object obj);
	}
}
