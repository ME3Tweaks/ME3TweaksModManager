using System;
using System.Runtime.InteropServices;

namespace Gammtek.Conduit.IO
{
	public sealed class ByteOrderConverter
	{
		private readonly ByteOrder _byteOrder;

		private ByteOrderConverter(ByteOrder byteOrder = ByteOrder.LittleEndian)
		{
			_byteOrder = byteOrder;
		}

		public static ByteOrderConverter BigEndian { get; } = new ByteOrderConverter(ByteOrder.BigEndian);

		public static ByteOrderConverter LittleEndian { get; } = new ByteOrderConverter();

		public void CopyBytes(bool value, byte[] buffer, int index)
		{
			CopyBytes(value ? 1 : 0, 1, buffer, index);
		}

		public void CopyBytes(char value, byte[] buffer, int index)
		{
			CopyBytes(value, 2, buffer, index);
		}

		public void CopyBytes(decimal value, byte[] buffer, int index)
		{
			var parts = decimal.GetBits(value);

			for (var i = 0; i < 4; i++)
			{
				CopyBytes(parts[i], 4, buffer, i * 4 + index);
			}
		}

		public void CopyBytes(double value, byte[] buffer, int index)
		{
			CopyBytes(DoubleToInt64Bits(value), 8, buffer, index);
		}

		public void CopyBytes(short value, byte[] buffer, int index)
		{
			CopyBytes(value, 2, buffer, index);
		}

		public void CopyBytes(int value, byte[] buffer, int index)
		{
			CopyBytes(value, 4, buffer, index);
		}

		public void CopyBytes(long value, byte[] buffer, int index)
		{
			CopyBytes(value, 8, buffer, index);
		}

		public void CopyBytes(float value, byte[] buffer, int index)
		{
			CopyBytes(SingleToInt32Bits(value), 4, buffer, index);
		}

		public void CopyBytes(ushort value, byte[] buffer, int index)
		{
			CopyBytes(value, 2, buffer, index);
		}

		public void CopyBytes(uint value, byte[] buffer, int index)
		{
			CopyBytes(value, 4, buffer, index);
		}

		public void CopyBytes(ulong value, byte[] buffer, int index)
		{
			CopyBytes(unchecked((long) value), 8, buffer, index);
		}

		public long DoubleToInt64Bits(double value)
		{
			return BitConverter.DoubleToInt64Bits(value);
		}

		public byte[] GetBytes(bool value)
		{
			return BitConverter.GetBytes(value);
		}

		public byte[] GetBytes(char value)
		{
			return GetBytes(value, 2);
		}

		public byte[] GetBytes(decimal value)
		{
			var bytes = new byte[16];
			var parts = decimal.GetBits(value);

			for (var i = 0; i < 4; i++)
			{
				CopyBytes(parts[i], 4, bytes, i * 4);
			}

			return bytes;
		}

		public byte[] GetBytes(double value)
		{
			return GetBytes(DoubleToInt64Bits(value), 8);
		}

		public byte[] GetBytes(short value)
		{
			return GetBytes(value, 2);
		}

		public byte[] GetBytes(int value)
		{
			return GetBytes(value, 4);
		}

		public byte[] GetBytes(long value)
		{
			return GetBytes(value, 8);
		}

		public byte[] GetBytes(float value)
		{
			return GetBytes(SingleToInt32Bits(value), 4);
		}

		public byte[] GetBytes(ushort value)
		{
			return GetBytes(value, 2);
		}

		public byte[] GetBytes(uint value)
		{
			return GetBytes(value, 4);
		}

		public byte[] GetBytes(ulong value)
		{
			return GetBytes(unchecked((long) value), 8);
		}

		public float Int32BitsToSingle(int value)
		{
			return new Int32SingleUnion(value).AsSingle;
		}

		public double Int64BitsToDouble(long value)
		{
			return BitConverter.Int64BitsToDouble(value);
		}

		public int SingleToInt32Bits(float value)
		{
			return new Int32SingleUnion(value).AsInt32;
		}

		public bool ToBoolean(byte[] value, int startIndex)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			if (startIndex < 0 || startIndex > value.Length - 1)
			{
				throw new ArgumentOutOfRangeException(nameof(startIndex));
			}

			return BitConverter.ToBoolean(value, startIndex);
		}

		public char ToChar(byte[] value, int startIndex)
		{
			return unchecked((char) (CheckedFromBytes(value, startIndex, 2)));
		}

		public decimal ToDecimal(byte[] value, int startIndex)
		{
			var parts = new int[4];

			for (var i = 0; i < 4; i++)
			{
				parts[i] = ToInt32(value, startIndex + i * 4);
			}

			return new decimal(parts);
		}

		public double ToDouble(byte[] value, int startIndex)
		{
			return Int64BitsToDouble(ToInt64(value, startIndex));
		}

		public short ToInt16(byte[] value, int startIndex)
		{
			return unchecked((short) (CheckedFromBytes(value, startIndex, 2)));
		}

		public int ToInt32(byte[] value, int startIndex)
		{
			return unchecked((int) (CheckedFromBytes(value, startIndex, 4)));
		}

		public long ToInt64(byte[] value, int startIndex)
		{
			return CheckedFromBytes(value, startIndex, 8);
		}

		public float ToSingle(byte[] value, int startIndex)
		{
			return Int32BitsToSingle(ToInt32(value, startIndex));
		}

		public string ToString(byte[] value)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			return BitConverter.ToString(value);
		}

		public string ToString(byte[] value, int startIndex)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			return BitConverter.ToString(value, startIndex);
		}

		public string ToString(byte[] value, int startIndex, int length)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			return BitConverter.ToString(value, startIndex, length);
		}

		public ushort ToUInt16(byte[] value, int startIndex)
		{
			return unchecked((ushort) (CheckedFromBytes(value, startIndex, 2)));
		}

		public uint ToUInt32(byte[] value, int startIndex)
		{
			return unchecked((uint) (CheckedFromBytes(value, startIndex, 4)));
		}

		public ulong ToUInt64(byte[] value, int startIndex)
		{
			return unchecked((ulong) (CheckedFromBytes(value, startIndex, 8)));
		}

		private long CheckedFromBytes(byte[] value, int startIndex, int bytesToConvert)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			if (startIndex < 0 || startIndex > value.Length - bytesToConvert)
			{
				throw new ArgumentOutOfRangeException(nameof(startIndex));
			}

			return FromBytes(value, startIndex, bytesToConvert);
		}

		private void CopyBytes(long value, int bytes, byte[] buffer, int index)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (buffer.Length < index + bytes)
			{
				throw new ArgumentOutOfRangeException(nameof(index));
			}

			for (var i = 0; i < bytes; i++)
			{
				if (_byteOrder == ByteOrder.BigEndian)
				{
					buffer[(index + bytes - 1) - i] = unchecked((byte) (value & 0xff));
				}
				else
				{
					buffer[i + index] = unchecked((byte) (value & 0xff));
				}

				value = value >> 8;
			}
		}

		private long FromBytes(byte[] buffer, int startIndex, int bytesToConvert)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			long returnValue = 0;

			for (var i = 0; i < bytesToConvert; i++)
			{
				if (_byteOrder == ByteOrder.BigEndian)
				{
					returnValue = unchecked((returnValue << 8) | buffer[startIndex + i]);
				}
				else
				{
					returnValue = unchecked((returnValue << 8) | buffer[startIndex + bytesToConvert - 1 - i]);
				}
			}

			return returnValue;
		}

		private byte[] GetBytes(long value, int bytes)
		{
			var buffer = new byte[bytes];

			CopyBytes(value, bytes, buffer, 0);

			return buffer;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct Int32SingleUnion
		{
			[FieldOffset(0)]
			private readonly int i;

			[FieldOffset(0)]
			private readonly float f;

			internal Int32SingleUnion(int i)
			{
				f = 0;
				this.i = i;
			}

			internal Int32SingleUnion(float f)
			{
				i = 0;
				this.f = f;
			}

			internal int AsInt32 => i;

			internal float AsSingle => f;
		}
	}
}
