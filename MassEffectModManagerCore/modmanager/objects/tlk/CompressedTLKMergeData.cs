using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.ME3Tweaks.ModManager;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.mod;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ME3TweaksModManager.modmanager.objects.tlk
{
    // V2: Add optionKeyId
    public readonly record struct TLKMergeCompressedInfo(int dataStartOffset, int decompressedSize, int compressedSize, int optionKeyId);

    /// <summary>
    /// Container for LZMA compressed string data, used for TLK merge feature in ME1/LE1
    /// </summary>
    public class CompressedTLKMergeData
    {
        private const string COMPRESSED_MAGIC = @"CTMD";

        private const byte HIGHEST_SUPPORTED_COMPRESSED_VERSION = 0x2;
        private const byte OPTIONKEY_NONE = 0xFF;

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
        /// List of option keys (used during serialization and to determine key name)
        /// </summary>
        private List<string> OptionKeys = new(0);

        /// <summary>
        /// The compressed data block, read from the compressed file
        /// </summary>
        public byte[] LoadedCompressedData { get; private set; }

        /// <summary>
        /// Gets the list of filenames of the xmls
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<string> GetFileListing(List<string> enabledOptionsKeys = null)
        {
            if (enabledOptionsKeys == null)
                return CompressedInfo.Keys.ToList();

            // If option keys are specified we will filter items out that are not marked as enabled
            List<string> files = new List<string>();
            foreach (var item in CompressedInfo)
            {
                if (item.Value.optionKeyId == OPTIONKEY_NONE)
                {
                    files.Add(item.Key);
                    continue;
                }

                if (enabledOptionsKeys.Contains(OptionKeys[item.Value.optionKeyId]))
                {
                    files.Add(item.Key);
                    continue;
                }
            }

            return files;
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
                throw new Exception(M3L.GetString(M3L.string_invalidMagic_m3za));
            }

            c.Version = streamData.ReadByte();

            if (c.Version > HIGHEST_SUPPORTED_COMPRESSED_VERSION)
            {
                // We do not support this version
                throw new Exception(M3L.GetString(M3L.string_interp_tooNewM3ZAFile, HIGHEST_SUPPORTED_COMPRESSED_VERSION, c.Version));
            }

            // The header is compressed - we must decompress the header to read it
            var decompSize = streamData.ReadUInt32();
            var compSize = streamData.ReadUInt32();
            var header = new MemoryStream(LZMA.Decompress(streamData.ReadToBuffer(compSize), decompSize));

            var fileCount = header.ReadInt32();
            for (int i = 0; i < fileCount; i++)
            {
                var name = header.ReadStringUnicodeNull();
                var dataStartOffset = header.ReadInt32();
                var decompressedSize = header.ReadInt32();
                var compressedSize = header.ReadInt32();
                var optionKeyId = OPTIONKEY_NONE;
                if (c.Version >= 2)
                {
                    optionKeyId = (byte)header.ReadByte();
                }

                var info = new TLKMergeCompressedInfo(dataStartOffset, decompressedSize, compressedSize, optionKeyId);
                c.CompressedInfo[name] = info;
            }

            // Read option key names
            if (c.Version >= 2)
            {
                int num = header.ReadByte();
                for (int i = 0; i < num; i++)
                {
                    c.OptionKeys.Add(header.ReadStringUnicodeNull());
                }
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
        public static MemoryStream CreateCompressedTlkMergeFile(Mod mod, string inputDirectory, Action<uint, uint> compressingCallback = null)
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

            var searchOption = mod.ModDescTargetVersion >= ModDescConsts.MODDESC_VERSION_9_0 ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            var files = Directory.GetFiles(inputDirectory, @"*.xml", searchOption);
            List<string> optionKeys = new List<string>();



            // Write out the header.
            ms.WriteStringASCII(COMPRESSED_MAGIC); // Magic; Compressed Tlk Merge Data
            var version = GetM3ZAVersionForModDesc(mod.ModDescTargetVersion);
            ms.WriteByte(version); // For changing parser in the future.

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
                if (version >= 2)
                {
                    header.WriteByte(GetOptionKeyIndex(optionKeys, inputDirectory, f));
                }
            }

            if (version >= 2)
            {
                header.WriteByte((byte)optionKeys.Count);
                for (int i = 0; i < optionKeys.Count; i++)
                {
                    header.WriteStringUnicodeNull(optionKeys[i]);
                }
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

#if DEBUG
            // Verify back.
            var pos = ms.Position;
            ms.Position = 0;
            var cmi = ReadCompressedTlkMergeFile(ms, true);
            ms.Position = pos;
#endif

            return ms;
        }

        /// <summary>
        /// Returns the option key index for the given filepath out of the list of option keys. Adds a key if needed
        /// </summary>
        /// <param name="optionKeys"></param>
        /// <param name="rootPath"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static byte GetOptionKeyIndex(List<string> optionKeys, string rootPath, string filePath)
        {
            var containingFolder = Directory.GetParent(filePath);
            if (rootPath == containingFolder.FullName)
                return OPTIONKEY_NONE;

            var idx = optionKeys.IndexOf(containingFolder.Name);
            if (idx == -1)
            {
                idx = optionKeys.Count;
                optionKeys.Add(containingFolder.Name);
            }

            return (byte)idx;
        }

        /// <summary>
        /// Returns the M3ZA version used for a various moddesc versions
        /// </summary>
        /// <param name="moddescVersion"></param>
        /// <returns></returns>
        private static byte GetM3ZAVersionForModDesc(double moddescVersion)
        {

            if (moddescVersion < 9.0)
                return 1;

            // Update this method if we change M3ZA parser!
            return HIGHEST_SUPPORTED_COMPRESSED_VERSION;
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
                    string outF = Path.Combine(outputDirectory, fileInfo.Key);
                    if (fileInfo.Value.optionKeyId != OPTIONKEY_NONE)
                    {
                        // subfolder
                        var outFolder = Path.Combine(outputDirectory, OptionKeys[fileInfo.Value.optionKeyId]);
                        Directory.CreateDirectory(outFolder);
                        outF = Path.Combine(outFolder, fileInfo.Key);
                    }

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

        /// <summary>
        /// Verifies all option keys are valid
        /// </summary>
        /// <param name="mod"></param>
        public void VerifyOptionKeys(Mod mod)
        {
            var custJob = mod.GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (custJob == null)
                return;

            foreach (var alt in custJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ENABLE_TLKMERGE_OPTIONKEY))
            {
                if (!OptionKeys.Contains(alt.LE1TLKOptionKey))
                {
                    throw new Exception(M3L.GetString(M3L.string_interp_m3zaMissingTlkOptionKey, alt.LE1TLKOptionKey, alt.FriendlyName));
                }
            }
        }
    }
}
