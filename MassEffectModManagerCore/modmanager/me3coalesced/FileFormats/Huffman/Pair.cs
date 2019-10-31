namespace MassEffect3.FileFormats.Huffman
{
	public class Pair
	{
		public int Left;
		public int Right;

		public Pair()
			: this(0, 0)
		{}

		public Pair(int left, int right)
		{
			Left = left;
			Right = right;
		}
	}
}