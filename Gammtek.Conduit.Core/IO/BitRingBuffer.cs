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

namespace Gammtek.Conduit.IO
{
	/// <summary>
	///     Ring buffer to read and write bits and bytes.
	/// </summary>
	public class BitRingBuffer
	{
		private readonly byte[] _buffer;
		private int _bitReadPosition;
		private int _bitWritePosition;
		private bool _full;

		/// <summary>
		///     Initializes a new instance of the <see cref="BitRingBuffer" /> class
		///     with a default buffer size of 256 bytes.
		/// </summary>
		public BitRingBuffer()
			: this(0x100) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitRingBuffer" /> class
		///     with a buffer size of <paramref name="bufferSize" /> bytes.
		/// </summary>
		public BitRingBuffer(int bufferSize)
		{
			_buffer = new byte[bufferSize];
			_bitWritePosition = 0;
			_bitReadPosition = 0;
			_full = false;
		}

		/// <summary>
		///     The number of bits that can be written to the buffer.
		/// </summary>
		public int AvailableBits => _buffer.Length * 8 - LengthBits;

		/// <summary>
		///     The number of bytes that can be written to the buffer.
		/// </summary>
		public int AvailableBytes => AvailableBits >> 3;

		/// <summary>
		///     The number of bytes in the buffer.
		/// </summary>
		public int BufferSize => _buffer.Length;

		/// <summary>
		///     The number of bits to read in the buffer.
		/// </summary>
		public int LengthBits => _full
			? _buffer.Length * 8
			: (_bitWritePosition < _bitReadPosition
				? _bitWritePosition + (_buffer.Length * 8) - _bitReadPosition
				: _bitWritePosition - _bitReadPosition);

		/// <summary>
		///     The number of full bytes to read in the buffer.
		/// </summary>
		public int LengthBytes => LengthBits >> 3;

		/// <summary>
		///     The total number of bytes containing relevant bits.
		/// </summary>
		public int UsedBytes => (LengthBits + 7) >> 3;

		private bool this[int i]
		{
			get { return (_buffer[i >> 3] & (1 << (7 - (i % 8)))) != 0; }
			set
			{
				if (value)
				{
					_buffer[i >> 3] |= (byte) (1 << (7 - (i % 8)));
				}
				else
				{
					_buffer[i >> 3] &= (byte) ~(1 << (7 - (i % 8)));
				}
			}
		}

		/// <summary>
		///     Clear all data in the buffer.
		/// </summary>
		public void Clear()
		{
			_bitReadPosition = _bitWritePosition = 0;
			_full = false;
		}

		/// <summary>
		///     Reads a single bit from the stream.
		/// </summary>
		public bool ReadBoolean()
		{
			if (LengthBits == 0)
			{
				throw new Exception("No data");
			}

			_full = false;

			var result = this[_bitReadPosition];

			_bitReadPosition = (_bitReadPosition + 1) % (_buffer.Length * 8);

			return result;
		}

		/// <summary>
		///     Reads a byte from the stream.
		/// </summary>
		public byte ReadByte()
		{
			byte result = 0;

			for (var i = 7; i >= 0; --i)
			{
				result |= (byte) (ReadBoolean() ? (1 << i) : 0);
			}

			return result;
		}

		/// <summary>
		///     Reads <paramref name="count" /> bytes into <paramref name="buffer" />
		///     starting at <paramref name="index" />.
		/// </summary>
		public void ReadBytes(byte[] buffer, int index, int count)
		{
			for (var i = 0; i < count; ++i)
			{
				buffer[index + i] = ReadByte();
			}
		}

		/// <summary>
		///     Writes the <paramref name="value" /> as a bit in the buffer.
		/// </summary>
		public void Write(bool value)
		{
			if (AvailableBits <= 0)
			{
				throw new IndexOutOfRangeException("Buffer is full!");
			}

			this[_bitWritePosition] = value;

			_bitWritePosition = (_bitWritePosition + 1) % (_buffer.Length * 8);
			_full = _bitWritePosition == _bitReadPosition;
		}

		/// <summary>
		///     Writes the <paramref name="value" /> to the buffer.
		/// </summary>
		public void Write(byte value)
		{
			for (var i = 7; i >= 0; --i)
			{
				Write((value & (1 << i)) != 0);
			}
		}

		/// <summary>
		///     Writes <paramref name="count" /> bytes from  <paramref name="buffer" />
		///     starting at <paramref name="index" />.
		/// </summary>
		public void Write(byte[] buffer, int index, int count)
		{
			for (var i = 0; i < count; ++i)
			{
				Write(buffer[index + i]);
			}
		}
	}
}
