using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Gammtek.Conduit.IO;

namespace Gammtek.Conduit.Extensions.IO
{
	public static class StreamExtensions
	{
		public const int DefaultBufferSize = 8 * 1024;
		public static Encoding DefaultEncoding = Encoding.ASCII;

		/// <summary>
		///     Copies all the data from one stream into another.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="output">The stream to write to</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentNullException">output is null</exception>
		/// <exception cref="IOException">An error occurs while reading or writing</exception>
		public static void Copy(this Stream input, Stream output)
		{
			Copy(input, output, DefaultBufferSize);
		}

		/// <summary>
		///     Copies all the data from one stream into another, using a buffer
		///     of the given size.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="output">The stream to write to</param>
		/// <param name="bufferSize">The size of buffer to use when reading</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentNullException">output is null</exception>
		/// <exception cref="ArgumentOutOfRangeException">bufferSize is less than 1</exception>
		/// <exception cref="IOException">An error occurs while reading or writing</exception>
		public static void Copy(this Stream input, Stream output, int bufferSize)
		{
			if (bufferSize < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(bufferSize));
			}

			Copy(input, output, new byte[bufferSize]);
		}

		/// <summary>
		///     Copies all the data from one stream into another, using the given
		///     buffer for transferring data. Note that the current contents of
		///     the buffer is ignored, so the buffer needn't be cleared beforehand.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="output">The stream to write to</param>
		/// <param name="buffer">The buffer to use to transfer data</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentNullException">output is null</exception>
		/// <exception cref="ArgumentNullException">buffer is null</exception>
		/// <exception cref="IOException">An error occurs while reading or writing</exception>
		public static void Copy(this Stream input, Stream output, IBuffer buffer)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			Copy(input, output, buffer.Bytes);
		}

		/// <summary>
		///     Copies all the data from one stream into another, using the given
		///     buffer for transferring data. Note that the current contents of
		///     the buffer is ignored, so the buffer needn't be cleared beforehand.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="output">The stream to write to</param>
		/// <param name="buffer">The buffer to use to transfer data</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentNullException">output is null</exception>
		/// <exception cref="ArgumentNullException">buffer is null</exception>
		/// <exception cref="ArgumentException">buffer is a zero-length array</exception>
		/// <exception cref="IOException">An error occurs while reading or writing</exception>
		public static void Copy(this Stream input, Stream output, byte[] buffer)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (input == null)
			{
				throw new ArgumentNullException(nameof(input));
			}

			if (output == null)
			{
				throw new ArgumentNullException(nameof(output));
			}

			if (buffer.Length == 0)
			{
				throw new ArgumentException("Buffer has length of 0");
			}

			int read;

			while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
			{
				output.Write(buffer, 0, read);
			}
		}

		public static int ReadAligned(this Stream stream, byte[] buffer, int offset, int size, int align)
		{
			if (size == 0)
			{
				return 0;
			}

			var read = stream.Read(buffer, offset, size);
			var skip = size % align;

			// Skip aligned bytes
			if (skip > 0)
			{
				stream.Seek(align - skip, SeekOrigin.Current);
			}

			return read;
		}

		public static bool ReadBoolean(this Stream stream)
		{
			return stream.ReadByte() > 0;
		}

		public static bool ReadBooleanInt(this Stream stream, ByteOrder endian = ByteOrder.LittleEndian)
		{
			return ReadUInt32(stream, endian) != 0;
		}

		public static byte ReadByte(this Stream stream)
		{
			return (byte) stream.ReadByte();
		}

		public static byte[] ReadBytes(this Stream stream, int length)
		{
			if (length < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(length));
			}

			var data = new byte[length];
			var read = stream.Read(data, 0, length);

			if (read != length)
			{
				throw new EndOfStreamException();
			}

			return data;
		}

		public static byte[] ReadBytes(this Stream stream, uint length)
		{
			return stream.ReadBytes((int) length);
		}

		public static double ReadDouble(this Stream stream, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = stream.ReadBytes(8);

			if (ShouldSwap(byteOrder))
			{
				return BitConverter.Int64BitsToDouble(BitConverter.ToInt64(data, 0).Swap());
			}

			return BitConverter.ToDouble(data, 0);
		}

		public static T ReadEnum<T>(this Stream stream, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var type = typeof (T);

			object value;

			switch (EnumTypeCache.Get(type))
			{
				case TypeCode.Byte:
				{
					value = stream.ReadByte();

					break;
				}
				case TypeCode.Int16:
				{
					value = stream.ReadInt16(byteOrder);

					break;
				}
				case TypeCode.Int32:
				{
					value = stream.ReadInt32(byteOrder);

					break;
				}
				case TypeCode.Int64:
				{
					value = stream.ReadInt64(byteOrder);
					break;
				}
				case TypeCode.SByte:
				{
					value = stream.ReadSByte();

					break;
				}
				case TypeCode.UInt16:
				{
					value = stream.ReadUInt16(byteOrder);

					break;
				}
				case TypeCode.UInt32:
				{
					value = stream.ReadUInt32(byteOrder);

					break;
				}

				case TypeCode.UInt64:
				{
					value = stream.ReadUInt64(byteOrder);

					break;
				}
				default:
				{
					throw new NotSupportedException();
				}
			}

			return (T) Enum.ToObject(type, value);
		}

		/// <summary>
		///     Reads exactly the given number of bytes from the specified stream.
		///     If the end of the stream is reached before the specified amount
		///     of data is read, an exception is thrown.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="bytesToRead">The number of bytes to read</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentOutOfRangeException">bytesToRead is less than 1</exception>
		/// <exception cref="EndOfStreamException">
		///     The end of the stream is reached before
		///     enough data has been read
		/// </exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		/// <returns>The data read from the stream</returns>
		public static byte[] ReadExactly(this Stream input, int bytesToRead)
		{
			return ReadExactly(input, new byte[bytesToRead]);
		}

		/// <summary>
		///     Reads into a buffer, filling it completely.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="buffer">The buffer to read into</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentOutOfRangeException">The buffer is of zero length</exception>
		/// <exception cref="EndOfStreamException">
		///     The end of the stream is reached before
		///     enough data has been read
		/// </exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		/// <returns>The data read from the stream</returns>
		public static byte[] ReadExactly(this Stream input, IBuffer buffer)
		{
			return ReadExactly(input, buffer.Bytes);
		}

		/// <summary>
		///     Reads into a buffer, filling it completely.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="buffer">The buffer to read into</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentOutOfRangeException">The buffer is of zero length</exception>
		/// <exception cref="EndOfStreamException">
		///     The end of the stream is reached before
		///     enough data has been read
		/// </exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		/// <returns>The data read from the stream</returns>
		public static byte[] ReadExactly(this Stream input, byte[] buffer)
		{
			return ReadExactly(input, buffer, buffer.Length);
		}

		/// <summary>
		///     Reads into a buffer, for the given number of bytes.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="buffer">The buffer to read into</param>
		/// <param name="bytesToRead">The number of bytes to read</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     The buffer is of zero length, or bytesToRead
		///     exceeds the buffer length
		/// </exception>
		/// <exception cref="EndOfStreamException">
		///     The end of the stream is reached before
		///     enough data has been read
		/// </exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		/// <returns>The data read from the stream</returns>
		public static byte[] ReadExactly(this Stream input, IBuffer buffer, int bytesToRead)
		{
			return ReadExactly(input, buffer.Bytes, bytesToRead);
		}

		/// <summary>
		///     Reads exactly the given number of bytes from the specified stream,
		///     into the given buffer, starting at position 0 of the array.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="buffer">The byte array to read into</param>
		/// <param name="bytesToRead">The number of bytes to read</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentOutOfRangeException">bytesToRead is less than 1</exception>
		/// <exception cref="EndOfStreamException">
		///     The end of the stream is reached before
		///     enough data has been read
		/// </exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		public static byte[] ReadExactly(this Stream input, byte[] buffer, int bytesToRead)
		{
			return ReadExactly(input, buffer, 0, bytesToRead);
		}

		/// <summary>
		///     Reads into a buffer, for the given number of bytes, from the specified location
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="buffer">The buffer to read into</param>
		/// <param name="startIndex">The index into the buffer at which to start writing</param>
		/// <param name="bytesToRead">The number of bytes to read</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     The buffer is of zero length, or startIndex+bytesToRead
		///     exceeds the buffer length
		/// </exception>
		/// <exception cref="EndOfStreamException">
		///     The end of the stream is reached before
		///     enough data has been read
		/// </exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		/// <returns>The data read from the stream</returns>
		public static byte[] ReadExactly(this Stream input, IBuffer buffer, int startIndex, int bytesToRead)
		{
			return ReadExactly(input, buffer.Bytes, 0, bytesToRead);
		}

		/// <summary>
		///     Reads exactly the given number of bytes from the specified stream,
		///     into the given buffer, starting at position 0 of the array.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="buffer">The byte array to read into</param>
		/// <param name="startIndex">The index into the buffer at which to start writing</param>
		/// <param name="bytesToRead">The number of bytes to read</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     bytesToRead is less than 1, startIndex is less than 0,
		///     or startIndex+bytesToRead is greater than the buffer length
		/// </exception>
		/// <exception cref="EndOfStreamException">
		///     The end of the stream is reached before
		///     enough data has been read
		/// </exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		public static byte[] ReadExactly(this Stream input, byte[] buffer, int startIndex, int bytesToRead)
		{
			if (input == null)
			{
				throw new ArgumentNullException(nameof(input));
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (startIndex < 0 || startIndex >= buffer.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(startIndex));
			}

			if (bytesToRead < 1 || startIndex + bytesToRead > buffer.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(bytesToRead));
			}

			var index = 0;

			while (index < bytesToRead)
			{
				var read = input.Read(buffer, startIndex + index, bytesToRead - index);
				if (read == 0)
				{
					throw new EndOfStreamException
						(string.Format("End of stream reached with {0} byte{1} left to read.",
							bytesToRead - index,
							bytesToRead - index == 1 ? "s" : ""));
				}
				index += read;
			}
			return buffer;
		}

		/// <summary>
		///     Reads the given stream up to the end, returning the data as a byte
		///     array.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		/// <returns>The data read from the stream</returns>
		public static byte[] ReadFully(this Stream input)
		{
			return ReadFully(input, DefaultBufferSize);
		}

		/// <summary>
		///     Reads the given stream up to the end, returning the data as a byte
		///     array, using the given buffer size.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="bufferSize">The size of buffer to use when reading</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentOutOfRangeException">bufferSize is less than 1</exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		/// <returns>The data read from the stream</returns>
		public static byte[] ReadFully(this Stream input, int bufferSize)
		{
			if (bufferSize < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(bufferSize));
			}
			return ReadFully(input, new byte[bufferSize]);
		}

		/// <summary>
		///     Reads the given stream up to the end, returning the data as a byte
		///     array, using the given buffer for transferring data. Note that the
		///     current contents of the buffer is ignored, so the buffer needn't
		///     be cleared beforehand.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="buffer">The buffer to use to transfer data</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentNullException">buffer is null</exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		/// <returns>The data read from the stream</returns>
		public static byte[] ReadFully(this Stream input, IBuffer buffer)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}
			return ReadFully(input, buffer.Bytes);
		}

		/// <summary>
		///     Reads the given stream up to the end, returning the data as a byte
		///     array, using the given buffer for transferring data. Note that the
		///     current contents of the buffer is ignored, so the buffer needn't
		///     be cleared beforehand.
		/// </summary>
		/// <param name="input">The stream to read from</param>
		/// <param name="buffer">The buffer to use to transfer data</param>
		/// <exception cref="ArgumentNullException">input is null</exception>
		/// <exception cref="ArgumentNullException">buffer is null</exception>
		/// <exception cref="ArgumentException">buffer is a zero-length array</exception>
		/// <exception cref="IOException">An error occurs while reading from the stream</exception>
		/// <returns>The data read from the stream</returns>
		public static byte[] ReadFully(this Stream input, byte[] buffer)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}
			if (input == null)
			{
				throw new ArgumentNullException(nameof(input));
			}
			if (buffer.Length == 0)
			{
				throw new ArgumentException("Buffer has length of 0");
			}
			// We could do all our own work here, but using MemoryStream is easier
			// and likely to be just as efficient.
			using (var tempStream = new MemoryStream())
			{
				Copy(input, tempStream, buffer);
				// No need to copy the buffer if it's the right size
				if (tempStream.Length == tempStream.GetBuffer().Length)
				{
					return tempStream.GetBuffer();
				}
				// Okay, make a copy that's the right size
				return tempStream.ToArray();
			}
		}

		public static Guid ReadGuid(this Stream stream, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var a = stream.ReadInt32(byteOrder);
			var b = stream.ReadInt16(byteOrder);
			var c = stream.ReadInt16(byteOrder);
			var d = stream.ReadBytes(8);

			return new Guid(a, b, c, d);
		}

		public static short ReadInt16(this Stream stream, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = stream.ReadBytes(2);
			var value = BitConverter.ToInt16(data, 0);

			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public static int ReadInt32(this Stream stream, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = stream.ReadBytes(4);
			var value = BitConverter.ToInt32(data, 0);

			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public static long ReadInt64(this Stream stream, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = stream.ReadBytes(8);
			var value = BitConverter.ToInt64(data, 0);

			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public static sbyte ReadInt8(this Stream stream)
		{
			return stream.ReadSByte();
		}

		public static sbyte ReadSByte(this Stream stream)
		{
			return (sbyte) stream.ReadByte();
		}

		public static float ReadSingle(this Stream stream, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = stream.ReadBytes(4);

			if (ShouldSwap(byteOrder))
			{
				return BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(data, 0).Swap()), 0);
			}

			return BitConverter.ToSingle(data, 0);
		}

		public static string ReadString(this Stream stream, uint size)
		{
			return stream.ReadStringInternalStatic(DefaultEncoding, size, false);
		}

		public static string ReadString(this Stream stream, uint size, bool trailingNull)
		{
			return stream.ReadStringInternalStatic(DefaultEncoding, size, trailingNull);
		}

		public static string ReadString(this Stream stream, uint size, Encoding encoding)
		{
			return stream.ReadStringInternalStatic(encoding, size, false);
		}

		public static string ReadString(this Stream stream, int size, Encoding encoding)
		{
			return stream.ReadStringInternalStatic(encoding, (uint) size, false);
		}

		public static string ReadString(this Stream stream, uint size, bool trailingNull, Encoding encoding)
		{
			return stream.ReadStringInternalStatic(encoding, size, trailingNull);
		}

		public static string ReadString(this Stream stream, int size, bool trailingNull, Encoding encoding)
		{
			return stream.ReadStringInternalStatic(encoding, (uint) size, trailingNull);
		}

		internal static string ReadStringInternalDynamic(this Stream stream, Encoding encoding, char end)
		{
			var characterSize = encoding.GetByteCount("e");

			Debug.Assert(characterSize == 1 || characterSize == 2 || characterSize == 4);

			var characterEnd = end.ToString(CultureInfo.InvariantCulture);

			var i = 0;
			var data = new byte[128 * characterSize];

			while (true)
			{
				if (i + characterSize > data.Length)
				{
					Array.Resize(ref data, data.Length + (128 * characterSize));
				}

				var read = stream.Read(data, i, characterSize);
				Debug.Assert(read == characterSize);

				if (encoding.GetString(data, i, characterSize) == characterEnd)
				{
					break;
				}

				i += characterSize;
			}

			return i == 0 ? "" : encoding.GetString(data, 0, i);
		}

		internal static string ReadStringInternalStatic(this Stream stream, Encoding encoding, uint size, bool trailingNull)
		{
			var data = new byte[size];
			stream.Read(data, 0, data.Length);

			var value = encoding.GetString(data, 0, data.Length);

			if (!trailingNull)
			{
				return value;
			}

			var position = value.IndexOf('\0');

			if (position >= 0)
			{
				value = value.Substring(0, position);
			}

			return value;
		}

		public static string ReadStringZ(this Stream stream)
		{
			return stream.ReadStringInternalDynamic(DefaultEncoding, '\0');
		}

		public static string ReadStringZ(this Stream stream, Encoding encoding)
		{
			return stream.ReadStringInternalDynamic(encoding, '\0');
		}

		public static T ReadStructure<T>(this Stream stream)
		{
			var structureSize = Marshal.SizeOf(typeof (T));
			var buffer = new byte[structureSize];

			if (stream.Read(buffer, 0, structureSize) != structureSize)
			{
				throw new EndOfStreamException("could not read all of data for structure");
			}

			var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

			var structure = (T) Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof (T));

			handle.Free();

			return structure;
		}

		public static T ReadStructure<T>(this Stream stream, int size)
		{
			var structureSize = Marshal.SizeOf(typeof (T));
			var buffer = new byte[Math.Max(structureSize, size)];

			if (stream.Read(buffer, 0, size) != size)
			{
				throw new EndOfStreamException("could not read all of data for structure");
			}

			var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

			var structure = (T) Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof (T));

			handle.Free();

			return structure;
		}

		public static MemoryStream ReadToMemoryStream(this Stream stream, long size, int buffer = 0x40000)
		{
			var memory = new MemoryStream();

			var left = size;
			var data = new byte[buffer];

			while (left > 0)
			{
				var block = (int) (Math.Min(left, data.Length));

				if (stream.Read(data, 0, block) != block)
				{
					throw new EndOfStreamException();
				}

				memory.Write(data, 0, block);
				left -= block;
			}

			memory.Seek(0, SeekOrigin.Begin);

			return memory;
		}

		public static ushort ReadUInt16(this Stream stream, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = stream.ReadBytes(2);
			var value = BitConverter.ToUInt16(data, 0);

			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public static uint ReadUInt32(this Stream stream, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = stream.ReadBytes(4);
			var value = BitConverter.ToUInt32(data, 0);

			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public static ulong ReadUInt64(this Stream stream, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = stream.ReadBytes(8);
			var value = BitConverter.ToUInt64(data, 0);

			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public static byte ReadUInt8(this Stream stream)
		{
			return (byte) stream.ReadByte();
		}

		public static bool ShouldSwap(ByteOrder byteOrder)
		{
			switch (byteOrder)
			{
				case ByteOrder.LittleEndian:
				{
					return BitConverter.IsLittleEndian == false;
				}
				case ByteOrder.BigEndian:
				{
					return BitConverter.IsLittleEndian;
				}
				default:
				{
					throw new ArgumentException("unsupported endianness", nameof(byteOrder));
				}
			}
		}

		public static void WriteAligned(this Stream stream, byte[] buffer, int offset, int size, int align)
		{
			if (size == 0)
			{
				return;
			}

			stream.Write(buffer, offset, size);

			var skip = size % align;

			// this is a dumbfuck way to do this but it'll work for now
			if (skip <= 0)
			{
				return;
			}

			var junk = new byte[align - skip];

			stream.Write(junk, 0, align - skip);
		}

		public static void WriteBoolean(this Stream stream, bool value)
		{
			stream.WriteByte((byte) (value ? 1 : 0));
		}

		public static void WriteBooleanInt(this Stream stream, bool value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			stream.WriteUInt32((byte) (value ? 1 : 0), byteOrder);
		}

		public static void WriteByte(this Stream stream, byte value)
		{
			stream.WriteByte(value);
		}

		public static void WriteBytes(this Stream stream, byte[] data)
		{
			stream.Write(data, 0, data.Length);
		}

		public static void WriteDouble(this Stream stream, double value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = ShouldSwap(byteOrder)
				? BitConverter.GetBytes(BitConverter.DoubleToInt64Bits(value).Swap())
				: BitConverter.GetBytes(value);

			Debug.Assert(data.Length == 8);
			stream.WriteBytes(data);
		}

		public static void WriteEnum<T>(this Stream stream, object value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var type = typeof (T);

			switch (EnumTypeCache.Get(type))
			{
				case TypeCode.Byte:
				{
					stream.WriteByte((byte) value);
					break;
				}
				case TypeCode.Int16:
				{
					stream.WriteInt16((short) value, byteOrder);

					break;
				}
				case TypeCode.Int32:
				{
					stream.WriteInt32((int) value, byteOrder);

					break;
				}
				case TypeCode.Int64:
				{
					stream.WriteInt64((long) value, byteOrder);

					break;
				}
				case TypeCode.SByte:
				{
					stream.WriteSByte((sbyte) value);

					break;
				}
				case TypeCode.UInt16:
				{
					stream.WriteUInt16((ushort) value, byteOrder);

					break;
				}
				case TypeCode.UInt32:
				{
					stream.WriteUInt32((uint) value, byteOrder);

					break;
				}
				case TypeCode.UInt64:
				{
					stream.WriteUInt64((ulong) value, byteOrder);

					break;
				}
				default:
				{
					throw new NotSupportedException();
				}
			}
		}

		public static void WriteFromStream(this Stream stream, Stream input, long size, int buffer = 0x40000)
		{
			var left = size;
			var data = new byte[buffer];

			while (left > 0)
			{
				var block = (int) (Math.Min(left, data.Length));

				if (input.Read(data, 0, block) != block)
				{
					throw new EndOfStreamException();
				}

				stream.Write(data, 0, block);
				left -= block;
			}
		}

		public static void WriteGuid(this Stream stream, Guid value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = value.ToByteArray();

			Debug.Assert(data.Length == 16);

			stream.WriteInt32(BitConverter.ToInt32(data, 0), byteOrder);
			stream.WriteInt16(BitConverter.ToInt16(data, 4), byteOrder);
			stream.WriteInt16(BitConverter.ToInt16(data, 6), byteOrder);
			stream.Write(data, 8, 8);
		}

		public static void WriteInt16(this Stream stream, short value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			var data = BitConverter.GetBytes(value);
			Debug.Assert(data.Length == 2);
			stream.WriteBytes(data);
		}

		public static void WriteInt32(this Stream stream, int value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			var data = BitConverter.GetBytes(value);
			Debug.Assert(data.Length == 4);
			stream.WriteBytes(data);
		}

		public static void WriteInt64(this Stream stream, long value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			var data = BitConverter.GetBytes(value);
			Debug.Assert(data.Length == 8);
			stream.WriteBytes(data);
		}

		public static void WriteSByte(this Stream stream, sbyte value)
		{
			stream.WriteByte((byte) value);
		}

		public static void WriteSingle(this Stream stream, float value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			var data = ShouldSwap(byteOrder)
				? BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(value), 0).Swap())
				: BitConverter.GetBytes(value);

			Debug.Assert(data.Length == 4);
			stream.WriteBytes(data);
		}

		public static void WriteString(this Stream stream, string value)
		{
			stream.WriteStringInternalStatic(DefaultEncoding, value);
		}

		public static void WriteString(this Stream stream, string value, int size)
		{
			stream.WriteStringInternalStatic(DefaultEncoding, value, size);
		}

		public static void WriteString(this Stream stream, string value, Encoding encoding)
		{
			stream.WriteStringInternalStatic(encoding, value);
		}

		public static void WriteString(this Stream stream, string value, int size, Encoding encoding)
		{
			stream.WriteStringInternalStatic(encoding, value, size);
		}

		internal static void WriteStringInternalDynamic(this Stream stream, Encoding encoding, string value, char end)
		{
			var data = encoding.GetBytes(value);
			stream.Write(data, 0, data.Length);

			data = encoding.GetBytes(end.ToString(CultureInfo.InvariantCulture));
			stream.Write(data, 0, data.Length);
		}

		internal static void WriteStringInternalStatic(this Stream stream, Encoding encoding, string value)
		{
			var data = encoding.GetBytes(value);

			stream.Write(data, 0, data.Length);
		}

		internal static void WriteStringInternalStatic(this Stream stream, Encoding encoding, string value, int size)
		{
			var data = encoding.GetBytes(value);

			Array.Resize(ref data, size);
			stream.Write(data, 0, size);
		}

		public static void WriteStringZ(this Stream stream, string value)
		{
			stream.WriteStringInternalDynamic(DefaultEncoding, value, '\0');
		}

		public static void WriteStringZ(this Stream stream, string value, Encoding encoding)
		{
			stream.WriteStringInternalDynamic(encoding, value, '\0');
		}

		public static void WriteStructure<T>(this Stream stream, T structure)
		{
			var structureSize = Marshal.SizeOf(typeof (T));
			var buffer = new byte[structureSize];
			var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

			Marshal.StructureToPtr(structure, handle.AddrOfPinnedObject(), false);

			handle.Free();

			stream.Write(buffer, 0, buffer.Length);
		}

		public static void WriteStructure<T>(this Stream stream, T structure, int size)
		{
			var structureSize = Marshal.SizeOf(typeof (T));
			var buffer = new byte[Math.Max(structureSize, size)];
			var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

			Marshal.StructureToPtr(structure, handle.AddrOfPinnedObject(), false);

			handle.Free();

			stream.Write(buffer, 0, buffer.Length);
		}

		public static void WriteUInt16(this Stream stream, ushort value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			var data = BitConverter.GetBytes(value);
			Debug.Assert(data.Length == 2);
			stream.WriteBytes(data);
		}

		public static void WriteUInt32(this Stream stream, uint value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			var data = BitConverter.GetBytes(value);
			Debug.Assert(data.Length == 4);
			stream.WriteBytes(data);
		}

		public static void WriteUInt64(this Stream stream, ulong value, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			if (ShouldSwap(byteOrder))
			{
				value = value.Swap();
			}

			var data = BitConverter.GetBytes(value);
			Debug.Assert(data.Length == 8);
			stream.WriteBytes(data);
		}

		private static class EnumTypeCache
		{
			public static TypeCode Get(Type type)
			{
				return TranslateType(type);
			}

			private static TypeCode TranslateType(Type type)
			{
				if (!type.IsEnum)
				{
					throw new ArgumentException("unknown enum type", nameof(type));
				}

				var underlyingType = Enum.GetUnderlyingType(type);
				var underlyingTypeCode = Type.GetTypeCode(underlyingType);

				switch (underlyingTypeCode)
				{
					case TypeCode.Byte:
					case TypeCode.Int16:
					case TypeCode.Int32:
					case TypeCode.Int64:
					case TypeCode.SByte:
					case TypeCode.UInt16:
					case TypeCode.UInt32:
					case TypeCode.UInt64:
					{
						return underlyingTypeCode;
					}
				}

				throw new ArgumentException("unknown enum type", nameof(type));
			}
		}
	}
}
