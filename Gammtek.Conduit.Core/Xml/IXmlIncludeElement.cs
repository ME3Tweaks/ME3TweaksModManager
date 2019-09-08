namespace Gammtek.Conduit.Xml
{
	public interface IXmlIncludeElement
	{
		string Accept { get; }

		string AcceptLanguage { get; }

		string Encoding { get; }

		XmlIncludeParseType Parse { get; }

		string XPointer { get; }

		string Href { get; }
	}
}
