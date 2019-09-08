using System.Xml.Linq;

namespace Gammtek.Conduit.Xml.Linq
{
	public class XIncludeElement : XElement
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="T:System.Xml.Linq.XElement" /> class with the specified name.
		/// </summary>
		/// <param name="name">An <see cref="T:System.Xml.Linq.XName" /> that contains the name of the element.</param>
		public XIncludeElement([NotNull] XName name)
			: base(name) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:System.Xml.Linq.XElement" /> class with the specified name and content.
		/// </summary>
		/// <param name="name">An <see cref="T:System.Xml.Linq.XName" /> that contains the element name.</param>
		/// <param name="content">The contents of the element.</param>
		public XIncludeElement([NotNull] XName name, object content)
			: base(name, content) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:System.Xml.Linq.XElement" /> class with the specified name and content.
		/// </summary>
		/// <param name="name">An <see cref="T:System.Xml.Linq.XName" /> that contains the element name.</param>
		/// <param name="content">The initial content of the element.</param>
		public XIncludeElement([NotNull] XName name, params object[] content)
			: base(name, content) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:System.Xml.Linq.XElement" /> class from another <see cref="T:System.Xml.Linq.XElement" /> object.
		/// </summary>
		/// <param name="other">An <see cref="T:System.Xml.Linq.XElement" /> object to copy from.</param>
		public XIncludeElement([NotNull] XElement other)
			: base(other) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:System.Xml.Linq.XElement" /> class from an <see cref="T:System.Xml.Linq.XStreamingElement" />
		///     object.
		/// </summary>
		/// <param name="other">
		///     An <see cref="T:System.Xml.Linq.XStreamingElement" /> that contains unevaluated queries that will be iterated for the contents of
		///     this <see cref="T:System.Xml.Linq.XElement" />.
		/// </param>
		public XIncludeElement([NotNull] XStreamingElement other)
			: base(other) {}
	}
}
