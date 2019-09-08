using System;
using System.Xml.Linq;
using Gammtek.Conduit.Xml.Linq;

namespace Gammtek.Conduit.Extensions.Xml.Linq
{
	public static class XObjectExtensions
	{
		/// <summary>
		///     Gets the first annotation object of the specified type, or creates a new one.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="self"></param>
		/// <returns></returns>
		public static T GetOrAddAnnotation<T>(this XObject self)
			where T : class, new()
		{
			if (self == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(self));
			}

			var value = self.Annotation<T>();

			if (value == null)
			{
				self.AddAnnotation(value = new T());
			}

			return value;
		}

		/// <summary>
		///     Gets the first annotation object of the specified type, or creates a new one.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="self"></param>
		/// <param name="create"></param>
		/// <returns></returns>
		public static T GetOrAddAnnotation<T>(this XObject self, Func<T> create)
			where T : class
		{
			if (self == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(self));
			}

			if (create == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(create));
			}

			var value = self.Annotation<T>();

			if (value == null)
			{
				self.AddAnnotation(value = create());
			}

			return value;
		}

		/// <summary>
		///     Gets the first annotation object of the specified type, or creates a new one.
		/// </summary>
		/// <param name="self"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object GetOrAddAnnotation(this XObject self, Type type)
		{
			if (self == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(self));
			}

			if (type == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(type));
			}

			var value = self.Annotation(type);

			if (value == null)
			{
				self.AddAnnotation(value = Activator.CreateInstance(type));
			}

			return value;
		}

		/// <summary>
		///     Gets the first annotation object of the specified type, or creates a new one.
		/// </summary>
		/// <param name="self"></param>
		/// <param name="type"></param>
		/// <param name="create"></param>
		/// <returns></returns>
		public static object GetOrAddAnnotation(this XObject self, Type type, Func<object> create)
		{
			if (self == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(self));
			}

			if (type == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(type));
			}

			if (create == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(create));
			}

			var value = self.Annotation(type);

			if (value == null)
			{
				self.AddAnnotation(value = create());
			}

			return value;
		}

		/// <summary>
		///     Gets the BaseUri of the <see cref="XObject" />.
		/// </summary>
		/// <param name="self"></param>
		/// <returns></returns>
		public static Uri GetXmlBaseUri(this XObject self)
		{
			if (self == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(self));
			}

			var baseUriAnno = self.Annotation<XmlBaseUriAnnotation>();

			if (baseUriAnno?.BaseUri != null)
			{
				return baseUriAnno.BaseUri;
			}

			var element = self as XElement;

			return element != null ? GetXmlBaseUri(element) : null;
		}

		/// <summary>
		///     Gets the BaseUri of the <see cref="XElement" />.
		/// </summary>
		/// <param name="self"></param>
		/// <returns></returns>
		public static Uri GetXmlBaseUri(this XElement self)
		{
			if (self == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(self));
			}

			var baseUriAttr = (string)self.Attribute(XNamespace.Xml + "base");

			if (baseUriAttr != null)
			{
				return new Uri(baseUriAttr, UriKind.RelativeOrAbsolute);
			}

			if (self.Parent != null)
			{
				return GetXmlBaseUri(self.Parent);
			}

			return self.Document != null ? GetXmlBaseUri(self.Document) : null;
		}

		/// <summary>
		///     Sets the BaseUri of the <see cref="XObject" />.
		/// </summary>
		/// <param name="self"></param>
		/// <param name="baseUri"></param>
		public static void SetXmlBaseUri(this XObject self, Uri baseUri)
		{
			if (self == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(self));
			}

			if (baseUri != null)
			{
				self.GetOrAddAnnotation<XmlBaseUriAnnotation>().BaseUri = baseUri;
			}
			else
			{
				self.RemoveAnnotations<XmlBaseUriAnnotation>();
			}
		}

		/// <summary>
		///     Sets the BaseUri of the <see cref="XObject" />.
		/// </summary>
		/// <param name="self"></param>
		/// <param name="baseUri"></param>
		public static void SetXmlBaseUri(this XObject self, string baseUri)
		{
			if (self == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(self));
			}

			SetXmlBaseUri(self, !string.IsNullOrWhiteSpace(baseUri) ? new Uri(baseUri) : null);
		}
	}
}
