using System.IO;

namespace Gammtek.Conduit.Extensions.IO
{
	public static class BinaryReaderExtensions
	{
		public static long Seek(this BinaryReader reader, long offset, SeekOrigin origin = SeekOrigin.Begin)
		{
			return reader.BaseStream.Seek(offset, origin);
		}
	}
}
