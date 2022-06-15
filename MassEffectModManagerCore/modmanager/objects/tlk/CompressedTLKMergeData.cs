using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.Design;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.objects.mod;
using Microsoft.AppCenter.Ingestion.Models;

namespace ME3TweaksModManager.modmanager.objects.tlk
{
    public readonly record struct TLKMergeCompressedInfo(int dataStartOffset, int decompressedSize, int compressedSize);

    /// <summary>
    /// Container for LZMA compressed string data, used for TLK merge feature in ME1/LE1
    /// </summary>
    public class CompressedTLKMergeData
    {
        private const string COMPRESSED_MAGIC = @"CTMD";

        private const byte COMPRESSED_VERSION = 0x1;

        // Compressed data is LZMA
        // Data is stored in this format:
        // 4 bytes DECOMPRESSED SIZE
        // 4 bytes COMPRESSED SIZE
        // [COMPRESSED SIZE OF LZMA DATA] Either immediately following or at a specified offset

        // File format:
        // 4BYTES "CTMD" Magic
        // 1BYTE VERSION for parsing

        // COMPRESSED HEADER
        // 

        // LIST OF COMPRESSED FILES


        // Header information
        /// <summary>
        /// Version that was used to serialize the archive
        /// </summary>
        private int Version;

        /// <summary>
        /// The header information for the archive
        /// </summary>
        private Dictionary<string, TLKMergeCompressedInfo> CompressedInfo = new();

        /// <summary>
        /// The compressed data block, read from the compressed file
        /// </summary>
        public byte[] LoadedCompressedData { get; private set; }

        /// <summary>
        /// Gets the list of filenames of the xmls
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<string> GetFileListing()
        {
            return CompressedInfo.Keys.ToList();
        }

        /// <summary>
        /// Fetches a file out of the archive and writes it to the specified stream
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="compressedStream"></param>
        /// <returns></returns>
        public void DecompressFileToStream(string fileName, Stream compressedStream, Stream outStream)
        {
            // This is probably terrible in terms of allocations
            var info = CompressedInfo[fileName];
            compressedStream.Seek(info.dataStartOffset, SeekOrigin.Begin);
            byte[] compressed = compressedStream.ReadToBuffer(info.compressedSize);
            var result = DecompressFile(info, compressed.AsSpan());
            outStream.Write(result, 0, result.Length);
        }

        /// <summary>
        /// Fetches a file out of the archive and writes it to the specified stream
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="compressedStream"></param>
        /// <returns></returns>
        public void DecompressFileToStream(string fileName, byte[] compressedDataBlock, Stream outStream)
        {
            // This is probably terrible in terms of allocations
            var info = CompressedInfo[fileName];
            var compressed = compressedDataBlock.AsSpan(info.dataStartOffset, info.compressedSize);
            var result = DecompressFile(info, compressed);
            outStream.Write(result, 0, result.Length);
        }

        /// <summary>
        /// Decompresses data from the specified span encoded in LZMA format.
        /// </summary>
        /// <returns></returns>
        public byte[] DecompressFile(TLKMergeCompressedInfo info, Span<byte> compressedData)
        {
            var outData = new byte[info.decompressedSize];
            LZMA.Decompress(compressedData, outData);
            return outData;
        }

        #region TEXT FILES
        /// <summary>
        /// Fetches a text file out of the archive
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="compressedStream"></param>
        /// <returns></returns>
        public string DecompressTextFile(string fileName, byte[] compressedData)
        {
            // This is probably terrible in terms of allocations
            var info = CompressedInfo[fileName];
            var compressed = compressedData.AsSpan(info.dataStartOffset, info.compressedSize);
            return DecompressTextFile(info, compressed);
        }

        /// <summary>
        /// Decompresses text data from the specified span encoded in LZMA format.
        /// </summary>
        /// <returns></returns>
        public string DecompressTextFile(TLKMergeCompressedInfo info, Span<byte> compressedData)
        {
            var result = DecompressFile(info, compressedData);
            return new StreamReader(new MemoryStream(result)).ReadToEnd();
        }
        #endregion



        /// <summary>
        /// Reads a compressed tlk merge data stream. Does NOT decompress the data - this only reads the header info
        /// </summary>
        /// <param name="streamData"></param>
        /// <returns></returns>
        public static CompressedTLKMergeData ReadCompressedTlkMergeFile(Stream streamData, bool loadDataBlock)
        {
            CompressedTLKMergeData c = new CompressedTLKMergeData();

            if (streamData.ReadStringASCII(4) != COMPRESSED_MAGIC)
            {
                throw new Exception(@"CompressedTLKMergeFile has invalid magic number!");
            }

            c.Version = streamData.ReadByte();

            // The header is compressed - we must decompress the header to read it
            var decompSize = streamData.ReadUInt32();
            var compSize = streamData.ReadUInt32();
            var header = new MemoryStream(LZMA.Decompress(streamData.ReadToBuffer(compSize), decompSize));

            var fileCount = header.ReadInt32();
            for (int i = 0; i < fileCount; i++)
            {
                var name = header.ReadStringUnicodeNull();
                var info = new TLKMergeCompressedInfo(header.ReadInt32(), header.ReadInt32(), header.ReadInt32());
                c.CompressedInfo[name] = info;
            }

            var dataBlockSize = streamData.ReadInt32();
            if (loadDataBlock)
            {
                c.LoadedCompressedData = new byte[dataBlockSize];
                streamData.Read(c.LoadedCompressedData, 0, dataBlockSize);
            }

            return c;
        }


        /// <summary>
        /// Compresses an input directory of xml files to a compressed file version
        /// </summary>
        /// <param name="inputDirectory"></param>
        /// <returns></returns>
        public static MemoryStream CreateCompressedTlkMergeFile(string inputDirectory, Action<uint, uint> compressingCallback = null)
        {
            MemoryStream ms = new MemoryStream();

            // Use existing file it exists
            var existingFile = Path.Combine(inputDirectory, Mod.Game1EmbeddedTlkCompressedFilename);
            if (File.Exists(existingFile))
            {
                using var f = File.OpenRead(existingFile);
                f.CopyTo(ms);
                return ms;
            }

            // Contains position for start of offset data (long long)
            Dictionary<string, long> headerOffsetMap = new Dictionary<string, long>();

            var files = Directory.GetFiles(inputDirectory, @"*.xml", SearchOption.TopDirectoryOnly);

            // Write out the header.
            ms.WriteStringASCII(COMPRESSED_MAGIC); // Magic; Compressed Tlk Merge Data
            ms.WriteByte(COMPRESSED_VERSION); // For changing parser in the future.


            // Compressed header since there might be a LOT of string data (3000+)

            MemoryStream header = new MemoryStream();
            header.WriteInt32(files.Length); // Number of entries to follow
            // Write out the file table
            foreach (var f in files)
            {
                header.WriteStringUnicodeNull(Path.GetFileName(f));
                headerOffsetMap[f] = header.Position;
                header.WriteUInt32(0); // Data offset
                header.WriteInt32(0); // Decompressed Data Size
                header.WriteInt32(0); // Compressed Data Size
            }


            // Compress the data into file blocks
            var compressedDataMap = new ConcurrentDictionary<string, byte[]>();
            uint done = 0;
            uint total = (uint)files.Length;
            Parallel.ForEach(files, f =>
            {
                compressingCallback?.Invoke(done, total);
                var inTextData = File.ReadAllBytes(f);
                compressedDataMap[f] = LZMA.Compress(inTextData);
                Interlocked.Increment(ref done);
            });

            // Build the compressed data block
            MemoryStream compressedData = new MemoryStream();
            foreach (var f in files)
            {
                var dataPos = compressedData.Position;
                var inTextData = compressedDataMap[f];

                compressedData.Write(inTextData);
                header.Seek(headerOffsetMap[f], SeekOrigin.Begin); // Seek to where we need to write info
                headerOffsetMap[f] = compressedData.Position;
                header.WriteUInt32((uint)dataPos); // Data offset in the compressed data block
                header.WriteInt32((int)new FileInfo(f).Length); // Decompressed size
                header.WriteInt32(inTextData.Length); // Compressed size
            }

            // Write the compressed header data
            var headerCompressed = LZMA.Compress(header.GetBuffer());
            ms.WriteInt32((int)header.Length); // Decompressed size
            ms.WriteInt32(headerCompressed.Length); // Compressed size
            ms.Write(headerCompressed); // Header data

            // Append the compressed data.
            ms.WriteInt32((int)compressedData.Length); // This is in event we change format later - we can append more data
            compressedData.Position = 0;
            compressedData.CopyTo(ms);

            return ms;
        }

        public TLKMergeCompressedInfo GetFileInfo(string file)
        {
            return CompressedInfo[file];
        }


        /// <summary>
        /// Decompresses the archive to disk at the specified location
        /// </summary>
        /// <param name="outputDirectory"></param>
        /// <returns>true if successful, false if any errors occurred</returns>
        public bool DecompressArchiveToDisk(string outputDirectory, byte[] compressedDataBlock, Action<int, int> progressCallback = null)
        {
            try
            {
                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);
                int done = 0;
                int total = CompressedInfo.Count;
                foreach (var fileInfo in CompressedInfo)
                {
                    var outF = Path.Combine(outputDirectory, fileInfo.Key);
                    using var outS = File.Open(outF, FileMode.Create, FileAccess.Write);
                    M3Log.Information($@"M3ZA: Decompressing {fileInfo.Key} to {outF}");
                    DecompressFileToStream(fileInfo.Key, compressedDataBlock, outS);
                    done++;
                    progressCallback?.Invoke(done, total);
                }

                return true;
            }
            catch (IOException e)
            {
                M3Log.Exception(e, $@"Error decompressing archive to disk at {outputDirectory}:");
            }

            return false;
        }
    }
}
