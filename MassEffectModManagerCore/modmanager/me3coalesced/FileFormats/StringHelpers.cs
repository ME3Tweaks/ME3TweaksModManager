namespace MassEffect3.FileFormats
{
	public static class StringHelpers
	{
		public static uint HashCrc32(this string input)
		{
			return input.HashCrc32(0);
		}

		public static uint HashCrc32(this string input, uint hash)
		{
			hash = ~hash;
			// ReSharper disable LoopCanBeConvertedToQuery
			foreach (var t in input)
				// ReSharper restore LoopCanBeConvertedToQuery
			{
				hash = Crc32.Table[(hash >> 24) ^ t] ^ (hash << 8);
			}
			return ~hash;
		}
	}
}