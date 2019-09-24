
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using ME3Explorer.Packages;
using System.Diagnostics;
using MassEffectModManager.modmanager;

namespace MassEffectModManager.gamefileformats
{
    public class TalkFileME1 : IEquatable<TalkFileME1>
    {
        #region structs
        public struct HuffmanNode
        {
            public int LeftNodeID;
            public int RightNodeID;
            public char data;

            public HuffmanNode(int r, int l)
                : this()
            {
                RightNodeID = r;
                LeftNodeID = l;
            }

            public HuffmanNode(char c)
                : this()
            {
                data = c;
                LeftNodeID = -1;
                RightNodeID = -1;
            }
        }

        [DebuggerDisplay("TLKStringRef {StringID} {Data}")]
        public class TLKStringRef : INotifyPropertyChanged, IEquatable<TLKStringRef>
        {
            private int _stringID;
            private string _data;
            private int _flags;
            private int _index;

            public event PropertyChangedEventHandler PropertyChanged;

            public int StringID { get; set; }
            public string Data { get; set; }
            public int Flags { get; set; }
            public int Index { get; set; }
            public int BitOffset { get => Flags; set => Flags = value; } //use same variable to save memory as flags is not used in me2/3, but bitoffset is.

            /// <summary>
            /// This is used by huffman compression
            /// </summary>
            public string ASCIIData
            {
                get
                {
                    if (Data == null)
                    {
                        return "-1\0";
                    }
                    if (Data.EndsWith("\0"))
                    {
                        return Data;
                    }
                    return Data + '\0';
                }
            }

            public TLKStringRef(BinaryReader r, bool me1)
            {
                StringID = r.ReadInt32();
                if (me1)
                {
                    Flags = r.ReadInt32();
                    Index = r.ReadInt32();
                }
                else
                {
                    BitOffset = r.ReadInt32();
                }
            }

            public TLKStringRef(int id, int flags, string data)
            {
                StringID = id;
                Flags = flags;
                Data = data;
                Index = -1;
            }

            public bool Equals(TLKStringRef other)
            {
                return StringID == other.StringID && ASCIIData == other.ASCIIData && Flags == other.Flags /*&& Index == other.Index*/;
            }
        }
        #endregion

        private List<HuffmanNode> nodes;
        private BitArray Bits;
        private int langRef;
        private readonly int tlkSetIndex;

        public TLKStringRef[] StringRefs;
        public IMEPackage pcc;
        //public int index;
        public int uindex;

        public int LangRef
        {
            get => langRef;
            set { langRef = value; language = pcc.getNameEntry(value); }
        }

        public string language;
        public bool male;

        public string Name => pcc.getUExport(uindex).ObjectName;
        public string BioTlkSetName => tlkSetIndex != -1 ? (pcc.Exports[tlkSetIndex].ObjectName + ".") : null;


        #region Constructors
        public TalkFileME1(IMEPackage _pcc, int uindex)
        {
            pcc = _pcc;
            //index = _index;
            this.uindex = uindex;
            tlkSetIndex = -1;
            LoadTlkData();
        }

        public TalkFileME1(ExportEntry export)
        {
            if (export.FileRef.Game != Mod.MEGame.ME1)
            {
                throw new Exception("ME1 Unreal TalkFile cannot be initialized with a non-ME1 file");
            }
            pcc = export.FileRef;
            uindex = export.UIndex;
            tlkSetIndex = -1;
            LoadTlkData();
        }

        public TalkFileME1(IMEPackage _pcc, int uindex, bool _male, int _langRef, int _tlkSetIndex)
        {
            pcc = _pcc;
            //index = _index;
            this.uindex = uindex;
            LangRef = _langRef;
            male = _male;
            tlkSetIndex = _tlkSetIndex;
            LoadTlkData();
        }
        #endregion

        //ITalkFile
        public string findDataById(int strRefID, bool withFileName = false)
        {
            string data = "No Data";
            for (int i = 0; i < StringRefs.Length; i++)
            {
                if (StringRefs[i].StringID == strRefID)
                {
                    data = "\"" + StringRefs[i].Data + "\"";
                    if (withFileName)
                    {
                        data += " (" + Path.GetFileName(pcc.FilePath) + " -> " + BioTlkSetName + Name + ")";
                    }
                    break;
                }
            }
            return data;
        }

        #region IEquatable
        public bool Equals(TalkFileME1 other)
        {
            return (other?.uindex == uindex && other.pcc.FilePath == pcc.FilePath);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            return Equals(obj as TalkFileME1);
        }

        public override int GetHashCode()
        {
            return 1;
        }
        #endregion

        #region Load Data
        public void LoadTlkData(BinaryReader r = null)
        {
            if (r == null)
            {
                r = new BinaryReader(new MemoryStream(pcc.getUExport(uindex).getBinaryData()), Encoding.Unicode);
            }
            //hashtable
            int entryCount = r.ReadInt32();
            StringRefs = new TLKStringRef[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                StringRefs[i] = new TLKStringRef(r, true);
            }

            //Huffman tree
            nodes = new List<HuffmanNode>();
            int nodeCount = r.ReadInt32();
            for (int i = 0; i < nodeCount; i++)
            {
                bool leaf = r.ReadBoolean();
                if (leaf)
                {
                    nodes.Add(new HuffmanNode(r.ReadChar()));
                }
                else
                {
                    nodes.Add(new HuffmanNode(r.ReadInt16(), r.ReadInt16()));
                }
            }
            //TraverseHuffmanTree(nodes[0], new List<bool>());

            //encoded data
            int stringCount = r.ReadInt32();
            byte[] data = new byte[r.BaseStream.Length - r.BaseStream.Position];
            r.Read(data, 0, data.Length);
            Bits = new BitArray(data);

            //decompress encoded data with huffman tree
            int offset = 4;
            var rawStrings = new List<string>(stringCount);
            while (offset * 8 < Bits.Length)
            {
                int size = BitConverter.ToInt32(data, offset);
                offset += 4;
                string s = GetString(offset * 8);
                offset += size + 4;
                rawStrings.Add(s);
            }

            //associate StringIDs with strings
            for (int i = 0; i < StringRefs.Length; i++)
            {
                if (StringRefs[i].Flags == 1)
                {
                    StringRefs[i].Data = rawStrings[StringRefs[i].Index];
                }
            }
        }

        private string GetString(int bitOffset)
        {
            HuffmanNode root = nodes[0];
            HuffmanNode curNode = root;

            string curString = "";
            int i;
            for (i = bitOffset; i < Bits.Length; i++)
            {
                /* reading bits' sequence and decoding it to Strings while traversing Huffman Tree */
                int nextNodeID = Bits[i] ? curNode.RightNodeID : curNode.LeftNodeID;

                /* it's an internal node - keep looking for a leaf */
                if (nextNodeID >= 0)
                    curNode = nodes[nextNodeID];
                else
                /* it's a leaf! */
                {
                    char c = curNode.data;
                    if (c != '\0')
                    {
                        /* it's not NULL */
                        curString += c;
                        curNode = root;
                        i--;
                    }
                    else
                    {
                        /* it's a NULL terminating processed string, we're done */
                        //skip ahead approximately 9 bytes to the next string
                        return curString;
                    }
                }
            }

            if (curNode.LeftNodeID == curNode.RightNodeID)
            {
                char c = curNode.data;
                //We hit edge case where final bit is on a byte boundary and there is nothing left to read. This is a leaf node.
                if (c != '\0')
                {
                    /* it's not NULL */
                    curString += c;
                    curNode = root;
                }
                else
                {
                    /* it's a NULL terminating processed string, we're done */
                    //skip ahead approximately 9 bytes to the next string
                    return curString;
                }
            }

            Debug.WriteLine("RETURNING NULL STRING (NOT NULL TERMINATED)!");
            return null;
        }

        private void TraverseHuffmanTree(HuffmanNode node, List<bool> code)
        {
            /* check if both sons are null */
            if (node.LeftNodeID == node.RightNodeID)
            {
                BitArray ba = new BitArray(code.ToArray());
                string c = "";
                foreach (bool b in ba)
                {
                    c += b ? '1' : '0';
                }
            }
            else
            {
                /* adds 0 to the code - process left son*/
                code.Add(false);
                TraverseHuffmanTree(nodes[node.LeftNodeID], code);
                code.RemoveAt(code.Count - 1);

                /* adds 1 to the code - process right son*/
                code.Add(true);
                TraverseHuffmanTree(nodes[node.RightNodeID], code);
                code.RemoveAt(code.Count - 1);
            }
        }
        #endregion

        public void saveToFile(string fileName)
        {
            XmlTextWriter xr = new XmlTextWriter(fileName, Encoding.UTF8)
            {
                Formatting = Formatting.Indented,
                Indentation = 4
            };

            xr.WriteStartDocument();
            xr.WriteStartElement("tlkFile");
            xr.WriteAttributeString("Name", Name);

            for (int i = 0; i < StringRefs.Length; i++)
            {
                xr.WriteStartElement("string");

                xr.WriteStartElement("id");
                xr.WriteValue(StringRefs[i].StringID);
                xr.WriteEndElement(); // </id>
                xr.WriteStartElement("flags");
                xr.WriteValue(StringRefs[i].Flags);
                xr.WriteEndElement(); // </flags>

                //if (i == StringRefs.Length - 1)
                //{
                //    Debugger.Break();
                //}
                TLKStringRef tref = StringRefs[i];
                if (StringRefs[i].Flags != 1)
                    xr.WriteElementString("data", "-1");
                else
                    xr.WriteElementString("data", StringRefs[i].Data);

                xr.WriteEndElement(); // </string>
            }

            xr.WriteEndElement(); // </tlkFile>
            xr.Flush();
            xr.Close();
        }
        public string TLKtoXmlstring()
        {
            StringBuilder InputTLK = new StringBuilder();
            using (StringWriter stringWriter = new StringWriter(InputTLK))
            {
                using (XmlTextWriter writer = new XmlTextWriter(stringWriter))
                {
                    writer.Formatting = Formatting.Indented;
                    writer.Indentation = 4;

                    writer.WriteStartDocument();
                    writer.WriteStartElement("tlkFile");
                    writer.WriteAttributeString("Name", Name);

                    for (int i = 0; i < StringRefs.Length; i++)
                    {
                        writer.WriteStartElement("string");
                        writer.WriteStartElement("id");
                        writer.WriteValue(StringRefs[i].StringID);
                        writer.WriteEndElement(); // </id>
                        writer.WriteStartElement("flags");
                        writer.WriteValue(StringRefs[i].Flags);
                        writer.WriteEndElement(); // </flags>
                        if (StringRefs[i].Flags != 1)
                            writer.WriteElementString("data", "-1");
                        else
                            writer.WriteElementString("data", StringRefs[i].Data);
                        writer.WriteEndElement(); // </string>
                    }
                    writer.WriteEndElement(); // </tlkFile>
                }
            }
            return InputTLK.ToString();
        }


    }
}
