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
	///     BinaryReader that supports reading and writing individual bits from
	///     the stream and reordering values based on Endian settings between
	///     the system and the stream.
	/// </summary>
	public class EndianReader : BinaryReader
	{
		private readonly BinaryReader _source;

		/// <summary>
		///     Initializes a new instance of the <see cref="EndianReader" /> class
		///     using the <paramref name="source" /> BinaryReader.
		/// </summary>
		public EndianReader(BinaryReader source)
			: base(source.BaseStream, Encoding.UTF8)
		{
			_source = source;
			Endian = BitConverter.IsLittleEndian ? Endian.Little : Endian.Big;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="EndianReader" /> class.
		/// </summary>
		public EndianReader(Stream input, Encoding encoding)
			: base(input, encoding)
		{
			_source = new BinaryReader(input, encoding);
			Endian = BitConverter.IsLittleEndian ? Endian.Little : Endian.Big;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="EndianReader" /> class
		///     using stream <paramref name="input" />.
		/// </summary>
		public EndianReader(Stream input)
			: this(input, Encoding.UTF8) {}

		/// <summary>
		///     The Endian setting of the stream.
		/// </summary>
		public Endian Endian { get; set; }

		/// <summary>
		///     Returns the next available character and does not advance the byte or character position.
		/// </summary>
		/// <returns>
		///     The next available character, or -1 if no more characters are available or the stream does not support seeking.
		/// </returns>
		public override int PeekChar()
		{
			return _source.PeekChar();
		}

		/// <summary>
		///     Reads characters from the underlying stream and advances the current position of the stream in accordance with the
		///     Encoding used and the specific character being read from the stream.
		/// </summary>
		/// <returns>
		///     The next character from the input stream, or -1 if no characters are currently available.
		/// </returns>
		public override int Read()
		{
			return _source.Read();
		}

		/// <summary>
		///     Reads the specified number of bytes from the stream, starting from a specified point in the byte array.
		/// </summary>
		public override int Read(byte[] buffer, int index, int count)
		{
			return _source.Read(buffer, index, count);
		}

		/// <summary>
		///     Reads the specified number of characters from the stream, starting from a specified point in the byte array.
		/// </summary>
		public override int Read(char[] buffer, int index, int count)
		{
			return _source.Read(buffer, index, count);
		}

		/// <summary>
		///     Reads a Boolean value from the current stream and advances the current position of the stream by one bit.
		/// </summary>
		/// <returns>
		///     true if the bit is nonzero; otherwise, false.
		/// </returns>
		public override bool ReadBoolean()
		{
			return _source.ReadBoolean();
		}

		/// <summary>
		///     Reads the next byte from the current stream and advances the current position of the stream by one byte.
		/// </summary>
		/// <returns>
		///     The next byte read from the current stream.
		/// </returns>
		public override byte ReadByte()
		{
			return _source.ReadByte();
		}

		/// <summary>
		///     Reads the specified number of bytes from the current stream into a byte array and advances the current position by
		///     that number of bytes.
		/// </summary>
		/// <returns>
		///     A byte array containing data read from the underlying stream. This might be less than the number of bytes
		///     requested if the end of the stream is reached.
		/// </returns>
		/// <param name='count'>
		///     The number of bytes to read.
		/// </param>
		public override byte[] ReadBytes(int count)
		{
			return _source.ReadBytes(count);
		}

		/// <summary>
		///     Reads the next character from the current stream and advances the current position of the stream in accordance
		///     with the Encoding used and the specific character being read from the stream.
		/// </summary>
		/// <returns>
		///     A character read from the current stream.
		/// </returns>
		public override char ReadChar()
		{
			return _source.ReadChar();
		}

		/// <summary>
		///     Reads the specified number of characters from the current stream, returns the data in a character array, and
		///     advances the current position in accordance with the Encoding used and the specific character being read from the stream.
		/// </summary>
		/// <returns>
		///     A character array containing data read from the underlying stream. This might be less than the number of
		///     characters requested if the end of the stream is reached.
		/// </returns>
		/// <param name='count'>
		///     The number of characters to read.
		/// </param>
		public override char[] ReadChars(int count)
		{
			return _source.ReadChars(count);
		}

		/// <summary>
		///     Reads an 8-byte floating point value from the current stream and advances the current position of the stream by
		///     eight bytes.
		/// </summary>
		/// <returns>
		///     An 8-byte floating point value read from the current stream.
		/// </returns>
		public override double ReadDouble()
		{
			return Endian.To(Endian.Native).Convert(_source.ReadDouble());
		}

		/// <summary>
		///     Reads a 2-byte signed integer from the current stream and advances the current position of the stream by two bytes.
		/// </summary>
		/// <returns>
		///     A 2-byte signed integer read from the current stream.
		/// </returns>
		public override short ReadInt16()
		{
			return Endian.To(Endian.Native).Convert(_source.ReadInt16());
		}

		/// <summary>
		///     Reads a 4-byte signed integer from the current stream and advances the current position of the stream by four bytes.
		/// </summary>
		/// <returns>
		///     A 4-byte signed integer read from the current stream.
		/// </returns>
		public override int ReadInt32()
		{
			return Endian.To(Endian.Native).Convert(_source.ReadInt32());
		}

		/// <summary>
		///     Reads an 8-byte signed integer from the current stream and advances the current position of the stream by eight bytes.
		/// </summary>
		/// <returns>
		///     An 8-byte signed integer read from the current stream.
		/// </returns>
		public override long ReadInt64()
		{
			return Endian.To(Endian.Native).Convert(_source.ReadInt64());
		}

		/// <summary>
		///     Reads a signed byte from this stream and advances the current position of the stream by one byte.
		/// </summary>
		/// <returns>
		///     A signed byte read from the current stream.
		/// </returns>
		public override sbyte ReadSByte()
		{
			return (sbyte) ReadByte();
		}

		/// <summary>
		///     Reads a 4-byte floating point value from the current stream and advances the current position of the stream by
		///     four bytes.
		/// </summary>
		/// <returns>
		///     A 4-byte floating point value read from the current stream.
		/// </returns>
		public override float ReadSingle()
		{
			return Endian.To(Endian.Native).Convert(_source.ReadSingle());
		}

		/// <summary>
		///     Reads a string from the current stream. The string is prefixed with the length, encoded as an integer seven bits
		///     at a time.
		/// </summary>
		/// <returns>
		///     The string being read.
		/// </returns>
		public override string ReadString()
		{
			return _source.ReadString();
		}

		/// <summary>
		///     Reads a 2-byte unsigned integer from the current stream using little-endian encoding and advances the position of
		///     the stream by two bytes.
		/// </summary>
		/// <returns>
		///     A 2-byte unsigned integer read from this stream.
		/// </returns>
		public override ushort ReadUInt16()
		{
			return Endian.To(Endian.Native).Convert(_source.ReadUInt16());
		}

		/// <summary>
		///     Reads a 4-byte unsigned integer from the current stream and advances the position of the stream by four bytes.
		/// </summary>
		/// <returns>
		///     A 4-byte unsigned integer read from this stream.
		/// </returns>
		public override uint ReadUInt32()
		{
			return Endian.To(Endian.Native).Convert(_source.ReadUInt32());
		}

		/// <summary>
		///     Reads an 8-byte unsigned integer from the current stream and advances the position of the stream by eight bytes.
		/// </summary>
		/// <returns>
		///     An 8-byte unsigned integer read from this stream.
		/// </returns>
		public override ulong ReadUInt64()
		{
			return Endian.To(Endian.Native).Convert(_source.ReadUInt64());
		}
	}
}
