using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Gammtek.Conduit.Xml.Linq
{
	[XmlRoot("base-uri")]
	public class XmlBaseUriAnnotation : IXmlSerializable
	{
		public XmlBaseUriAnnotation()
			: this(null)
		{ }

		public XmlBaseUriAnnotation(Uri baseUri)
		{
			BaseUri = baseUri;
		}

		public Uri BaseUri { get; set; }

		public override string ToString()
		{
			return BaseUri?.ToString() ?? "";
		}

		public XmlSchema GetSchema()
		{
			throw new NotImplementedException();
		}

		public void ReadXml(XmlReader reader)
		{
			if (reader.MoveToContent() != XmlNodeType.Element || reader.LocalName != "base-uri")
			{
				return;
			}

			var baseUri = reader["uri"];

			if (baseUri != null)
			{
				BaseUri = new Uri(baseUri);
			}
		}

		public void WriteXml(XmlWriter writer)
		{
			if (BaseUri != null)
			{
				writer.WriteAttributeString("uri", BaseUri.ToString());
			}
		}
	}
}
