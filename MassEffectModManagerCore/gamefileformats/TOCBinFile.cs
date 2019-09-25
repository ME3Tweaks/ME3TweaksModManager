
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MassEffectModManagerCore.gamefileformats
{
    public class TOCBinFile
    {
        MemoryStream Memory;
        [DebuggerDisplay("TOCBinEntry | {name}, {size} bytes")]
        public struct Entry
        {
            public int offset;
            public string name;
            public int size;
        }

        public List<Entry> Entries;

        public TOCBinFile(MemoryStream m)
        {
            Memory = m;
            ReadFile();
        }

        public TOCBinFile(string path)
        {
            Memory = new MemoryStream(File.ReadAllBytes(path).ToArray());
            ReadFile();
        }

        public void ReadFile()
        {
            Memory.Seek(0, 0);
            uint magic = (uint)ReadInt(Memory);
            if (magic != 0x3AB70C13)
            {
                //DebugOutput.PrintLn("Not a SFAR File.  DEBUG: in tocbin");
                return;
            }
            Memory.Seek(8, 0);
            int count = ReadInt(Memory);
            Memory.Seek(0xC + 8 * count, 0);
            Entries = new List<Entry>();
            int blocksize = 0;
            int pos = (int)Memory.Position;
            do
            {
                Entry e = new Entry();
                e.offset = pos;
                Memory.Seek(pos, 0);
                blocksize = ReadInt16(Memory);
                Memory.Seek(pos + 0x4, 0);
                e.size = ReadInt(Memory);
                Memory.Seek(pos + 0x1C, 0);
                e.name = ReadString(Memory);
                pos += blocksize;
                Entries.Add(e);
            }
            while (blocksize != 0);
        }

        private int ReadInt(MemoryStream m)
        {
            byte[] buff = new byte[4];
            m.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }

        private ushort ReadInt16(MemoryStream m)
        {
            byte[] buff = new byte[2];
            m.Read(buff, 0, 2);
            return BitConverter.ToUInt16(buff, 0);
        }

        public string ReadString(MemoryStream m)
        {
            string s = "";
            byte b;
            while ((b = (byte)m.ReadByte()) != 0)
                s += (char)b;
            return s;
        }

        public void UpdateEntry(int Index, int size)
        {
            if (Entries == null || Index < 0 || Index >= Entries.Count)
                return;
            Entry e = Entries[Index];
            e.size = size;
            Entries[Index] = e;
        }

        public MemoryStream Save()
        {

            foreach (Entry e in Entries)
            {
                Memory.Seek(e.offset + 4, 0);
                Memory.Write(BitConverter.GetBytes(e.size), 0, 4);
            }
            return Memory;
        }
    }
}