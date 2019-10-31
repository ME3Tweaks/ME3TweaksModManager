using System;
using System.IO;

namespace MassEffect3.FileFormats
{
	public static class StreamHelpers
	{
		public static MemoryStream ReadToMemoryStream(this Stream stream, long size)
		{
			var memory = new MemoryStream();

			var left = size;
			var data = new byte[4096];
			while (left > 0)
			{
				var block = (int) (Math.Min(left, 4096));
				stream.Read(data, 0, block);
				memory.Write(data, 0, block);
				left -= block;
			}

			memory.Seek(0, SeekOrigin.Begin);
			return memory;
		}

		public static void WriteFromStream(this Stream stream, Stream input, long size)
		{
			var left = size;
			var data = new byte[4096];
			while (left > 0)
			{
				var block = (int) (Math.Min(left, 4096));
				input.Read(data, 0, block);
				stream.Write(data, 0, block);
				left -= block;
			}
		}
	}
}
