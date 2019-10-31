using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MassEffect3.FileFormats.Coalesced;
using MassEffect3.FileFormats.Huffman;
using MassEffectModManagerCore.modmanager.helpers;
using Decoder = MassEffect3.FileFormats.Huffman.Decoder;
using Encoder = MassEffect3.FileFormats.Huffman.Encoder;

namespace MassEffect3.FileFormats
{
	public class CoalescedFileXml
	{
		public CoalescedFileXml(List<FileEntry> files = null, IEnumerable<int> compileTypes = null, int overrideCompileValueTypes = -1, uint version = 0)
		{
			Files = files ?? new List<FileEntry>();
			OverrideCompileValueTypes = overrideCompileValueTypes;
			Version = version;
			CompileTypes = compileTypes ?? new[] { 0, 1, 2, 3, 4 };
		}

		public List<FileEntry> Files { get; set; }
		public uint Version { get; set; }

		public int OverrideCompileValueTypes { get; set; }

		public IEnumerable<int> CompileTypes { get; set; }

		public void Serialize(Stream output)
		{
			const uint headerSize = 32;
			output.WriteUInt32(0x42424947);
			output.WriteUInt32(Version);

			var keys = new List<string>
			{
				""
			};

			var maxValueLength = 0;
			var blob = new StringBuilder();

			foreach (var file in Files)
			{
				keys.Add(file.Name);

				foreach (var section in file.Sections)
				{
					keys.Add(section.Key);

					foreach (var value in section.Value)
					{
						keys.Add(value.Key);

						foreach (var item in value.Value)
						{
							if (item.Value != null)
							{
								blob.Append(item.Value + '\0');
								maxValueLength = Math.Max(maxValueLength, item.Value.Length);
							}
						}
					}
				}
			}

			var huffmanEncoder = new Encoder();
			huffmanEncoder.Build(blob.ToString());

			keys = keys.Distinct().OrderBy(k => k.HashCrc32()).ToList();
			var maxKeyLength = keys.Max(k => k.Length);

			uint stringTableSize;

			using (var data = new MemoryStream())
			{
				data.Position = 4;
				data.WriteInt32(keys.Count);

				data.Position = 4 + 4 + (8 * keys.Count);
				var offsets = new List<KeyValuePair<uint, uint>>();

				foreach (var key in keys)
				{
					var offset = (uint) data.Position;
					data.WriteUInt16((ushort) key.Length);
                    //data.WriteString(key, Encoding.UTF8);
                    data.WriteStringUTF8(key);
                    offsets.Add(new KeyValuePair<uint, uint>(key.HashCrc32(), offset));
				}

				data.Position = 8;

				foreach (var kv in offsets)
				{
					data.WriteUInt32(kv.Key);
					data.WriteUInt32(kv.Value - 8);
				}

				data.Position = 0;
				data.WriteUInt32((uint) data.Length);

				data.Position = 0;
				stringTableSize = (uint) data.Length;

				output.Seek(headerSize, SeekOrigin.Begin);
				output.WriteFromStream(data, data.Length);
			}

			uint huffmanSize;
			using (var data = new MemoryStream())
			{
				var pairs = huffmanEncoder.GetPairs();
				data.WriteUInt16((ushort) pairs.Length);
				foreach (var pair in pairs)
				{
					data.WriteInt32(pair.Left);
					data.WriteInt32(pair.Right);
				}

				data.Position = 0;
				huffmanSize = (uint) data.Length;

				output.Seek(headerSize + stringTableSize, SeekOrigin.Begin);
				output.WriteFromStream(data, data.Length);
			}

			var bits = new BitArray(huffmanEncoder.TotalBits);
			var bitOffset = 0;

			uint indexSize;

			using (var index = new MemoryStream())
			{
				var fileDataOffset = 2 + (Files.Count * 6);

				var files = new List<KeyValuePair<ushort, int>>();

				foreach (var file in Files.OrderBy(f => keys.IndexOf(f.Name)))
				{
					files.Add(new KeyValuePair<ushort, int>((ushort) keys.IndexOf(file.Name), fileDataOffset));

					var sectionDataOffset = 2 + (file.Sections.Count * 6);

					var sections = new List<KeyValuePair<ushort, int>>();

					foreach (var section in file.Sections.OrderBy(s => keys.IndexOf(s.Key)))
					{
						sections.Add(new KeyValuePair<ushort, int>((ushort) keys.IndexOf(section.Key), sectionDataOffset));

						var valueDataOffset = 2 + (section.Value.Count * 6);

						var values = new List<KeyValuePair<ushort, int>>();

						foreach (var value in section.Value.OrderBy(v => keys.IndexOf(v.Key)))
						{
							index.Position = fileDataOffset + sectionDataOffset + valueDataOffset;

							values.Add(new KeyValuePair<ushort, int>((ushort) keys.IndexOf(value.Key), valueDataOffset));

							index.WriteUInt16((ushort) value.Value.Count);
							valueDataOffset += 2;

							foreach (var item in value.Value)
							{
								switch (item.Type)
								{
									case -1:
									{
										continue;
									}
									case 1:
									{
										index.WriteInt32((1 << 29) | bitOffset);

										break;
									}
									case 0:
									case 2:
									case 3:
									case 4:
									{
										var type = item.Type;

										if (OverrideCompileValueTypes >= 0)
										{
											type = OverrideCompileValueTypes;
										}

										index.WriteInt32((type << 29) | bitOffset);
										bitOffset += huffmanEncoder.Encode((item.Value ?? "") + '\0', bits, bitOffset);

										break;
									}
								}

								valueDataOffset += 4;
							}
						}

						index.Position = fileDataOffset + sectionDataOffset;

						index.WriteUInt16((ushort) values.Count);
						sectionDataOffset += 2;

						foreach (var value in values)
						{
							index.WriteUInt16(value.Key);
							index.WriteInt32(value.Value);

							sectionDataOffset += 6;
						}

						sectionDataOffset += valueDataOffset;
					}

					index.Position = fileDataOffset;

					index.WriteUInt16((ushort) sections.Count);
					fileDataOffset += 2;

					foreach (var section in sections)
					{
						index.WriteUInt16(section.Key);
						index.WriteInt32(section.Value);

						fileDataOffset += 6;
					}

					fileDataOffset += sectionDataOffset;
				}

				index.Position = 0;

				index.WriteUInt16((ushort) files.Count);

				foreach (var file in files)
				{
					index.WriteUInt16(file.Key);
					index.WriteInt32(file.Value);
				}

				index.Position = 0;
				indexSize = (uint) index.Length;

				output.Seek(headerSize + stringTableSize + huffmanSize, SeekOrigin.Begin);
				output.WriteFromStream(index, index.Length);
			}

			output.Seek(headerSize + stringTableSize + huffmanSize + indexSize, SeekOrigin.Begin);
			output.WriteInt32(bits.Length);

			var bytes = new byte[(bits.Length - 1) / 8 + 1];
			bits.CopyTo(bytes, 0);
			output.Write(bytes);

			output.Seek(8, SeekOrigin.Begin);
			output.WriteInt32(maxKeyLength);
			output.WriteInt32(maxValueLength);
			output.WriteUInt32(stringTableSize);
			output.WriteUInt32(huffmanSize);
			output.WriteUInt32(indexSize);
			output.WriteInt32(bytes.Length);

			output.Seek(0, SeekOrigin.Begin);
			output.WriteUInt32(0x666D726D);
		}

		public void Deserialize(Stream input)
		{
			var magic = input.ReadUInt32();

			if (magic != 0x666D726D)
			{
				throw new FormatException();
			}

			var version = input.ReadUInt32();

			if (version != 1)
			{
				throw new FormatException();
			}

			Version = version;

			input.ReadInt32();
			var maxValueLength = input.ReadInt32();

			var stringTableSize = input.ReadUInt32();
			var huffmanSize = input.ReadUInt32();
			var indexSize = input.ReadUInt32();
			var dataSize = input.ReadUInt32();

			var strings = new List<KeyValuePair<uint, string>>();

			using (var data = input.ReadToMemoryStream(stringTableSize))
			{
				var localStringTableSize = data.ReadUInt32();

				if (localStringTableSize != stringTableSize)
				{
					throw new FormatException();
				}

				var count = data.ReadUInt32();

				var offsets = new List<KeyValuePair<uint, uint>>();

				for (uint i = 0; i < count; i++)
				{
					var hash = data.ReadUInt32();
					var offset = data.ReadUInt32();

					offsets.Add(new KeyValuePair<uint, uint>(hash, offset));
				}

				foreach (var kv in offsets)
				{
					var hash = kv.Key;
					var offset = kv.Value;

					data.Seek(8 + offset, SeekOrigin.Begin);
					var length = data.ReadUInt16();
                    //var text = data.ReadString(length, Encoding.UTF8);//original.
                    var text = data.ReadStringUTF8(length);

                    if (text.HashCrc32() != hash)
					{
						throw new InvalidOperationException();
					}

					strings.Add(new KeyValuePair<uint, string>(hash, text));
				}
			}

			Pair[] huffmanTree;

			using (var data = input.ReadToMemoryStream(huffmanSize))
			{
				var count = data.ReadUInt16();
				huffmanTree = new Pair[count];

				for (ushort i = 0; i < count; i++)
				{
					var left = data.ReadInt32();
					var right = data.ReadInt32();
					huffmanTree[i] = new Pair(left, right);
				}
			}

			using (var index = input.ReadToMemoryStream(indexSize))
			{
				var totalBits = input.ReadInt32();
				var data = input.ReadToBuffer(dataSize);
				var bitArray = new BitArray(data)
				{
					Length = totalBits
				};

				var files = new List<KeyValuePair<string, uint>>();
				var fileCount = index.ReadUInt16();

				for (ushort i = 0; i < fileCount; i++)
				{
					var nameIndex = index.ReadUInt16();
					var name = strings[nameIndex].Value;
					var offset = index.ReadUInt32();

					files.Add(new KeyValuePair<string, uint>(name, offset));
				}

				foreach (var fileInfo in files.OrderBy(f => f.Key))
				{
					var file = new FileEntry
					{
						Name = fileInfo.Key
					};

					index.Seek(fileInfo.Value, SeekOrigin.Begin);

					var sectionCount = index.ReadUInt16();
					var sections = new List<KeyValuePair<string, uint>>();

					for (ushort i = 0; i < sectionCount; i++)
					{
						var nameIndex = index.ReadUInt16();
						var name = strings[nameIndex].Value;
						var offset = index.ReadUInt32();

						sections.Add(new KeyValuePair<string, uint>(name, offset));
					}

					foreach (var sectionInfo in sections.OrderBy(s => s.Key))
					{
						var section = new Dictionary<string, List<PropertyValue>>();

						index.Seek(fileInfo.Value + sectionInfo.Value, SeekOrigin.Begin);
						var valueCount = index.ReadUInt16();
						var values = new List<KeyValuePair<string, uint>>();

						for (ushort i = 0; i < valueCount; i++)
						{
							var nameIndex = index.ReadUInt16();
							var name = strings[nameIndex].Value;
							var offset = index.ReadUInt32();

							values.Add(new KeyValuePair<string, uint>(name, offset));
						}

						foreach (var valueInfo in values.OrderBy(v => v.Key))
						{
							var value = new List<PropertyValue>();

							index.Seek(fileInfo.Value + sectionInfo.Value + valueInfo.Value, SeekOrigin.Begin);
							var itemCount = index.ReadUInt16();

							for (ushort i = 0; i < itemCount; i++)
							{
								var offset = index.ReadInt32();

								var type = (offset & 0xE0000000) >> 29;
								
								switch (type)
								{
									case 1:
									{
										value.Add(new PropertyValue(1, null));

										break;
									}
									case 0:
									case 2:
									case 3:
									case 4:
									{
										offset &= 0x1fffffff;
										var text = Decoder.Decode(huffmanTree, bitArray, offset, maxValueLength);

										value.Add(new PropertyValue((int) type, text));

										break;
									}
									default:
									{
										throw new NotImplementedException();
									}
								}
							}

							section.Add(valueInfo.Key, value);
						}

						file.Sections.Add(sectionInfo.Key, section);
					}

					Files.Add(file);
				}
			}
		}
	}
}