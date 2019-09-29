using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using MassEffectModManagerCore.modmanager;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace MassEffectModManagerCore.gamefileformats
{
    class HuffmanCompressionME1
    {
        private List<TalkFileME1.TLKStringRef> _inputData = new List<TalkFileME1.TLKStringRef>();
        private Dictionary<char, int> frequencyCount = new Dictionary<char, int>();
        private List<HuffmanNode> _huffmanTree = new List<HuffmanNode>();
        private Dictionary<char, BitArray> _huffmanCodes = new Dictionary<char, BitArray>();

        //private class TLKEntry
        //{
        //    public int StringID;
        //    public int Flags;
        //    public string data;
        //    public int index;

        //    public TLKEntry(int StringID, int flags, string data)
        //    {
        //        this.StringID = StringID;
        //        this.Flags = flags;
        //        this.data = data;
        //        index = -1;
        //    }
        //}

        private class HuffmanNode
        {
            public char Data;
            public readonly int FrequencyCount;
            public HuffmanNode Left;
            public HuffmanNode Right;

            public ushort ID;
            public bool leaf;

            public HuffmanNode(char d, int freq)
            {
                leaf = true;
                Data = d;
                FrequencyCount = freq;
            }

            public HuffmanNode(HuffmanNode left, HuffmanNode right)
            {
                leaf = false;
                FrequencyCount = left.FrequencyCount + right.FrequencyCount;
                Left = left;
                Right = right;
            }
        }

        private struct EncodedString
        {
            public int stringLength;
            public int encodedLength;
            public byte[] binaryData;

            public EncodedString(int _stringLength, int _encodedLength, byte[] _data)
            {
                stringLength = _stringLength;
                encodedLength = _encodedLength;
                binaryData = _data;
            }
        }

        /// <summary>
        /// Loads a file into memory and prepares for compressing it to TLK
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadInputData(string fileName)
        {
            _inputData.Clear();
            LoadXmlInputData(fileName);
            PrepareHuffmanCoding();
        }

        public void LoadInputData(List<TalkFileME1.TLKStringRef> tlkEntries)
        {
            _inputData = tlkEntries;
            PrepareHuffmanCoding();
        }

        public void serializeTLKStrListToExport(ExportEntry export, bool savePackage = false)
        {
            if (export.FileRef.Game != Mod.MEGame.ME1)
            {
                throw new Exception("Cannot save a ME1 TLK to a game that is not Mass Effect 1.");
            }
            serializeTalkfileToExport(export, savePackage);
        }

        public void serializeTalkfileToExport(ExportEntry export, bool savePackage = false)
        {
            /* converts Huffmann Tree to binary form */
            byte[] treeBuffer = ConvertHuffmanTreeToBuffer();

            var encodedStrings = new List<EncodedString>();
            int i = 0;
            foreach (var entry in _inputData)
            {
                if (entry.Flags == 0)
                {
                    if (entry.StringID > 0)
                        entry.Index = -1;
                    else
                        entry.Index = 0;
                }
                else
                {
                    entry.Index = i;
                    i++;
                    var binaryData = new List<BitArray>();
                    int binaryLength = 0;

                    /* for every character in a string, put it's binary code into data array */
                    foreach (char c in entry.ASCIIData)
                    {
                        binaryData.Add(_huffmanCodes[c]);
                        binaryLength += _huffmanCodes[c].Count;
                    }
                    byte[] buffer = BitArrayListToByteArray(binaryData, binaryLength);
                    encodedStrings.Add(new EncodedString(entry.ASCIIData.Length, buffer.Length, buffer));
                }
            }


            /* writing properties */
            export.WriteProperty(new IntProperty(_inputData.Count, "m_nHashTableSize"));

            MemoryStream m = new MemoryStream();
            /* writing entries */
            m.Write(BitConverter.GetBytes(_inputData.Count), 0, 4);
            foreach (TalkFileME1.TLKStringRef entry in _inputData)
            {
                m.Write(BitConverter.GetBytes(entry.StringID), 0, 4);
                m.Write(BitConverter.GetBytes(entry.Flags), 0, 4);
                m.Write(BitConverter.GetBytes(entry.Index), 0, 4);
            }

            /* writing HuffmanTree */
            m.Write(treeBuffer, 0, treeBuffer.Length);

            /* writing data */
            m.Write(BitConverter.GetBytes(encodedStrings.Count), 0, 4);
            foreach (EncodedString enc in encodedStrings)
            {
                m.Write(BitConverter.GetBytes(enc.stringLength), 0, 4);
                m.Write(BitConverter.GetBytes(enc.encodedLength), 0, 4);
                m.Write(enc.binaryData, 0, enc.encodedLength);
            }

            byte[] buff = m.ToArray();
            export.setBinaryData(buff);
            if (savePackage)
            {
                export.FileRef.save(export.FileRef.FilePath);
            }
        }

        /// <summary>
        /// Loads data from XML file into memory
        /// </summary>
        /// <param name="fileName"></param>
        private void LoadXmlInputData(string fileName)
        {
            var xmlReader = new XmlTextReader(fileName);

            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "string")
                {
                    int id = 0, flags = 0;
                    string data = "";
                    while (xmlReader.Name != "string" || xmlReader.NodeType != XmlNodeType.EndElement)
                    {
                        if (!xmlReader.Read() || xmlReader.NodeType != XmlNodeType.Element)
                            continue;
                        if (xmlReader.Name == "id")
                            id = xmlReader.ReadElementContentAsInt();
                        else if (xmlReader.Name == "flags")
                            flags = xmlReader.ReadElementContentAsInt();
                        else if (xmlReader.Name == "data")
                            data = xmlReader.ReadString();
                    }
                    data = data.Replace("\r\n", "\n");
                    /* every string should be NULL-terminated */
                    if (id > 0)
                        data += '\0';
                    _inputData.Add(new TalkFileME1.TLKStringRef(id, flags, data));
                }
            }
            xmlReader.Close();
        }

        /// <summary>
        /// Creates Huffman Tree based on data from memory.
        /// For every character in text data, a corresponding Huffman Code is prepared.
        /// Source: http://en.wikipedia.org/wiki/Huffman_coding
        /// </summary>
        private void PrepareHuffmanCoding()
        {
            frequencyCount.Clear();
            foreach (var entry in _inputData)
            {
                if (entry.StringID <= 0)
                    continue;
                foreach (char c in entry.ASCIIData)
                {
                    if (!frequencyCount.ContainsKey(c))
                        frequencyCount.Add(c, 0);
                    ++frequencyCount[c];
                }
            }

            foreach (var element in frequencyCount)
                _huffmanTree.Add(new HuffmanNode(element.Key, element.Value));

            BuildHuffmanTree();
            BuildCodingArray();
        }

        /// <summary>
        /// Standard implementation of building a Huffman Tree
        /// </summary>
        private void BuildHuffmanTree()
        {
            while (_huffmanTree.Count() > 1)
            {
                /* sort Huffman Nodes by frequency */
                _huffmanTree.Sort(CompareNodes);

                HuffmanNode parent = new HuffmanNode(_huffmanTree[0], _huffmanTree[1]);
                _huffmanTree.RemoveAt(0);
                _huffmanTree.RemoveAt(0);
                _huffmanTree.Add(parent);
            }
        }

        /// <summary>
        /// Using Huffman Tree (created with BuildHuffmanTree method), generates a binary code for every character.
        /// </summary>
        private void BuildCodingArray()
        {
            /* stores a binary code */
            List<bool> currentCode = new List<bool>();
            HuffmanNode currenNode = _huffmanTree[0];

            TraverseHuffmanTree(currenNode, currentCode);
        }

        /// <summary>
        /// Recursively traverses Huffman Tree and generates codes
        /// </summary>
        /// <param name="node"></param>
        /// <param name="code"></param>
        private void TraverseHuffmanTree(HuffmanNode node, List<bool> code)
        {
            /* check if both sons are null */
            if (node.Left == node.Right)
            {
                BitArray ba = new BitArray(code.ToArray());
                _huffmanCodes.Add(node.Data, ba);
            }
            else
            {
                /* adds 0 to the code - process left son*/
                code.Add(false);
                TraverseHuffmanTree(node.Left, code);
                code.RemoveAt(code.Count() - 1);

                /* adds 1 to the code - process right son*/
                code.Add(true);
                TraverseHuffmanTree(node.Right, code);
                code.RemoveAt(code.Count() - 1);
            }
        }

        /// <summary>
        /// Converts a Huffman Tree to it's binary representation used by TLK format of Mass Effect 1.
        /// </summary>
        /// <returns></returns>
        private byte[] ConvertHuffmanTreeToBuffer()
        {
            List<HuffmanNode> nodes = new List<HuffmanNode>();
            Queue<HuffmanNode> q = new Queue<HuffmanNode>();

            ushort index = 0;
            q.Enqueue(_huffmanTree[0]);

            while (q.Count > 0)
            {
                HuffmanNode node = q.Dequeue();
                nodes.Add(node);
                node.ID = index;
                index++;
                if (node.Right != null)
                {
                    q.Enqueue(node.Right);
                }
                if (node.Left != null)
                {
                    q.Enqueue(node.Left);
                }
            }

            List<byte> output = new List<byte>();
            output.AddRange(BitConverter.GetBytes((int)index));
            foreach (HuffmanNode node in nodes)
            {
                if (node.leaf)
                {
                    output.Add(1);
                    output.AddRange(BitConverter.GetBytes(node.Data));
                }
                else
                {
                    output.Add(0);
                    output.AddRange(BitConverter.GetBytes(node.Right.ID));
                    output.AddRange(BitConverter.GetBytes(node.Left.ID));
                }
            }

            return output.ToArray();
        }

        /// <summary>
        /// Converts bits in a BitArray to an array with bytes.
        /// Such array is ready to be written to a file.
        /// </summary>
        /// <param name="bitsList"></param>
        /// <param name="bitsCount"></param>
        /// <returns></returns>
        private static byte[] BitArrayListToByteArray(List<BitArray> bitsList, int bitsCount)
        {
            const int BITSPERBYTE = 8;

            int bytesize = bitsCount / BITSPERBYTE;
            if (bitsCount % BITSPERBYTE > 0)
                bytesize++;

            byte[] bytes = new byte[bytesize];
            int bytepos = 0;
            int bitsRead = 0;
            byte value = 0;
            byte significance = 1;

            foreach (BitArray bits in bitsList)
            {
                int bitpos = 0;

                while (bitpos < bits.Length)
                {
                    if (bits[bitpos])
                    {
                        value += significance;
                    }
                    ++bitpos;
                    ++bitsRead;
                    if (bitsRead % BITSPERBYTE == 0)
                    {
                        bytes[bytepos] = value;
                        ++bytepos;
                        value = 0;
                        significance = 1;
                        bitsRead = 0;
                    }
                    else
                    {
                        significance <<= 1;
                    }
                }
            }
            if (bitsRead % BITSPERBYTE != 0)
                bytes[bytepos] = value;
            return bytes;
        }

        /// <summary>
        /// For sorting Huffman Nodes
        /// </summary>
        /// <param name="L1"></param>
        /// <param name="L2"></param>
        /// <returns></returns>
        private static int CompareNodes(HuffmanNode L1, HuffmanNode L2)
        {
            return L1.FrequencyCount.CompareTo(L2.FrequencyCount);
        }
    }
}
