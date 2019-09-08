using System.Xml.Linq;

namespace Gammtek.Conduit.Xml.Dynamic
{
	public static class XElementExtension
	{
		public static DynamicXml ToDynamicXmlStream(this XElement xElement)
		{
			var dynamicXmlStream = new DynamicXml(xElement);

			return dynamicXmlStream;
		}
	}
}
