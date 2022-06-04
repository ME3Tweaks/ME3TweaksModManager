using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using Microsoft.AppCenter.Ingestion.Models;

namespace ME3TweaksModManager.modmanager.objects.tlk
{
    readonly record struct TLKMergeCompressedInfo(long dataStartOffset, long compressedSize, uint decompressedSize);

    /// <summary>
    /// Container for LZMA compressed string data, used for TLK merge feature in ME1/LE1
    /// </summary>
    internal class CompressedTLKMergeData
    {
        private const string COMPRESSED_MAGIC = @"CTMD";

        private const byte COMPRESSED_VERSION = 0x1;
        // Header information
        private Dictionary<string, TLKMergeCompressedInfo> CompressedInfo = new();

        /// <summary>
        /// Gets the list of filenames of the xmls
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<string> GetAllTlkMergeEntries()
        {
            return CompressedInfo.Keys.ToList();
        }

        public string DecompressTLKMergeFile(string fileName, Stream compressedStream)
        {
            // This is probably terrible in terms of allocations
            var info = CompressedInfo[fileName];
            compressedStream.Seek(info.dataStartOffset, SeekOrigin.Begin);
            byte[] compressed = compressedStream.ReadToBuffer(info.compressedSize);
            var result = LZMA.Decompress(compressed, info.decompressedSize);
            return new StreamReader(new MemoryStream(result)).ReadToEnd();
        }

        /// <summary>
        /// Compresses an input directory of xml files to a compressed file version
        /// </summary>
        /// <param name="inputDirectory"></param>
        /// <returns></returns>
        public static MemoryStream CreateCompressedTlkMergeFile(string inputDirectory)
        {
            MemoryStream ms = new MemoryStream();

            // Contains position for start of offset data (long long)
            Dictionary<string, long> headerOffsetMap = new Dictionary<string, long>();

            var files = Directory.GetFiles(inputDirectory, @"*.xml", SearchOption.TopDirectoryOnly);

            // Write out the header.
            ms.WriteStringASCII(COMPRESSED_MAGIC); // Magic; Compressed Tlk Merge Data
            ms.WriteByte(COMPRESSED_VERSION); // For changing parser in the future.
            ms.WriteInt32(files.Length); // Number of entries to follow

            // Write out the file table
            foreach (var f in files)
            {
                ms.WriteStringUnicode(Path.GetFileName(f));
                headerOffsetMap[f] = ms.Position;
                ms.WriteUInt64(0); // Data offset 64bit
                ms.WriteInt32(0); // Data size 32bit
            }

            // Compress the data and update the header as we go
            foreach (var f in files)
            {
                var dataPos = ms.Position;
                var data = File.ReadAllBytes(f);

                ms.Write(LZMA.Compress(data));
                ms.Seek(headerOffsetMap[f], SeekOrigin.Begin); // Seek to where we need to write info
                headerOffsetMap[f] = ms.Position;
                ms.WriteInt64(dataPos); // Data offset
                ms.WriteInt32(data.Length); // Data size

                ms.SeekEnd(); // Go to end for next file
            }

            return ms;
        }
    }
}
