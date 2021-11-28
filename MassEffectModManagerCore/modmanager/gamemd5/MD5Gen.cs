using System.IO;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Services;

namespace ME3TweaksModManager.modmanager.gamemd5
{
    class MD5Gen
    {

        public static void UpdateMD5Map(MEGame game, string directory, string outName)
        {
            var outF = $@"C:\Users\mgame\source\repos\ME3Tweaks\MassEffectModManager\ME3TweaksModManager\modmanager\gamemd5\{outName}";
            var db = VanillaDatabaseService.LoadDatabaseFor(game, false);

            if (game.IsLEGame())
            {
                db.RemoveAll(x => x.Key.Contains(@"BioGame\Config")); // Do not include config files
            }

            MemoryStream mapStream = new MemoryStream();

            // Name Table
            mapStream.WriteInt32(db.Count); // Num Entries
            foreach (var f in db.Keys)
            {
                mapStream.WriteStringASCIINull(f);
            }

            // Data Table
            mapStream.WriteInt32(db.Count);
            int idx = 0;
            foreach (var f in db)
            {
                mapStream.WriteInt32(idx); // Name Table IDX. Update this code for duplicates support
                mapStream.WriteInt32(f.Value[0].size); // Size
                var md5 = f.Value[0].md5;
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

            MemoryStream ms = new MemoryStream();
            ms.WriteStringASCII(@"MD5T");
            ms.WriteInt32((int)mapStream.Length);
            ms.Write(compBytes);
            ms.WriteToFile(outF);
        }


        public static void GenerateMD5Map(MEGame game, string directory, string outName)
        {
            var allFiles = Directory.GetFiles(directory, @"*.*", SearchOption.AllDirectories);//.Take(10).ToArray();
            if (game.IsLEGame())
            {
                allFiles.RemoveAll(x => x.Contains(@"BioGame\Config")); // Do not include config files
            }
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
                var md5 = M3Utilities.CalculateMD5(f);
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
            outStream.WriteToFile($@"C:\Users\mgame\source\repos\ME3Tweaks\MassEffectModManager\ME3TweaksModManager\modmanager\gamemd5\{outName}");

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
