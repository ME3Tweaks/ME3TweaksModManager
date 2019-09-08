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
	///     A BinaryWriter implementation to write individual bits to a stream.
	/// </summary>
	public class BitBinaryWriter : BinaryWriter
	{
		private readonly BitStream _stream;

		/// <summary>
		///     Initializes a new instance of the <see cref="BitBinaryWriter" /> class
		///     with the underlying <paramref name="stream" /> and <paramref name="encoding" />.
		/// </summary>
		public BitBinaryWriter(Stream stream, Encoding encoding)
			: this(new BitStreamWriter(stream), encoding) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitBinaryWriter" /> class
		///     with the underlying <paramref name="stream" /> and default encoding (UTF8).
		/// </summary>
		public BitBinaryWriter(Stream stream)
			: this(stream, Encoding.UTF8) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitBinaryWriter" /> class
		///     with the underlying <paramref name="stream" /> and <paramref name="encoding" />.
		/// </summary>
		public BitBinaryWriter(BitStream stream, Encoding encoding)
			: base(stream, encoding)
		{
			_stream = stream;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitBinaryWriter" /> class
		///     with the underlying <paramref name="stream" /> and default encoding (UTF8).
		/// </summary>
		public BitBinaryWriter(BitStream stream)
			: this(stream, Encoding.UTF8) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="BitBinaryWriter" /> class.
		/// </summary>
		public BitBinaryWriter()
			: this(null, Encoding.UTF8) {}

		/// <summary>
		///     Writes a one-bit Boolean value to the current stream, with 0 representing false and 1 representing true.
		/// </summary>
		public override void Write(bool value)
		{
			_stream.Write(value);
		}
	}
}
