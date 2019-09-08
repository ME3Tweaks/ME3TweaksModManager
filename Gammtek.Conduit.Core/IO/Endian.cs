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
using Gammtek.Conduit.IO.Converters;

namespace Gammtek.Conduit.IO
{
	/// <summary>
	///     Endian identification support for the current platform
	///     and streams.  Supports conversion of data between Endian
	///     settings.
	///     Note: this class still assumes that bit orderings are
	///     the same regardless of byte Endian configurations.
	/// </summary>
	public class Endian
	{
		static Endian()
		{
			Little = new Endian();
			Big = new Endian();
			Native = BitConverter.IsLittleEndian ? Little : Big;
			NonNative = BitConverter.IsLittleEndian ? Big : Little;
		}

		private Endian() {}

		/// <summary>
		///     Retrieve the big Endian instance.
		/// </summary>
		public static Endian Big { get; }

		/// <summary>
		///     Gets a value indicating whether this instance is native.
		/// </summary>
		/// <value>
		///     <c>true</c> if this instance is native; otherwise, <c>false</c>.
		/// </value>
		public bool IsNative => this == Native;

		/// <summary>
		///     Retrieve the little Endian instance.
		/// </summary>
		public static Endian Little { get; }

		/// <summary>
		///     Retrieve the platform native Endian instance.
		/// </summary>
		public static Endian Native { get; }

		/// <summary>
		///     Retrieve the non-native Endian instance.
		/// </summary>
		public static Endian NonNative { get; private set; }

		/// <summary>
		///     Retrieves the other Endian instance from the current.
		/// </summary>
		public Endian Switch => this == Big ? Little : Big;

		/// <summary>
		///     Creates a converter for changing data from the current
		///     Endian setting to the target Endian setting.
		/// </summary>
		public EndianConverter To(Endian target)
		{
			return EndianConverter.Create(this != target);
		}
	};
}
