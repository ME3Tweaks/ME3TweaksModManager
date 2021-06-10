using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;

namespace MassEffectModManagerCore.modmanager.gamemd5
{
    class MD5Gen
    {
        public static void GenerateMD5Map(string directory, string outName)
        {
            var allFiles = Directory.GetFiles(directory, @"*.*", SearchOption.AllDirectories);

            MemoryStream mapStream = new MemoryStream();

            // Name Table
            mapStream.WriteInt32(allFiles.Length); // Num Entries
            foreach (var f in allFiles)
            {
                mapStream.WriteStringASCIINull(f.Substring(directory.Length + 1));
            }

            // Data Table
            mapStream.WriteInt32(allFiles.Length);
            int idx = 0;
            foreach (var f in allFiles)
            {
                mapStream.WriteInt32(idx); // Name Table IDX. Update this code for duplicates support
                mapStream.WriteInt32((int)new FileInfo(f).Length); // Size
                var md5 = Utilities.CalculateMD5(f);
                for (int i = 0; i < 32; i++)
                {
                    byte b = 0;
                    b |= HexToInt(md5[i]);
                    b = (byte)(b << 4);
                    i++;
                    b |= HexToInt(md5[i]);

                    mapStream.WriteByte(b);
                }
                idx++;
            }

            var compBytes = LZMA.Compress(mapStream.ToArray());

            MemoryStream outStream = new MemoryStream();
            outStream.WriteStringASCII(@"MD5T");
            outStream.WriteInt32((int)mapStream.Length);
            outStream.Write(compBytes);
            outStream.WriteToFile($@"C:\Users\mgame\source\repos\ME3Tweaks\MassEffectModManager\MassEffectModManagerCore\modmanager\gamemd5\{outName}");

        }

        static byte HexToInt(char hexChar)
        {
            hexChar = char.ToLower(hexChar);  // may not be necessary

            return (byte)((byte)hexChar < (byte)'a' ?
                ((byte)hexChar - (byte)'0') :
                10 + ((byte)hexChar - (byte)'a'));
        }
    }
}
