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

using System.IO;
using System.Text;

namespace Gammtek.Conduit.IO
{
	/// <summary>
	///     BinaryReader that supports reading and writing individual bits from
	///     the stream.
	/// </summary>
	public class BitBinaryReader : BinaryReader
	{
		private readonly BitStream _reader;

		/// <summary>
		///     Initializes a new instance of the <see cref="BitBinaryReader" /> class
		///     using stream <paramref name="input" /> and <paramref name="encoding" />.
		/// </summary>
		public BitBinaryReader(Stream input, Encoding encoding)
			: this(new BitStreamReader(input), encoding) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitBinaryReader" /> class
		///     using stream <paramref name="input" />.
		/// </summary>
		public BitBinaryReader(Stream input)
			: this(input, Encoding.UTF8) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitBinaryReader" /> class.
		/// </summary>
		public BitBinaryReader(BitStream input, Encoding encoding)
			: base(input, encoding)
		{
			_reader = input;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitBinaryReader" /> class.
		/// </summary>
		public BitBinaryReader(BitStream input)
			: this(input, Encoding.UTF8) {}

		/// <summary>
		///     Reads a Boolean value from the current stream and advances the current position of the stream by one bit.
		/// </summary>
		/// <returns>
		///     true if the bit is nonzero; otherwise, false.
		/// </returns>
		public override bool ReadBoolean()
		{
			return _reader.ReadBoolean();
		}
	}
}
