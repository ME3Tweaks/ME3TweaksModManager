using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Gammtek.Conduit.Xml.Dynamic
{
	public class XmlNamespaceRemover
	{
		private const string NamespaceXsltPath = "Gammtek.Conduit.Xml.Dynamic.StripNamespace.xslt";
		private static readonly XslCompiledTransform NamespaceXslTransform;

		static XmlNamespaceRemover()
		{
			NamespaceXslTransform = new XslCompiledTransform();

			var xsltStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(NamespaceXsltPath);

			if (xsltStream == null)
			{
				return;
			}
			
			var xsltReader = XmlReader.Create(xsltStream);

			NamespaceXslTransform.Load(xsltReader);
		}

		public static void RemoveAllNamespaces(ref XDocument xDocumentSource)
		{
			var docStream = new MemoryStream();
			xDocumentSource.Save(docStream);
			docStream.Position = 0;

			var outputStream = new MemoryStream
			{
				Position = 0
			};

			var xPathDocument = new XPathDocument(docStream);
			var xPathNavigator = xPathDocument.CreateNavigator();

			var xsltArgList = new XsltArgumentList();
			NamespaceXslTransform.Transform(xPathNavigator, xsltArgList, outputStream);

			outputStream.Position = 0;
			xDocumentSource = XDocument.Load(outputStream);
		}

		public static void RemoveAllNamespaces(ref XElement xElementSource)
		{
			var docStream = new MemoryStream();
			xElementSource.Save(docStream);
			docStream.Position = 0;

			var outputStream = new MemoryStream
			{
				Position = 0
			};

			var xPathDocument = new XPathDocument(docStream);
			var xPathNavigator = xPathDocument.CreateNavigator();

			var xsltArgList = new XsltArgumentList();
			NamespaceXslTransform.Transform(xPathNavigator, xsltArgList, outputStream);

			outputStream.Position = 0;
			xElementSource = XDocument.Load(outputStream).Root;
		}

		public static XDocument RemoveAllNamespaces(XDocument xDocumentSource)
		{
			var docStream = new MemoryStream();
			xDocumentSource.Save(docStream);
			docStream.Position = 0;

			var outputStream = new MemoryStream
			{
				Position = 0
			};

			var xPathDocument = new XPathDocument(docStream);
			var xPathNavigator = xPathDocument.CreateNavigator();

			var xsltArgList = new XsltArgumentList();
			NamespaceXslTransform.Transform(xPathNavigator, xsltArgList, outputStream);

			outputStream.Position = 0;
			return XDocument.Load(outputStream);
		}

		public static XElement RemoveAllNamespaces(XElement xElementSource)
		{
			var docStream = new MemoryStream();
			xElementSource.Save(docStream);
			docStream.Position = 0;

			var outputStream = new MemoryStream
			{
				Position = 0
			};

			var xPathDocument = new XPathDocument(docStream);
			var xPathNavigator = xPathDocument.CreateNavigator();

			var xsltArgList = new XsltArgumentList();
			NamespaceXslTransform.Transform(xPathNavigator, xsltArgList, outputStream);

			outputStream.Position = 0;
			var finalDocument = XDocument.Load(outputStream);

			var root = finalDocument.Root;

			return root;
		}
	}
}