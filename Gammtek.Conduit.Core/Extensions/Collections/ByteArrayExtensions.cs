namespace Gammtek.Conduit.Extensions.Collections
{
	public static class ByteArrayExtensions
	{
		public static int CompareTo(this byte[] self, byte[] other)
		{
			if (other.Length > self.Length)
			{
				return -1;
			}

			if (other.Length < self.Length)
			{
				return 1;
			}

			for (var i = 0; i < self.Length; i++)
			{
				if (other[i] > self[i])
				{
					return -1;
				}
				if (other[i] < self[i])
				{
					return 1;
				}
			}

			return 0;
		}
	}
}
