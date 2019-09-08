using System;

namespace Gammtek.Conduit.Xml.Linq
{
	public class XBaseUriAnnotation
	{
		private Uri _baseUri;

		public XBaseUriAnnotation(Uri baseUri)
		{
			if (baseUri == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(baseUri));
			}

			BaseUri = baseUri;
		}

		public XBaseUriAnnotation(string baseUri)
		{
			if (baseUri == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(baseUri));
			}

			BaseUri = new Uri(baseUri);
		}

		public Uri BaseUri
		{
			get { return _baseUri; }
			set
			{
				if (value == null)
				{
					ThrowHelper.ThrowArgumentNullException(nameof(value));
				}

				_baseUri = value;
			}
		}

		public override string ToString()
		{
			return BaseUri.ToString();
		}
	}
}
