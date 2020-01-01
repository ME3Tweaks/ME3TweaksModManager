using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using MassEffectModManagerCore.modmanager.helpers;

namespace MassEffectModManagerCore.modmanager.gameini
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
                throw new Exception("First 4 bytes were not 0x1E (was " + unknownInt.ToString("X8") + ").This does not appear to be a Coalesced file.");
            }

            while (fs.Position < fs.Length)
            {
                long pos = fs.Position;
                string filename = fs.ReadUnrealString();
                string contents = fs.ReadUnrealString();
                Inis[filename] = DuplicatingIni.ParseIni(contents);
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
                outStream.WriteUnrealStringASCII(file.Key);
                outStream.WriteUnrealStringASCII(file.Value.ToString());
            }
            File.WriteAllBytes(outfile, outStream.ToArray());
            return true;
        }
    }
}
