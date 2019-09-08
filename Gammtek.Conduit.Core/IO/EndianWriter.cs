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
using System.Text;

namespace Gammtek.Conduit.IO
{
	/// <summary>
	///     A BinaryWriter implementation to write individual bits to a stream
	///     and support writing data types for platforms with different Endian
	///     configurations.
	///     Note: for compatibility with streams written with BinaryWriter,
	///     character data uses the provided encoding and does not perform any
	///     Endian swapping with the data.
	/// </summary>
	public class EndianWriter : BinaryWriter
	{
		private readonly BinaryWriter _target;

		/// <summary>
		///     Creates an EndianWriter using the given <paramref name="target" /> BinaryWriter.
		/// </summary>
		public EndianWriter(BinaryWriter target)
		{
			_target = target;
			Endian = BitConverter.IsLittleEndian ? Endian.Little : Endian.Big;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="EndianWriter" /> class
		///     with the underlying <paramref name="stream" /> and <paramref name="encoding" />.
		/// </summary>
		public EndianWriter(Stream stream, Encoding encoding)
			: this(new BinaryWriter(stream, encoding)) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="EndianWriter" /> class
		///     with the underlying <paramref name="stream" /> and default encoding (UTF8).
		/// </summary>
		public EndianWriter(Stream stream)
			: this(stream, Encoding.UTF8) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="EndianWriter" /> class.
		/// </summary>
		public EndianWriter()
			: this(null, Encoding.UTF8) {}

		/// <summary>
		///     The Endian setting of the stream.
		/// </summary>
		public Endian Endian { get; set; }

		/// <summary>
		///     Clears all buffers for the current writer and causes any buffered data to be written to the underlying device.
		/// </summary>
		public override void Flush()
		{
			_target.Flush();
		}

		/// <summary>
		///     Sets the position within the current stream.
		/// </summary>
		/// <returns>
		///     The position with the current stream.
		/// </returns>
		public override long Seek(int offset, SeekOrigin origin)
		{
			return _target.Seek(offset, origin);
		}

		/// <summary>
		///     Writes a one-byte Boolean value to the current stream, with 0 representing false and 1 representing true.
		/// </summary>
		public override void Write(bool value)
		{
			_target.Write(value);
		}

		/// <summary>
		///     Writes one byte to the current stream.
		/// </summary>
		public override void Write(byte value)
		{
			_target.Write(value);
		}

		/// <summary>
		///     Writes the bytes in the buffer to the stream.
		/// </summary>
		public override void Write(byte[] buffer)
		{
			_target.Write(buffer);
		}

		/// <summary>
		///     Writes a region of a byte array to the current stream.
		/// </summary>
		public override void Write(byte[] buffer, int index, int count)
		{
			_target.Write(buffer, index, count);
		}

		/// <summary>
		///     Writes a character to the stream using the provided encoding.
		/// </summary>
		public override void Write(char ch)
		{
			_target.Write(ch);
		}

		/// <summary>
		///     Writes an array of characters to the stream using the provided encoding.
		/// </summary>
		public override void Write(char[] chars)
		{
			_target.Write(chars);
		}

		/// <summary>
		///     Writes a region of a character array to the current stream.
		/// </summary>
		public override void Write(char[] chars, int index, int count)
		{
			_target.Write(chars, index, count);
		}

		/// <summary>
		///     Writes an eight byte double value to the stream in the target Endian format.
		/// </summary>
		public override void Write(double value)
		{
			_target.Write(Endian.Native.To(Endian).Convert(value));
		}

		/// <summary>
		///     Writes a four byte float value to the stream in the target Endian format.
		/// </summary>
		public override void Write(float value)
		{
			_target.Write(Endian.Native.To(Endian).Convert(value));
		}

		/// <summary>
		///     Writes a four byte signed integer value to the stream in the target Endian format.
		/// </summary>
		public override void Write(int value)
		{
			_target.Write(Endian.Native.To(Endian).Convert(value));
		}

		/// <summary>
		///     Writes an eight byte signed integer value to the stream in the target Endian format.
		/// </summary>
		public override void Write(long value)
		{
			_target.Write(Endian.Native.To(Endian).Convert(value));
		}

		/// <summary>
		///     Writes a one byte signed byte value to the stream.
		/// </summary>
		public override void Write(sbyte value)
		{
			_target.Write((byte) value);
		}

		/// <summary>
		///     Writes a two byte signed integer value to the stream in the target Endian format.
		/// </summary>
		public override void Write(short value)
		{
			_target.Write(Endian.Native.To(Endian).Convert(value));
		}

		/// <summary>
		///     Writes a string to the stream using the provided encoding and a variable integer
		///     length marker.
		/// </summary>
		public override void Write(string value)
		{
			_target.Write(value);
		}

		/// <summary>
		///     Writes a four byte unsigned integer value to the stream in the target Endian format.
		/// </summary>
		public override void Write(uint value)
		{
			_target.Write(Endian.Native.To(Endian).Convert(value));
		}

		/// <summary>
		///     Writes an eight byte unsigned integer value to the stream in the target Endian format.
		/// </summary>
		public override void Write(ulong value)
		{
			_target.Write(Endian.Native.To(Endian).Convert(value));
		}

		/// <summary>
		///     Writes a two byte unsigned integer value to the stream in the target Endian format.
		/// </summary>
		public override void Write(ushort value)
		{
			_target.Write(Endian.Native.To(Endian).Convert(value));
		}
	}
}
