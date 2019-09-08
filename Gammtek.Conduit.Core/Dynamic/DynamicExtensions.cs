using System;
using System.Linq;
using System.Xml.Linq;

namespace Gammtek.Conduit.Dynamic
{
	public static class DynamicExtensions
	{
		public static ElasticObject ElasticFromXElement(XElement el)
		{
			var exp = new ElasticObject();

			if (!string.IsNullOrEmpty(el.Value))
			{
				exp.InternalValue = el.Value;
			}

			exp.InternalName = el.Name.LocalName;

			foreach (var a in el.Attributes())
			{
				exp.CreateOrGetAttribute(a.Name.LocalName, a.Value);
			}

			var textNode = el.Nodes().FirstOrDefault();

			if (textNode is XText)
			{
				exp.InternalContent = textNode.ToString();
			}

			foreach (var child in el.Elements().Select(ElasticFromXElement))
			{
				child.InternalParent = exp;

				exp.AddElement(child);
			}

			return exp;
		}

		public static dynamic ToElastic(this XElement e)
		{
			return ElasticFromXElement(e);
		}

		public static XElement ToXElement(this ElasticObject e)
		{
			return XElementFromElastic(e);
		}

		public static XElement XElementFromElastic(ElasticObject elastic, XNamespace nameSpace = null)
		{
			// we default to empty namespace
			nameSpace = nameSpace ?? string.Empty;
			var exp = new XElement(nameSpace + elastic.InternalName);

			foreach (var a in elastic.Attributes.Where(a => a.Value.InternalValue != null))
			{
				// if we have xmlns attribute add it like XNamespace instead of regular attribute
				if (a.Key.Equals("xmlns", StringComparison.InvariantCultureIgnoreCase))
				{
					nameSpace = a.Value.InternalValue.ToString();
					exp.Name = nameSpace.GetName(exp.Name.LocalName);
				}
				else
				{
					exp.Add(new XAttribute(a.Key, a.Value.InternalValue));
				}
			}

			if (elastic.InternalContent is string)
			{
				exp.Add(new XText(elastic.InternalContent as string));
			}

			foreach (var child in elastic.Elements.Select(c => XElementFromElastic(c, nameSpace)))
			{
				exp.Add(child);
			}

			return exp;
		}
	}
}
