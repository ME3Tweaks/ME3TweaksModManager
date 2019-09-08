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
	public abstract class BitStream : Stream
	{
		private readonly bool _leaveOpen;

		/// <summary>
		///     Initializes a new instance of the <see cref="BitStream" /> class.
		/// </summary>
		protected BitStream()
			: this(null, false) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitStream" /> class
		///     with backing <paramref name="stream" />.  The stream will be automatically
		///     closed when this stream is closed.
		/// </summary>
		protected BitStream(Stream stream)
			: this(stream, false) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitStream" /> class
		///     with backing <paramref name="stream" />.  The stream will be closed only if
		///     <paramref name="leaveOpen" /> is false.
		/// </summary>
		protected BitStream(Stream stream, bool leaveOpen)
		{
			BaseStream = stream;
			_leaveOpen = leaveOpen;
			BitBuffer = new BitRingBuffer();
			TemporaryBuffer = new byte[0x100];
		}

		/// <summary>
		///     The number of bits in the stream.
		/// </summary>
		public abstract long BitLength { get; }

		/// <summary>
		///     The current bit position in the stream.
		/// </summary>
		public abstract long BitPosition { get; }

		/// <summary>
		///     The base stream being read/written.
		/// </summary>
		protected Stream BaseStream { get; }

		/// <summary>
		///     The ring buffer for managing bit level reads and writes.
		/// </summary>
		protected BitRingBuffer BitBuffer { get; }

		/// <summary>
		///     A temporary buffer for conversions.
		/// </summary>
		protected byte[] TemporaryBuffer { get; }

		/// <summary>
		///     When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written
		///     to the underlying device.
		/// </summary>
		public override void Flush()
		{
			InternalFlushBuffer(true);

			BaseStream.Flush();
		}

		/// <summary>
		///     Reads the a boolean as a single bit from the stream.
		/// </summary>
		public abstract bool ReadBoolean();

		/// <summary>
		///     Writes a bit to the stream.
		/// </summary>
		public abstract void Write(bool value);

		/// <summary>
		///     Disposes the base stream if set and leaveOpen is false.
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (!_leaveOpen)
			{
				BaseStream?.Dispose();
			}
		}

		/// <summary>
		///     Flushes the internal buffer to disk.
		/// </summary>
		protected void InternalFlushBuffer(bool flushAllBits)
		{
			if (flushAllBits)
			{
				// Add bits until we can write out all bits during the flush.
				var bitsToFlush = (8 - (BitBuffer.LengthBits % 8)) % 8;

				for (var i = 0; i < bitsToFlush; ++i)
				{
					BitBuffer.Write(false);
				}
			}

			var toTransfer = Math.Min(TemporaryBuffer.Length, BitBuffer.LengthBytes);

			BitBuffer.ReadBytes(TemporaryBuffer, 0, toTransfer);
			BaseStream.Write(TemporaryBuffer, 0, toTransfer);
		}
	}
}
