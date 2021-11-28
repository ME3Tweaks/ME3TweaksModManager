using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF;
using ME3TweaksModManager.modmanager.diagnostics;

namespace ME3TweaksModManager.modmanager.gameini
{
    [Localizable(false)]
    public class ME2Coalesced
    {
        public SortedDictionary<string, DuplicatingIni> Inis = new SortedDictionary<string, DuplicatingIni>();
        public string Inputfile { get; }
        public ME2Coalesced(string file)
        {
            Inputfile = file;
            using FileStream fs = new FileStream(file, FileMode.Open);
            int unknownInt = fs.ReadInt32();
            if (unknownInt != 0x1E)
            {
                throw new Exception("First 4 bytes were not 0x1E (was " + unknownInt.ToString("X8") + "). This does not appear to be a Coalesced file.");
            }

            while (fs.Position < fs.Length)
            {
                long pos = fs.Position;
                string filename = fs.ReadUnrealString();
                string contents = fs.ReadUnrealString();
                Inis[filename] = DuplicatingIni.ParseIni(contents);
            }
        }

        public static ME2Coalesced OpenFromTarget(GameTargetWPF target, bool throwExceptionOnError = false)
        {
            var coalPath = Path.Combine(target.TargetPath, @"BioGame", @"Config", @"PC", @"Cooked", @"Coalesced.ini");
            if (!File.Exists(coalPath)) return null;
            try
            {
                return new ME2Coalesced(coalPath);
            }
            catch (Exception e)
            {
                M3Log.Error("Cannot open ME2Coalesced file from target: " + e.Message);
                if (throwExceptionOnError) throw;
                return null;
            }
        }

        /// <summary>
        /// Serializes this coalesced file to disk.
        /// </summary>
        /// <param name="outputfile">File to write to. If this is null the input filepath is used instead.</param>
        /// <returns>True if serialization succeeded (always).</returns>
        public bool Serialize(string outputfile = null)
        {
            string outfile = outputfile ?? Inputfile;
            MemoryStream outStream = new MemoryStream();
            outStream.WriteInt32(0x1E); //Unknown header but seems to just be 1E. Can't find any documentation on what this is.
            foreach (var file in Inis)
            {
                //Console.WriteLine("Coalescing " + Path.GetFileName(file));
                outStream.WriteStringLatin1(file.Key);
                outStream.WriteStringLatin1(file.Value.ToString());
            }
            File.WriteAllBytes(outfile, outStream.ToArray());
            return true;
        }
    }
}
