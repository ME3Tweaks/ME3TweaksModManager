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
using Gammtek.Conduit.IO.Converters;

namespace Gammtek.Conduit.IO
{
	/// <summary>
	///     Extension methods to write variable length integers to a
	///     BinaryWriter.
	/// </summary>
	public static class VarInt
	{
		/// <summary>
		///     Reads a variable length representation of an integer from the stream
		///     managed by <paramref name="reader" />.
		/// </summary>
		public static short ReadVarInt16(this BinaryReader reader)
		{
			var result = reader.ReadVarUInt64().ZigZag();

			if (result >= short.MinValue && result <= short.MaxValue)
			{
				return (short) result;
			}

			throw new OverflowException();
		}

		/// <summary>
		///     Reads a variable length representation of an integer from the stream
		///     managed by <paramref name="reader" />.
		/// </summary>
		public static int ReadVarInt32(this BinaryReader reader)
		{
			var result = reader.ReadVarUInt64().ZigZag();

			if (result >= int.MinValue && result <= int.MaxValue)
			{
				return (int) result;
			}

			throw new OverflowException();
		}

		/// <summary>
		///     Reads a variable length representation of an integer from the stream
		///     managed by <paramref name="reader" />.
		/// </summary>
		public static long ReadVarInt64(this BinaryReader reader)
		{
			return reader.ReadVarUInt64().ZigZag();
		}

		/// <summary>
		///     Reads a variable length representation of an integer from the stream
		///     managed by <paramref name="reader" />.
		/// </summary>
		public static ushort ReadVarUInt16(this BinaryReader reader)
		{
			var result = reader.ReadVarUInt64();

			if (result <= ushort.MaxValue)
			{
				return (ushort) result;
			}

			throw new OverflowException();
		}

		/// <summary>
		///     Reads a variable length representation of an integer from the stream
		///     managed by <paramref name="reader" />.
		/// </summary>
		public static uint ReadVarUInt32(this BinaryReader reader)
		{
			var result = reader.ReadVarUInt64();

			if (result <= uint.MaxValue)
			{
				return (uint) result;
			}

			throw new OverflowException();
		}

		/// <summary>
		///     Reads a variable length representation of an integer from the stream
		///     managed by <paramref name="reader" />.
		/// </summary>
		public static ulong ReadVarUInt64(this BinaryReader reader)
		{
			ulong result = 0;
			var iteration = 0;
			bool pendingData;

			do
			{
				var b = reader.ReadByte();

				pendingData = (iteration < 8) && (b & 0x80) != 0;

				var v = (ulong) (iteration < 8 ? (b & 0x7f) : b);

				result = result + (v << (7 * iteration));

				++iteration;
			} while (pendingData);

			return result;
		}

		/// <summary>
		///     Writes a variable length representation of <paramref name="value" />
		///     to the stream managed by <paramref name="writer" />.
		/// </summary>
		public static void WriteVar(this BinaryWriter writer, ulong value)
		{
			var temp = value;
			uint iteration = 1;

			do
			{
				var b = (iteration < 9)
					? (byte) ((byte) (temp & 0x7f) | (byte) ((temp > 0x7f) ? 0x80 : 0))
					: (byte) (temp & 0xff);

				writer.Write(b);

				temp = temp >> ((iteration < 9) ? 7 : 8);

				++iteration;
			} while (temp != 0);
		}

		/// <summary>
		///     Writes a variable length representation of <paramref name="value" />
		///     to the stream managed by <paramref name="writer" />.
		/// </summary>
		public static void WriteVar(this BinaryWriter writer, uint value)
		{
			writer.WriteVar((ulong) value);
		}

		/// <summary>
		///     Writes a variable length representation of <paramref name="value" />
		///     to the stream managed by <paramref name="writer" />.
		/// </summary>
		public static void WriteVar(this BinaryWriter writer, ushort value)
		{
			writer.WriteVar((ulong) value);
		}

		/// <summary>
		///     Writes a variable length representation of <paramref name="value" />
		///     to the stream managed by <paramref name="writer" />.
		/// </summary>
		public static void WriteVar(this BinaryWriter writer, long value)
		{
			writer.WriteVar(value.ZigZag());
		}

		/// <summary>
		///     Writes a variable length representation of <paramref name="value" />
		///     to the stream managed by <paramref name="writer" />.
		/// </summary>
		public static void WriteVar(this BinaryWriter writer, int value)
		{
			writer.WriteVar(value.ZigZag());
		}

		/// <summary>
		///     Writes a variable length representation of <paramref name="value" />
		///     to the stream managed by <paramref name="writer" />.
		/// </summary>
		public static void WriteVar(this BinaryWriter writer, short value)
		{
			writer.WriteVar(value.ZigZag());
		}
	}
}
