using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gammtek.Conduit.IO
{
	public class MultiStream : Stream
	{
		private readonly List<Stream> _streamList = new List<Stream>();
		private long _position;

		public override bool CanRead
		{
			get { return true; }
		}

		public override bool CanSeek
		{
			get { return true; }
		}

		public override bool CanWrite
		{
			get { return false; }
		}

		public override long Length
		{
			get
			{
				return _streamList.Sum(stream => stream.Length);
			}
		}

		public override long Position
		{
			get { return _position; }
			set { Seek(value, SeekOrigin.Begin); }
		}

		public override void Flush() {}

		public override long Seek(long offset, SeekOrigin origin)
		{
			var len = Length;
			switch (origin)
			{
				case SeekOrigin.Begin:
					_position = offset;
					break;
				case SeekOrigin.Current:
					_position += offset;
					break;
				case SeekOrigin.End:
					_position = len - offset;
					break;
			}
			if (_position > len)
			{
				_position = len;
			}
			else if (_position < 0)
			{
				_position = 0;
			}
			return _position;
		}

		public override void SetLength(long value) {}

		public void AddStream(Stream stream)
		{
			_streamList.Add(stream);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			long len = 0;
			var result = 0;
			var bufPos = offset;

			foreach (var stream in _streamList)
			{
				if (_position < (len + stream.Length))
				{
					stream.Position = _position - len;

					var bytesRead = stream.Read(buffer, bufPos, count);

					result += bytesRead;
					bufPos += bytesRead;
					_position += bytesRead;

					if (bytesRead < count)
					{
						count -= bytesRead;
					}
					else
					{
						break;
					}
				}

				len += stream.Length;
			}

			return result;
		}

		public override void Write(byte[] buffer, int offset, int count) {}
	}
}
