using System.Xml;

namespace Gammtek.Conduit.Xml
{
	public class XmlIncludeElement : XmlElement
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="T:System.Xml.XmlElement" /> class.
		/// </summary>
		/// <param name="prefix">The namespace prefix; see the <see cref="P:System.Xml.XmlElement.Prefix" /> property.</param>
		/// <param name="localName">The local name; see the <see cref="P:System.Xml.XmlElement.LocalName" /> property.</param>
		/// <param name="namespaceURI">The namespace URI; see the <see cref="P:System.Xml.XmlElement.NamespaceURI" /> property.</param>
		/// <param name="doc">The parent XML document.</param>
		public XmlIncludeElement(string prefix, [NotNull] string localName, string namespaceURI, [NotNull] XmlDocument doc)
			: base(prefix, localName, namespaceURI, doc) {}
	}
}
