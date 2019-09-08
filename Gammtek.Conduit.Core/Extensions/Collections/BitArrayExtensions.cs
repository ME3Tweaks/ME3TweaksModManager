using System.Collections;
using System.Collections.Generic;

namespace Gammtek.Conduit.Extensions.Collections
{
	public static class BitArrayExtensions
	{
		public static byte[] ToByteArray(this BitArray bits)
		{
			var numBytes = bits.Count / 8;

			if (bits.Count % 8 != 0)
			{
				numBytes++;
			}

			var bytes = new byte[numBytes];
			var byteIndex = 0;
			var bitIndex = 0;

			for (var i = 0; i < bits.Count; i++)
			{
				if (bits[i])
				{
					bytes[byteIndex] |= (byte) (1 << (7 - bitIndex));
				}

				bitIndex++;

				if (bitIndex != 8)
				{
					continue;
				}

				bitIndex = 0;
				byteIndex++;
			}

			return bytes;
		}

		public static byte[] ToByteArray(this BitArray bits, int count)
		{
			var numBytes = count / 8;

			if (count % 8 != 0)
			{
				numBytes++;
			}

			var bytes = new byte[numBytes];
			var byteIndex = 0;
			var bitIndex = 0;

			for (var i = 0; i < count && i < bits.Count; i++)
			{
				if (bits[i])
				{
					bytes[byteIndex] |= (byte) (1 << (7 - bitIndex));
				}

				bitIndex++;

				if (bitIndex != 8)
				{
					continue;
				}

				bitIndex = 0;
				byteIndex++;
			}

			return bytes;
		}

		public static byte[] ToByteArray(this List<BitArray> bitsList, int count)
		{
			var byteSize = count / 8;

			if (count % 8 > 0)
			{
				byteSize++;
			}

			var bytes = new byte[byteSize];
			var bytePos = 0;
			var bitsRead = 0;
			byte value = 0;
			byte significance = 1;

			foreach (var bits in bitsList)
			{
				for (var bitPos = 0; bitPos < bits.Length; bitPos++)
				{
					if (bits[bitPos])
					{
						value += significance;
					}

					++bitsRead;

					if (bitsRead % 8 == 0)
					{
						bytes[bytePos] = value;
						++bytePos;
						value = 0;
						significance = 1;
						bitsRead = 0;
					}
					else
					{
						significance <<= 1;
					}
				}
			}

			if (bitsRead % 8 != 0)
			{
				bytes[bytePos] = value;
			}

			return bytes;
		}
	}
}
