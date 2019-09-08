using System;
using System.Text;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.Extensions.IO;

namespace Gammtek.Conduit.IO
{
	[Obsolete]
	public class ByteBufferReader
	{
		private readonly ByteOrder _byteOrder;

		public ByteBufferReader(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (offset < 0 || offset >= buffer.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(offset));
			}

			Buffer = buffer;
			Offset = offset;
			_byteOrder = byteOrder;
		}

		public byte[] Buffer { get; }

		public int Offset { get; }

		public ByteBufferReader this[int index] => index == 0 
			? this 
			: new ByteBufferReader(Buffer, Offset + index, _byteOrder);

		public bool ReadBoolean()
		{
			return Buffer[Offset] != 0;
		}

		public bool ReadIntAsBoolean()
		{
			return BitConverter.ToInt32(Buffer, Offset) != 0;
		}

		public sbyte ReadSByte()
		{
			return (sbyte) Buffer[Offset];
		}

		public byte ReadByte()
		{
			return Buffer[Offset];
		}

		public short ReadInt16()
		{
			var value = BitConverter.ToInt16(Buffer, Offset);

			if (StreamExtensions.ShouldSwap(_byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public ushort ReadUInt16()
		{
			var value = BitConverter.ToUInt16(Buffer, Offset);

			if (StreamExtensions.ShouldSwap(_byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public int ReadInt32()
		{
			var value = BitConverter.ToInt32(Buffer, Offset);

			if (StreamExtensions.ShouldSwap(_byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public uint ReadUInt32()
		{
			var value = BitConverter.ToUInt32(Buffer, Offset);

			if (StreamExtensions.ShouldSwap(_byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public long ReadInt64()
		{
			var value = BitConverter.ToInt64(Buffer, Offset);

			if (StreamExtensions.ShouldSwap(_byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public ulong ReadUInt64()
		{
			var value = BitConverter.ToUInt64(Buffer, Offset);

			if (StreamExtensions.ShouldSwap(_byteOrder))
			{
				value = value.Swap();
			}

			return value;
		}

		public float ReadSingle()
		{
			if (StreamExtensions.ShouldSwap(_byteOrder))
			{
				return
					BitConverter.ToSingle(
						BitConverter.GetBytes(BitConverter.ToInt32(Buffer, Offset).Swap()), 0);
			}

			return BitConverter.ToSingle(Buffer, Offset);
		}

		public double ReadDouble()
		{
			return StreamExtensions.ShouldSwap(_byteOrder) 
				? BitConverter.Int64BitsToDouble(BitConverter.ToInt64(Buffer, Offset).Swap()) 
				: BitConverter.ToDouble(Buffer, Offset);
		}

		public string ReadString(int size, bool trailingNull, Encoding encoding)
		{
			if (size < 0 || Offset + size > Buffer.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(size));
			}

			var value = encoding.GetString(Buffer, Offset, size);

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
	}
}