using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace Gammtek.Conduit.Xml.Dynamic
{
	public class DynamicXml : DynamicObject, IEnumerable<DynamicXml>
	{
		public delegate void AddElementAction<in T>(T obj);

		public delegate void CreateElementAction<in T>(T obj);

		public delegate void GetElementAction<in T>(T obj);

		public delegate void RemoveElementAction<in T>(T obj);

		public delegate void SetElementAction<in T>(T obj);

		private const string CountBinderName = "Count";
		private const string ValueBinderName = "Value";

		public DynamicXml()
		{
			XmlElementsCollection = new List<XElement>();
		}

		public DynamicXml(string xmlString)
		{
			var xDocument = XDocument.Parse(xmlString);
			var xElement = XmlNamespaceRemover.RemoveAllNamespaces(xDocument).Root;

			XmlElementsCollection = new List<XElement>
			{
				xElement
			};
		}

		public DynamicXml(DynamicXml dynamicXml)
		{
			XmlElementsCollection = dynamicXml.XmlElementsCollection;
		}

		internal DynamicXml(XElement xElement)
		{
			XmlElementsCollection = new List<XElement>
			{
				XmlNamespaceRemover.RemoveAllNamespaces(xElement)
			};
		}

		internal DynamicXml(IEnumerable<XElement> xElementCollection)
		{
			XmlElementsCollection = new List<XElement>(xElementCollection);
		}

		public List<XElement> XmlElementsCollection { get; set; }

		IEnumerator<DynamicXml> IEnumerable<DynamicXml>.GetEnumerator()
		{
			return XmlElementsCollection.Select(xElement => new DynamicXml(xElement)).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return XmlElementsCollection.Select(xElement => new DynamicXml(xElement)).GetEnumerator();
		}

		public static DynamicXml CreateElement(string rootElementName)
		{
			var dynamicXmlStream = new DynamicXml();
			dynamicXmlStream.XmlElementsCollection.Add(new XElement(rootElementName));

			return dynamicXmlStream;
		}

		public static Action CreateElement(Action action)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			return action;
		}

		public static CreateElementAction<dynamic> CreateElement(Action<dynamic> action)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			CreateElementAction<dynamic> createElementAction = obj => action(obj);

			return createElementAction;
		}

		public static GetElementAction<dynamic> GetElement(Action<dynamic> action)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			GetElementAction<dynamic> getElementAction = obj => action(obj);

			return getElementAction;
		}

		/*private void OnCreateElement(CreateElementAction<dynamic> action, string name)
		{
			var dynamicXml = CreateElement(name);

			if (XmlElementsCollection.Count == 0)
			{
				XmlElementsCollection = dynamicXml.XmlElementsCollection;
			}
			else
			{
				XmlElementsCollection[0].Add(dynamicXml.XmlElementsCollection[0]);
			}

			if (action != null)
			{
				action(dynamicXml);
			}
		}*/

		/*private void OnGetElement(GetElementAction<dynamic> action, string name)
		{
			var dynamicXml = this;

			if (XmlElementsCollection.Count > 0)
			{
				var xElementItems = XmlElementsCollection[0].DescendantsAndSelf(XName.Get(name));

				var xElementCollection = xElementItems as IList<XElement> ?? xElementItems.ToList();

				if (xElementCollection.Any())
				{
					dynamicXml = new DynamicXml(xElementCollection);
				}
			}

			if (action != null)
			{
				action(dynamicXml);
			}
		}*/

		public static DynamicXml Load(Stream fileStream, LoadOptions options = LoadOptions.None)
		{
			return new DynamicXml(XElement.Load(fileStream, options));
		}

		public static DynamicXml Load(string uri, LoadOptions options = LoadOptions.None)
		{
			return new DynamicXml(XElement.Load(uri, options));
		}

		public static DynamicXml Load(TextReader textReader, LoadOptions options = LoadOptions.None)
		{
			return new DynamicXml(XElement.Load(textReader, options));
		}

		public static DynamicXml Load(XmlReader xmlReader, LoadOptions options = LoadOptions.None)
		{
			return new DynamicXml(XElement.Load(xmlReader, options));
		}

		public static DynamicXml Parse(string xmlString, LoadOptions options = LoadOptions.None)
		{
			return new DynamicXml(XElement.Parse(xmlString, options));
		}

		public void Save(string fileName, bool indent = true, string indentChars = "\t")
		{
			var writerSettings = new XmlWriterSettings
			{
				Indent = indent,
				IndentChars = indentChars
			};

			using (var writer = XmlWriter.Create(fileName, writerSettings))
			{
				XmlElementsCollection[0].Save(writer);
			}
		}

		public override string ToString()
		{
			return (XmlElementsCollection != null && XmlElementsCollection.Count > 0)
				? XmlElementsCollection[0].ToString()
				: string.Empty;
		}

		public override bool TryConvert(ConvertBinder binder, out object result)
		{
			if (binder.Type == typeof (XElement))
			{
				result = XmlElementsCollection[0];
			}
			else if (binder.Type == typeof (List<XElement>) ||
					 (binder.Type.IsArray && binder.Type.GetElementType() == typeof (XElement)))
			{
				result = XmlElementsCollection;
			}
			else if (binder.Type == typeof (String))
			{
				result = XmlElementsCollection[0].Value;
			}
			else
			{
				result = false;
				return false;
			}

			return true;
		}

		public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
		{
			result = null;

			if (indexes[0] == null)
			{
				return false;
			}

			if (indexes[0] is int)
			{
				var index = (int) indexes[0];
				result = new DynamicXml(XmlElementsCollection[index]);

				return true;
			}

			if (!(indexes[0] is string))
			{
				return false;
			}

			var attributeName = (string) indexes[0];
			var attribute = XmlElementsCollection[0].Attribute(XName.Get(attributeName));

			result = attribute?.Value;

			return true;
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			result = null;

			switch (binder.Name)
			{
				case ValueBinderName:
				{
					var items = XmlElementsCollection[0].Descendants(XName.Get(ValueBinderName));

					var xElementCollection = items as IList<XElement> ?? items.ToList();

					if (!xElementCollection.Any())
					{
						result = XmlElementsCollection[0].Value;
					}
					else
					{
						result = new DynamicXml(xElementCollection);
					}

					break;
				}
				case CountBinderName:
				{
					result = XmlElementsCollection.Count;

					break;
				}
				default:
				{
					var xAttribute = XmlElementsCollection[0].Attribute(XName.Get(binder.Name));

					if (null != xAttribute)
					{
						result = xAttribute;
					}
					else
					{
						var xElementItems = XmlElementsCollection[0].DescendantsAndSelf(XName.Get(binder.Name));

						var xElementCollection = xElementItems as IList<XElement> ?? xElementItems.ToList();

						if (xElementCollection.Any())
						{
							result = new DynamicXml(xElementCollection);
						}
					}
				}
					break;
			}

			return true;
		}

		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
		{
			var xmlType = typeof (XElement);
			Action fragment = null;

			args.ToList().ForEach(arg =>
			{
				var argAction = arg as Action;

				if (argAction != null)
				{
					fragment = argAction;
				}
				else if (arg is Action<dynamic>)
				{
					fragment = () =>
					{
						var action = arg as Action<dynamic>;

						action?.Invoke(this);
					};
				}
				else if (arg is CreateElementAction<dynamic>)
				{
					fragment = () =>
					{
						var action = arg as CreateElementAction<dynamic>;

						var dynamicXml = CreateElement(binder.Name);

						if (XmlElementsCollection.Count == 0)
						{
							XmlElementsCollection = dynamicXml.XmlElementsCollection;
						}
						else
						{
							XmlElementsCollection[0].Add(dynamicXml.XmlElementsCollection[0]);
						}

						action?.Invoke(dynamicXml);
					};
				}
				else if (arg is GetElementAction<dynamic>)
				{
					fragment = () =>
					{
						var action = arg as GetElementAction<dynamic>;

						var dynamicXml = this;

						if (XmlElementsCollection.Count > 0)
						{
							var xElementItems = XmlElementsCollection[0].DescendantsAndSelf(XName.Get(binder.Name));

							var xElementCollection = xElementItems as IList<XElement> ?? xElementItems.ToList();

							if (xElementCollection.Any())
							{
								dynamicXml = new DynamicXml(xElementCollection);
							}
						}

						action?.Invoke(dynamicXml);
					};
				}
			});

			if (fragment != null)
			{
				fragment();

				result = null;

				return true;
			}

			try
			{
				result = xmlType.InvokeMember(binder.Name,
					BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
					null, XmlElementsCollection[0], args);

				return true;
			}
			catch
			{
				result = null;

				return false;
			}
		}

		public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
		{
			if (!(indexes[0] is string))
			{
				return false;
			}

			XmlElementsCollection[0].SetAttributeValue((string) indexes[0], value);

			return true;
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			if (binder.Name == ValueBinderName)
			{
				XmlElementsCollection[0].Value = value.ToString();
			}
			else
			{
				var setNode = XmlElementsCollection[0].Element(binder.Name);

				if (setNode != null)
				{
					setNode.SetValue(value);
				}
				else
				{
					XmlElementsCollection[0].Add(value.GetType() == typeof (DynamicXml)
						? new XElement(binder.Name)
						: new XElement(binder.Name, value));
				}
			}

			return true;
		}
	}
}
