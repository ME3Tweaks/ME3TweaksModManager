using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MassEffect3.FileFormats.Huffman
{
    [Localizable(false)]
	public class Encoder
	{
		private readonly Dictionary<char, BitArray> _codes = new Dictionary<char, BitArray>();
		private Node _root;

		public int TotalBits { get; private set; }

		public void Build(string text)
		{
			if (text == null)
			{
				throw new ArgumentNullException(nameof(text));
			}

			_root = null;
			var frequencies = new Dictionary<char, int>();
			_codes.Clear();

			foreach (var t in text)
			{
			    if (!frequencies.TryGetValue(t, out int frequency))
				{
					frequency = 0;
				}
				frequencies[t] = frequency + 1;
			}

			var nodes = frequencies.Select(
				symbol => new Node
				{
					Symbol = symbol.Key,
					Frequency = symbol.Value,
				}).ToList();

			while (nodes.Count > 1)
			{
				var orderedNodes = nodes
					.OrderBy(n => n.Frequency).ToList();

				if (orderedNodes.Count >= 2)
				{
					var taken = orderedNodes.Take(2).ToArray();
					var first = taken[0];
					var second = taken[1];

					var parent = new Node
					{
						Symbol = '\0',
						Frequency = first.Frequency + second.Frequency,
						Left = first,
						Right = second,
					};

					nodes.Remove(first);
					nodes.Remove(second);
					nodes.Add(parent);
				}

				_root = nodes.FirstOrDefault();
			}

			foreach (var frequency in frequencies)
			{
				List<bool> bits = Traverse(_root, frequency.Key, new List<bool>());
				
				if (bits == null)
				{
					throw new InvalidOperationException($"could not traverse '{frequency.Key}'");
				}

				_codes.Add(frequency.Key, new BitArray(bits.ToArray()));
			}

			TotalBits = GetTotalBits(_root);
		}

		private static int GetTotalBits(Node root)
		{
			var queue = new Queue<Node>();
			queue.Enqueue(root);

			var totalBits = 0;

			while (queue.Count > 0)
			{
				var node = queue.Dequeue();

				if (node.Left == null && node.Right == null)
				{
					continue;
				}

				totalBits += node.Frequency;

				if (node.Left?.Left != null && node.Left.Right != null)
				{
					queue.Enqueue(node.Left);
				}

				if (node.Right?.Left != null && node.Right.Right != null)
				{
					queue.Enqueue(node.Right);
				}
			}

			return totalBits;
		}

		private static List<bool> Traverse(Node node, char symbol, List<bool> data)
		{
			if (node.Left == null && node.Right == null)
			{
				return symbol == node.Symbol ? data : null;
			}

			if (node.Left != null)
			{
				var path = new List<bool>();
				path.AddRange(data);
				path.Add(false);

				List<bool> left = Traverse(node.Left, symbol, path);

				if (left != null)
				{
					return left;
				}
			}

			if (node.Right != null)
			{
				var path = new List<bool>();
				path.AddRange(data);
				path.Add(true);

				List<bool> right = Traverse(node.Right, symbol, path);

				if (right != null)
				{
					return right;
				}
			}

			return null;
		}

		private int Encode(char symbol, BitArray bits, int offset)
		{
			var code = _codes[symbol];

			for (var i = 0; i < code.Length; i++)
			{
				bits[offset + i] = code[i];
			}

			return code.Length;
		}

		public int Encode(string text, BitArray bits, int offset)
		{
			if (text == null)
			{
				throw new ArgumentNullException(nameof(text));
			}

			var bitCount = 0;

			foreach (var t in text)
			{
				if (_codes.ContainsKey(t) == false)
				{
					throw new ArgumentException($"could not lookup '{t}'", nameof(text));
				}

				bitCount += Encode(t, bits, offset + bitCount);
			}

			return bitCount;
		}

		public Pair[] GetPairs()
		{
			var pairs = new List<Pair>();
			var mapping = new Dictionary<Node, Pair>();

			var queue = new Queue<Node>();
			queue.Enqueue(_root);

			var root = new Pair();
			mapping.Add(_root, root);

			while (queue.Count > 0)
			{
				var node = queue.Dequeue();
				var pair = mapping[node];

				if (node.Left == null && node.Right == null)
				{
					throw new InvalidOperationException();
				}

				// ReSharper disable PossibleNullReferenceException
				if (node.Left.Left == null &&
					// ReSharper restore PossibleNullReferenceException
					node.Left.Right == null)
				{
					pair.Left = -1 - node.Left.Symbol;
				}
				else
				{
					var left = new Pair();
					mapping.Add(node.Left, left);
					pairs.Add(left);

					queue.Enqueue(node.Left);

					pair.Left = pairs.IndexOf(left);
				}

				if (node.Right.Left == null &&
					node.Right.Right == null)
				{
					pair.Right = -1 - node.Right.Symbol;
				}
				else
				{
					var right = new Pair();
					mapping.Add(node.Right, right);
					pairs.Add(right);

					queue.Enqueue(node.Right);

					pair.Right = pairs.IndexOf(right);
				}
			}

			pairs.Add(root);

			return pairs.ToArray();
		}
	}
}