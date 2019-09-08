using System.Xml.Linq;

namespace Gammtek.Conduit.Xml.Linq
{
	public abstract class XNodeExtension : IXObjectExtension
	{
		public XNodeExtension(XNode node)
		{
			Node = node;
		}

		public XNode Node { get; }

		public XObject Object => Node;
	}
}
