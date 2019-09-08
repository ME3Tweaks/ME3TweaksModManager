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

namespace Gammtek.Conduit.IO.Converters
{
	/// <summary>
	/// Provides the conversion functionality between two Endian types.
	/// This is either a straight-through copy if the Endian settings are
	/// the same or a conversion routine reversing the bytes.
	/// </summary>
	public unsafe class EndianConverter
	{
		private static readonly EndianConverter LinearCopier;
		private static readonly EndianConverter ReverseCopier;

		private readonly RawCopierDelegate _rawCopier;

		static EndianConverter()
		{
			LinearCopier = new EndianConverter(CopyLinear);
			ReverseCopier = new EndianConverter(CopyReverse);
		}

		private EndianConverter(RawCopierDelegate copier)
		{
			_rawCopier = copier;
		}

		/// <summary>
		/// Creates a converter based on whether the data needs to be converted or
		/// simply copied.
		/// </summary>
		public static EndianConverter Create(bool convert)
		{
			return convert ? ReverseCopier : LinearCopier;
		}

		/// <summary>
		/// Performs either a copy or byte order reversal from the <paramref name="source"/>
		/// to the <paramref name="destination"/> for <paramref name="count"/> bytes based
		/// on the conversion settings.
		/// </summary>
		public void BetweenBuffers(byte[] destination, int dstIndex, byte[] source, int srcIndex, int count)
		{
			fixed (byte* dst = &destination[dstIndex])
			fixed (byte* src = &source[srcIndex])
			{
				SafeCopier(dst, destination.Length - dstIndex, src, source.Length - srcIndex, count);
			}
		}

		/// <summary>
		/// Copies the bytes from <paramref name="data"/> to 
		/// <paramref name="destination"/> starting at <paramref name="index"/>
		/// either linearly or reversed based on the conversion settings.
		/// </summary>
		public void ToBytes(char data, byte[] destination, int index)
		{
			var src = (byte*)&data;
			fixed (byte* dst = &destination[index])
			{
				SafeCopier(dst, destination.Length - index, src, sizeof(char), sizeof(char));
			}
		}

		/// <summary>
		/// Copies the bytes from <paramref name="data"/> to 
		/// <paramref name="destination"/> starting at <paramref name="index"/>
		/// either linearly or reversed based on the conversion settings.
		/// </summary>
		public void ToBytes(double data, byte[] destination, int index)
		{
			var src = (byte*)&data;
			fixed (byte* dst = &destination[index])
			{
				SafeCopier(dst, destination.Length - index, src, sizeof(double), sizeof(double));
			}
		}

		/// <summary>
		/// Copies the bytes from <paramref name="data"/> to 
		/// <paramref name="destination"/> starting at <paramref name="index"/>
		/// either linearly or reversed based on the conversion settings.
		/// </summary>
		public void ToBytes(float data, byte[] destination, int index)
		{
			var src = (byte*)&data;
			fixed (byte* dst = &destination[index])
			{
				SafeCopier(dst, destination.Length - index, src, sizeof(float), sizeof(float));
			}
		}

		/// <summary>
		/// Copies the bytes from <paramref name="data"/> to 
		/// <paramref name="destination"/> starting at <paramref name="index"/>
		/// either linearly or reversed based on the conversion settings.
		/// </summary>
		public void ToBytes(short data, byte[] destination, int index)
		{
			var src = (byte*)&data;
			fixed (byte* dst = &destination[index])
			{
				SafeCopier(dst, destination.Length - index, src, sizeof(short), sizeof(short));
			}
		}

		/// <summary>
		/// Copies the bytes from <paramref name="data"/> to 
		/// <paramref name="destination"/> starting at <paramref name="index"/>
		/// either linearly or reversed based on the conversion settings.
		/// </summary>
		public void ToBytes(int data, byte[] destination, int index)
		{
			var src = (byte*)&data;
			fixed (byte* dst = &destination[index])
			{
				SafeCopier(dst, destination.Length - index, src, sizeof(int), sizeof(int));
			}
		}

		/// <summary>
		/// Copies the bytes from <paramref name="data"/> to 
		/// <paramref name="destination"/> starting at <paramref name="index"/>
		/// either linearly or reversed based on the conversion settings.
		/// </summary>
		public void ToBytes(long data, byte[] destination, int index)
		{
			var src = (byte*)&data;
			fixed (byte* dst = &destination[index])
			{
				SafeCopier(dst, destination.Length - index, src, sizeof(long), sizeof(long));
			}
		}

		/// <summary>
		/// Copies the bytes from <paramref name="data"/> to 
		/// <paramref name="destination"/> starting at <paramref name="index"/>
		/// either linearly or reversed based on the conversion settings.
		/// </summary>
		public void ToBytes(ushort data, byte[] destination, int index)
		{
			var src = (byte*)&data;
			fixed (byte* dst = &destination[index])
			{
				SafeCopier(dst, destination.Length - index, src, sizeof(ushort), sizeof(ushort));
			}
		}

		/// <summary>
		/// Copies the bytes from <paramref name="data"/> to 
		/// <paramref name="destination"/> starting at <paramref name="index"/>
		/// either linearly or reversed based on the conversion settings.
		/// </summary>
		public void ToBytes(uint data, byte[] destination, int index)
		{
			var src = (byte*)&data;
			fixed (byte* dst = &destination[index])
			{
				SafeCopier(dst, destination.Length - index, src, sizeof(uint), sizeof(uint));
			}
		}

		/// <summary>
		/// Copies the bytes from <paramref name="data"/> to 
		/// <paramref name="destination"/> starting at <paramref name="index"/>
		/// either linearly or reversed based on the conversion settings.
		/// </summary>
		public void ToBytes(ulong data, byte[] destination, int index)
		{
			var src = (byte*)&data;
			fixed (byte* dst = &destination[index])
			{
				SafeCopier(dst, destination.Length - index, src, sizeof(ulong), sizeof(ulong));
			}
		}

		/// <summary>
		/// Returns the type from <paramref name="data"/> starting at <paramref name="index"/>
		/// in the correct byte order based on the conversion settings.
		/// </summary>
		public char ToChar(byte[] data, int index)
		{
			char result;
			var dst = (byte*)&result;
			fixed (byte* src = &data[index])
			{
				SafeCopier(dst, sizeof(char), src, data.Length - index, sizeof(char));
			}
			return result;
		}

		/// <summary>
		/// Returns the type from <paramref name="data"/> starting at <paramref name="index"/>
		/// in the correct byte order based on the conversion settings.
		/// </summary>
		public double ToDouble(byte[] data, int index)
		{
			double result;
			var dst = (byte*)&result;
			fixed (byte* src = &data[index])
			{
				SafeCopier(dst, sizeof(double), src, data.Length - index, sizeof(double));
			}
			return result;
		}

		/// <summary>
		/// Returns the type from <paramref name="data"/> starting at <paramref name="index"/>
		/// in the correct byte order based on the conversion settings.
		/// </summary>
		public float ToSingle(byte[] data, int index)
		{
			float result;
			var dst = (byte*)&result;
			fixed (byte* src = &data[index])
			{
				SafeCopier(dst, sizeof(float), src, data.Length - index, sizeof(float));
			}
			return result;
		}

		/// <summary>
		/// Returns the type from <paramref name="data"/> starting at <paramref name="index"/>
		/// in the correct byte order based on the conversion settings.
		/// </summary>
		public short ToInt16(byte[] data, int index)
		{
			short result;
			var dst = (byte*)&result;
			fixed (byte* src = &data[index])
			{
				SafeCopier(dst, sizeof(short), src, data.Length - index, sizeof(short));
			}
			return result;
		}

		/// <summary>
		/// Returns the type from <paramref name="data"/> starting at <paramref name="index"/>
		/// in the correct byte order based on the conversion settings.
		/// </summary>
		public int ToInt32(byte[] data, int index)
		{
			int result;
			var dst = (byte*)&result;
			fixed (byte* src = &data[index])
			{
				SafeCopier(dst, sizeof(int), src, data.Length - index, sizeof(int));
			}
			return result;
		}

		/// <summary>
		/// Returns the type from <paramref name="data"/> starting at <paramref name="index"/>
		/// in the correct byte order based on the conversion settings.
		/// </summary>
		public long ToInt64(byte[] data, int index)
		{
			long result;
			var dst = (byte*)&result;
			fixed (byte* src = &data[index])
			{
				SafeCopier(dst, sizeof(long), src, data.Length - index, sizeof(long));
			}
			return result;
		}

		/// <summary>
		/// Returns the type from <paramref name="data"/> starting at <paramref name="index"/>
		/// in the correct byte order based on the conversion settings.
		/// </summary>
		public ushort ToUInt16(byte[] data, int index)
		{
			ushort result;
			var dst = (byte*)&result;
			fixed (byte* src = &data[index])
			{
				SafeCopier(dst, sizeof(ushort), src, data.Length - index, sizeof(ushort));
			}
			return result;
		}

		/// <summary>
		/// Returns the type from <paramref name="data"/> starting at <paramref name="index"/>
		/// in the correct byte order based on the conversion settings.
		/// </summary>
		public uint ToUInt32(byte[] data, int index)
		{
			uint result;
			var dst = (byte*)&result;
			fixed (byte* src = &data[index])
			{
				SafeCopier(dst, sizeof(uint), src, data.Length - index, sizeof(uint));
			}
			return result;
		}

		/// <summary>
		/// Returns the type from <paramref name="data"/> starting at <paramref name="index"/>
		/// in the correct byte order based on the conversion settings.
		/// </summary>
		public ulong ToUInt64(byte[] data, int index)
		{
			ulong result;
			var dst = (byte*)&result;
			fixed (byte* src = &data[index])
			{
				SafeCopier(dst, sizeof(ulong), src, data.Length - index, sizeof(ulong));
			}
			return result;
		}

		/// <summary>
		/// Converts <paramref name="data"/> to the target Endian format.
		/// </summary>
		public char Convert(char data)
		{
			const int size = sizeof(char);
			char result;
			var dst = (byte*)&result;
			var src = (byte*)&data;
			_rawCopier(dst, src, size);
			return result;
		}

		/// <summary>
		/// Converts <paramref name="data"/> to the target Endian format.
		/// </summary>
		public double Convert(double data)
		{
			const int size = sizeof(double);
			double result;
			var dst = (byte*)&result;
			var src = (byte*)&data;
			_rawCopier(dst, src, size);
			return result;
		}

		/// <summary>
		/// Converts <paramref name="data"/> to the target Endian format.
		/// </summary>
		public float Convert(float data)
		{
			const int size = sizeof(float);
			float result;
			var dst = (byte*)&result;
			var src = (byte*)&data;
			_rawCopier(dst, src, size);
			return result;
		}

		/// <summary>
		/// Converts <paramref name="data"/> to the target Endian format.
		/// </summary>
		public short Convert(short data)
		{
			const int size = sizeof(short);
			short result;
			var dst = (byte*)&result;
			var src = (byte*)&data;
			_rawCopier(dst, src, size);
			return result;
		}

		/// <summary>
		/// Converts <paramref name="data"/> to the target Endian format.
		/// </summary>
		public int Convert(int data)
		{
			const int size = sizeof(int);
			int result;
			var dst = (byte*)&result;
			var src = (byte*)&data;
			_rawCopier(dst, src, size);
			return result;
		}

		/// <summary>
		/// Converts <paramref name="data"/> to the target Endian format.
		/// </summary>
		public long Convert(long data)
		{
			const int size = sizeof(long);
			long result;
			var dst = (byte*)&result;
			var src = (byte*)&data;
			_rawCopier(dst, src, size);
			return result;
		}

		/// <summary>
		/// Converts <paramref name="data"/> to the target Endian format.
		/// </summary>
		public ushort Convert(ushort data)
		{
			const int size = sizeof(ushort);
			ushort result;
			var dst = (byte*)&result;
			var src = (byte*)&data;
			_rawCopier(dst, src, size);
			return result;
		}

		/// <summary>
		/// Converts <paramref name="data"/> to the target Endian format.
		/// </summary>
		public uint Convert(uint data)
		{
			const int size = sizeof(uint);
			uint result;
			var dst = (byte*)&result;
			var src = (byte*)&data;
			_rawCopier(dst, src, size);
			return result;
		}

		/// <summary>
		/// Converts <paramref name="data"/> to the target Endian format.
		/// </summary>
		public ulong Convert(ulong data)
		{
			const int size = sizeof(ulong);
			ulong result;
			var dst = (byte*)&result;
			var src = (byte*)&data;
			_rawCopier(dst, src, size);
			return result;
		}

		// ReSharper disable UnusedParameter.Local
		private void SafeCopier(byte* dst, int dstSize, byte* src, int srcSize, int count)
		// ReSharper restore UnusedParameter.Local
		{
			if (Math.Min(srcSize, count) > dstSize)
			{
				throw new ArgumentOutOfRangeException();
			}
			if (count > srcSize)
			{
				throw new ArgumentOutOfRangeException();
			}
			_rawCopier(dst, src, count);
		}

		private static void CopyLinear(byte* dst, byte* src, int count)
		{
			for (int i = 0; i < count; ++i)
			{
				dst[i] = src[i];
			}
		}

		private static void CopyReverse(byte* dst, byte* src, int count)
		{
			for (int i = 0; i < count; ++i)
			{
				dst[count - i - 1] = src[i];
			}
		}

		#region Nested type: RawCopierDelegate

		private delegate void RawCopierDelegate(byte* dst, byte* src, int count);

		#endregion
	};
}