using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using MassEffectModManagerCore.modmanager.objects.mod.merge.v1;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge
{
    public class MergeModLoader
    {
        private const string MERGEMOD_MAGIC = "M3MM";
        public static IMergeMod LoadMergeMod(Stream mergeFileStream, string filename, bool loadAssets)
        {
            if (mergeFileStream.ReadStringASCII(4) != MERGEMOD_MAGIC)
            {
                throw new Exception("Merge mod file does not have correct magic header");
            }

            var version = mergeFileStream.ReadByte();
            switch (version)
            {
                case 1:
                    return MergeMod1.ReadMergeMod(mergeFileStream, filename, loadAssets);
                default:
                    return null;
            }
        }

#if DEBUG
        public static string SerializeTest(int version)
        {
            var outfile = @"X:\m3modlibrary\LE2\No Mini Games\MergeMods\nominigames.m3m";
            using FileStream fs = File.Open(outfile, FileMode.Create, FileAccess.ReadWrite);
            fs.WriteStringLatin1(MERGEMOD_MAGIC);
            fs.WriteByte((byte) version);
            switch (version)
            {
                case 1:
                    MergeMod1.SerializeTest(fs, @"C:\Users\mgame\Desktop\MERGE\NoMiniGames\nominigames.json");
                    break;
            }

            return outfile;
        }
#endif
    }
}
