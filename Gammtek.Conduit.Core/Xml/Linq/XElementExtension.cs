using System.Xml.Linq;

namespace Gammtek.Conduit.Xml.Linq
{
	public class XElementExtension : XNodeExtension
	{
		public XElementExtension(XElement element)
			: base(element) {}

		public XElement Element => Node as XElement;
	}
}
