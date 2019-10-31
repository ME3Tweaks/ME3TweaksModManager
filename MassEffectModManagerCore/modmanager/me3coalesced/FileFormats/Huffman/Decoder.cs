using System.Collections;
using System.Text;

namespace MassEffect3.FileFormats.Huffman
{
	public static class Decoder
	{
		public static string Decode(Pair[] tree, BitArray data, int offset, int maxLength)
		{
			var sb = new StringBuilder();

			var start = tree.Length - 1;

			while (true)
			{
				var node = start;
				do
				{
					node = data[offset] == false
						? tree[node].Left
						: tree[node].Right;
					offset++;
				} while (node >= 0);

				var c = (ushort) (-1 - node);

				if (c == 0)
				{
					break;
				}

				sb.Append((char) c);

				if (sb.Length >= maxLength)
				{
					break;
				}
			}

			return sb.ToString();
		}
	}
}