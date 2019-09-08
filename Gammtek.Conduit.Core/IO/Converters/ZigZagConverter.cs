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

namespace Gammtek.Conduit.IO.Converters
{
	/// <summary>
	/// The zig zag converter performs a 1-1 and onto mapping of signed integers
	/// onto unsigned integers using the transformation defined at:
	/// https://developers.google.com/protocol-buffers/docs/encoding
	/// 
	/// This is subsequently used in the VarInt code to handle variable sized 
	/// signed integers by remapping them to unsigned integers in a highly 
	/// compressible manner.
	/// </summary>
	public static class ZigZagConverter
	{
		/// <summary>Converts the signed value to an unsigned value invertibly.</summary>
		public static ulong ZigZag(this long value)
		{
			return (((ulong)(value << 1)) ^ ((ulong)(value >> 63)));
		}

		/// <summary>Converts the signed value to an unsigned value invertibly.</summary>
		public static uint ZigZag(this int value)
		{
			return (((uint)(value << 1)) ^ ((uint)(value >> 31)));
		}

		/// <summary>Converts the signed value to an unsigned value invertibly.</summary>
		public static ushort ZigZag(this short value)
		{
			return (ushort)(((ushort)(value << 1)) ^ ((ushort)(value >> 15)));
		}

		/// <summary>Converts the unsigned value to a signed value invertibly.</summary>
		public static long ZigZag(this ulong value)
		{
			return (((long)(value >> 1)) ^ (((value & 1) != 0 ? -1 : 0)));
		}

		/// <summary>Converts the unsigned value to a signed value invertibly.</summary>
		public static int ZigZag(this uint value)
		{
			return (((int)(value >> 1)) ^ (((value & 1) != 0 ? -1 : 0)));
		}

		/// <summary>Converts the unsigned value to a signed value invertibly.</summary>
		public static short ZigZag(this ushort value)
		{
			return (short)(((short)(value >> 1)) ^ ((short)((value & 1) != 0 ? -1 : 0)));
		}
	}
}