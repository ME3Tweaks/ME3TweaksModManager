using System;
using System.Collections.Generic;
using System.IO;

namespace Gammtek.Conduit.IO
{
	public class MemoryTributary : Stream
	{
		protected long BlockSize = 65536;
		protected List<byte[]> Blocks = new List<byte[]>();
		private long _length;

		public MemoryTributary()
			: this(null) {}

		public MemoryTributary(byte[] source)
		{
			InitStream(source);
		}

		public MemoryTributary(int length)
		{
			InitStream(length);
		}

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
			get { return true; }
		}

		public override long Length
		{
			get { return _length; }
		}

		public override long Position { get; set; }

		protected byte[] Block
		{
			get
			{
				while (Blocks.Count <= BlockId)
				{
					Blocks.Add(new byte[BlockSize]);
				}
				return Blocks[(int) BlockId];
			}
		}

		protected long BlockId
		{
			get { return Position / BlockSize; }
		}

		protected long BlockOffset
		{
			get { return Position % BlockSize; }
		}

		private void InitStream(int length)
		{
			if (length < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(length));
			}

			SetLength(length);
			Position = length;
			var d = Block;
			Position = 0;
		}

		private void InitStream(byte[] source = null)
		{
			if (source != null)
			{
				Write(source, 0, source.Length);
			}

			Position = 0;
		}

		public override void Flush() {}

		public override int Read(byte[] buffer, int offset, int count)
		{
			long lcount = count;

			if (lcount < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count), lcount, "Number of bytes to copy cannot be negative.");
			}

			var remaining = (_length - Position);
			if (lcount > remaining)
			{
				lcount = remaining;
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer), "Buffer cannot be null.");
			}
			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(offset), offset, "Destination offset cannot be negative.");
			}

			var read = 0;
			do
			{
				var copysize = Math.Min(lcount, (BlockSize - BlockOffset));
				Buffer.BlockCopy(Block, (int) BlockOffset, buffer, offset, (int) copysize);
				lcount -= copysize;
				offset += (int) copysize;

				read += (int) copysize;
				Position += copysize;
			} while (lcount > 0);

			return read;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin:
					Position = offset;
					break;
				case SeekOrigin.Current:
					Position += offset;
					break;
				case SeekOrigin.End:
					Position = Length - offset;
					break;
			}
			return Position;
		}

		public override void SetLength(long value)
		{
			_length = value;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			var initialPosition = Position;
			try
			{
				do
				{
					var copysize = Math.Min(count, (int) (BlockSize - BlockOffset));

					EnsureCapacity(Position + copysize);

					Buffer.BlockCopy(buffer, offset, Block, (int) BlockOffset, copysize);
					count -= copysize;
					offset += copysize;

					Position += copysize;
				} while (count > 0);
			}
			catch (Exception)
			{
				Position = initialPosition;
				throw;
			}
		}

		public override int ReadByte()
		{
			if (Position >= _length)
			{
				return -1;
			}

			var b = Block[BlockOffset];
			Position++;

			return b;
		}

		public override void WriteByte(byte value)
		{
			EnsureCapacity(Position + 1);
			Block[BlockOffset] = value;
			Position++;
		}

		protected void EnsureCapacity(long intendedLength)
		{
			if (intendedLength > _length)
			{
				_length = (intendedLength);
			}
		}

		public byte[] ToArray()
		{
			var firstposition = Position;
			Position = 0;
			var destination = new byte[Length];
			Read(destination, 0, (int) Length);
			Position = firstposition;
			return destination;
		}

		public void ReadFrom(Stream source, long length)
		{
			var buffer = new byte[4096];
			do
			{
				var read = source.Read(buffer, 0, (int) Math.Min(4096, length));
				length -= read;
				Write(buffer, 0, read);
			} while (length > 0);
		}

		public void WriteTo(Stream destination)
		{
			var initialpos = Position;
			Position = 0;
			CopyTo(destination);
			Position = initialpos;
		}
	}
}
