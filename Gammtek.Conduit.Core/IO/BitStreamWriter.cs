/*	Copyright 2012 Brent Scriver

	Licensed under the Apache License, Version 2.0 (the "License");
	you may not use this file except in compliance with the License.
	You may obtain a copy of the License at

		http://www.apache.org/licenses/LICENSE-2.0

	Unless required by applicable law or agreed to in writing, software
	distributed under the License is distributed on an "AS IS" BASIS,
	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	See the License for the specific language governing permissions and
	limitations under the License.
*/

using System;
using System.IO;

namespace Gammtek.Conduit.IO
{
	/// <summary>
	///     Stream class for writing individual bits to a stream.
	///     Packs subsequent bytes written to the stream.
	/// </summary>
	public class BitStreamWriter : BitStream
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="BitStreamWriter" /> class.
		/// </summary>
		public BitStreamWriter()
			: base(null, false) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitStreamWriter" /> class
		///     with backing <paramref name="stream" />.  The stream will be automatically
		///     closed when this stream is closed.
		/// </summary>
		public BitStreamWriter(Stream stream)
			: base(stream, false)
		{
			if (stream != null && !stream.CanWrite)
			{
				throw new ArgumentException(@"The stream must support writing!", nameof(stream));
			}
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitStreamWriter" /> class
		///     with backing <paramref name="stream" />.  The stream will be closed only if
		///     <paramref name="leaveOpen" /> is false.
		/// </summary>
		public BitStreamWriter(Stream stream, bool leaveOpen)
			: base(stream, leaveOpen)
		{
			if (stream != null && !stream.CanWrite)
			{
				throw new ArgumentException(@"The stream must support writing!", nameof(stream));
			}
		}

		/// <summary>
		///     The number of bits in the stream.
		/// </summary>
		public override long BitLength => Math.Max(BaseStream.Length * 8, BitPosition);

		/// <summary>
		///     The current bit position in the stream.
		/// </summary>
		public override long BitPosition => BaseStream.Position * 8 + BitBuffer.LengthBits;

		/// <summary>
		///     When overridden in a derived class, gets a value indicating whether the current stream supports reading.
		/// </summary>
		/// <returns>
		///     true if the stream supports reading; otherwise, false.
		/// </returns>
		/// <value>
		///     <c>true</c> if this instance can read; otherwise, <c>false</c>.
		/// </value>
		public override bool CanRead => false;

		/// <summary>
		///     When overridden in a derived class, gets a value indicating whether the current stream supports seeking.
		/// </summary>
		/// <returns>
		///     true if the stream supports seeking; otherwise, false.
		/// </returns>
		/// <value>
		///     <c>true</c> if this instance can seek; otherwise, <c>false</c>.
		/// </value>
		public override bool CanSeek => BaseStream.CanSeek;

		/// <summary>
		///     Gets a value that determines whether the current stream can time out.
		/// </summary>
		/// <returns>
		///     A value that determines whether the current stream can time out.
		/// </returns>
		/// <value>
		///     <c>true</c> if this instance can timeout; otherwise, <c>false</c>.
		/// </value>
		public override bool CanTimeout => BaseStream.CanTimeout;

		/// <summary>
		///     When overridden in a derived class, gets a value indicating whether the current stream supports writing.
		/// </summary>
		/// <returns>
		///     true if the stream supports writing; otherwise, false.
		/// </returns>
		/// <value>
		///     <c>true</c> if this instance can write; otherwise, <c>false</c>.
		/// </value>
		public override bool CanWrite => BaseStream.CanWrite;

		/// <summary>
		///     When overridden in a derived class, gets the length in bytes of the stream.
		/// </summary>
		/// <returns>
		///     A long value representing the length of the stream in bytes.
		/// </returns>
		/// <value>
		///     The length.
		/// </value>
		public override long Length => Math.Max(BaseStream.Length, Position + BitBuffer.UsedBytes - BitBuffer.LengthBytes);

		/// <summary>
		///     When overridden in a derived class, gets or sets the position within the current stream.
		/// </summary>
		/// <returns>
		///     The current position within the stream.
		/// </returns>
		/// <value>
		///     The position.
		/// </value>
		public override long Position
		{
			get { return BaseStream.Position + BitBuffer.LengthBytes; }
			set { Seek(value, SeekOrigin.Begin); }
		}

		/// <summary>
		///     Gets or sets a value, in miliseconds, that determines how long the stream will attempt to read before timing out.
		/// </summary>
		/// <returns>
		///     A value, in miliseconds, that determines how long the stream will attempt to read before timing out.
		/// </returns>
		/// <value>
		///     The read timeout.
		/// </value>
		public override int ReadTimeout
		{
			get { return BaseStream.ReadTimeout; }
			set { BaseStream.ReadTimeout = value; }
		}

		/// <summary>
		///     Gets or sets a value, in miliseconds, that determines how long the stream will attempt to write before timing out.
		/// </summary>
		/// <returns>
		///     A value, in miliseconds, that determines how long the stream will attempt to write before timing out.
		/// </returns>
		/// <value>
		///     The write timeout.
		/// </value>
		public override int WriteTimeout
		{
			get { return BaseStream.WriteTimeout; }
			set { BaseStream.WriteTimeout = value; }
		}

		/// <summary>
		///     When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position
		///     within the stream by the number of bytes read.
		/// </summary>
		/// <returns>
		///     The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many
		///     bytes are not currently available, or zero (0) if the end of the stream has been reached.
		/// </returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		///     Reads the a boolean as a single bit from the stream.
		/// </summary>
		public override bool ReadBoolean()
		{
			throw new NotSupportedException();
		}

		/// <summary>
		///     Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end
		///     of the stream.
		/// </summary>
		/// <returns>
		///     The unsigned byte cast to an Int32, or -1 if at the end of the stream.
		/// </returns>
		public override int ReadByte()
		{
			throw new NotSupportedException();
		}

		/// <summary>
		///     When overridden in a derived class, sets the position within the current stream.
		/// </summary>
		/// <returns>
		///     The new position within the current stream.
		/// </returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			InternalFlushBuffer(true);

			return BaseStream.Seek(offset, origin);
		}

		/// <summary>
		///     When overridden in a derived class, sets the length of the current stream.
		/// </summary>
		/// <param name='value'>
		///     The desired length of the current stream in bytes.
		/// </param>
		public override void SetLength(long value)
		{
			InternalFlushBuffer(true);

			BaseStream.SetLength(value);
		}

		/// <summary>
		///     When overridden in a derived class, writes a sequence of bytes to the current stream and advances the current
		///     position within this stream by the number of bytes written.
		/// </summary>
		public override void Write(byte[] buffer, int offset, int count)
		{
			var transferred = 0;

			while (transferred < count)
			{
				if (BitBuffer.AvailableBytes == 0)
				{
					InternalFlushBuffer(false);
				}

				var toTransfer = Math.Min(count, BitBuffer.AvailableBytes);

				BitBuffer.Write(buffer, offset, toTransfer);

				offset += toTransfer;
				transferred += toTransfer;
			}
		}

		/// <summary>
		///     Writes a bit to the stream.
		/// </summary>
		public override void Write(bool value)
		{
			if (BitBuffer.AvailableBits == 0)
			{
				InternalFlushBuffer(false);
			}

			BitBuffer.Write(value);
		}

		/// <summary>
		///     Writes a byte to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name='value'>
		///     The byte to write to the stream.
		/// </param>
		public override void WriteByte(byte value)
		{
			if (BitBuffer.AvailableBytes == 0)
			{
				InternalFlushBuffer(false);
			}

			base.WriteByte(value);
		}

		/// <summary>
		///     Flushes the buffer and optionally closes the base stream.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			InternalFlushBuffer(true);

			base.Dispose(disposing);
		}
	}
}
