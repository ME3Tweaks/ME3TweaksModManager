using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MassEffectModManagerCore;
using MassEffectModManagerCore.gamefileformats;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.helpers;
using ME3Explorer.Unreal;
using static ME3Explorer.Unreal.UnrealFlags;

namespace ME3Explorer.Packages
{
    /// <summary>
    /// Storage type flags for Texture2D
    /// </summary>
    [Flags]
    public enum StorageFlags
    {
        noFlags = 0,
        externalFile = 1 << 0,
        compressedZlib = 1 << 1,
        compressedLZO = 1 << 4,
        unused = 1 << 5,
    }

    [DebuggerDisplay("MEPackage - {FilePath}, {FileSourceForDebugging}")]
    public sealed class MEPackage : UnrealPackageFile, IMEPackage
    {
        public Mod.MEGame Game { get; private set; } //can only be ME1, ME2, or ME3. UDK is a seperate class
        public List<string> AdditionalPackagesToCook = new List<string>();
        public bool CanReconstruct =>
            Game == Mod.MEGame.ME3 ||
            Game == Mod.MEGame.ME2 ||
            Game == Mod.MEGame.ME1; //mod manager has no ui to modify things and we won't be modifying ME1 files except for blank TLK files.

        public int FullHeaderSize { get; private set; }
        public EPackageFlags Flags { get; private set; }
        public int PackageTypeId { get; private set; }

        public uint OriginalDependencyTableSize { get; private set; }
        public override int NameCount { get; protected set; }
        public int NameOffset { get; private set; }
        public override int ExportCount { get; protected set; }
        public int ExportOffset { get; private set; }
        public override int ImportCount { get; protected set; }
        public int ImportOffset { get; private set; }
        public int DependencyTableOffset { get; private set; }
        public Guid PackageGuid { get; set; }

        public byte[] getHeader()
        {
            var ms = new MemoryStream();
            WriteHeader(ms);
            return ms.ToArray();
        }

        public bool IsCompressed
        {
            get => Flags.HasFlag(EPackageFlags.Compressed);
        }

        public enum CompressionType
        {
            None = 0,
            Zlib,
            LZO
        }

        #region HeaderMisc
        private int Gen0ExportCount;
        private int Gen0NameCount;
        private int Gen0NetworkedObjectCount;
        private int ImportExportGuidsOffset;
        //private int ImportGuidsCount;
        //private int ExportGuidsCount;
        //private int ThumbnailTableOffset;
        private uint packageSource;
        private int unknown4;
        private int unknown6;
        #endregion

        private static bool isInitialized;
        public static Func<string, Mod.MEGame, MEPackage> Initialize()
        {
            if (isInitialized)
            {
                throw new Exception(nameof(MEPackage) + " can only be initialized once");
            }

            isInitialized = true;
            return (f, g) => new MEPackage(f, g);
        }

        private static bool isInitializedQuick;
        public static Func<string, Mod.MEGame, MEPackage> InitializeQuick()
        {
            if (isInitializedQuick)
            {
                throw new Exception(nameof(MEPackage) + " can only be initialized once");
            }

            isInitializedQuick = true;
            return (f, g) => new MEPackage(f, g, onlyHeader: true);
        }

        private static bool isInitializedStream;
        public static Func<Stream, Mod.MEGame, MEPackage> InitializeStream()
        {
            if (isInitializedStream)
            {
                throw new Exception(nameof(MEPackage) + " (stream mode) can only be initialized once");
            }

            isInitializedStream = true;
            return (f, g) => new MEPackage(f, g);
        }

        private MEPackage(Stream stream, Mod.MEGame forceGame = Mod.MEGame.Unknown)
        {
            if (forceGame != Mod.MEGame.Unknown)
            {
                //new Package
                Game = forceGame;
                //reasonable defaults?
                Flags = EPackageFlags.Cooked | EPackageFlags.AllowDownload | EPackageFlags.DisallowLazyLoading | EPackageFlags.RequireImportsAlreadyLoaded;
                return;
            }

            ReadPackageFromStream(stream, false);
        }

        /// <summary>
        /// Gets a decompressed stream of a package. Mixin rules makes it follow the following rules if the package is compressed and needs to be decompressed:
        /// 1. Additional packages to cook is not written to the stream.
        /// 2. Dependency table is included.
        /// If the package is not compressed, the additional packages header is written.
        /// ME3CMM decompression code was based on ME3Exp 2.0 which would do this when decompressing files. If a file was already decompressed, it would not modify it, so it did not affect SFAR files.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="mixinRules"></param>
        /// <returns></returns>
        public static MemoryStream GetDecompressedPackageStream(MemoryStream stream, bool mixinRules = false)
        {
            var package = MEPackageHandler.OpenMEPackage(stream);
            return package.saveToStream(false, !mixinRules);
        }

        /// <summary>
        /// Reads a package file from a stream.
        /// </summary>
        /// <param name="stream"></param>
        private void ReadPackageFromStream(Stream stream, bool onlyHeader)
        {
            #region Header
            uint magic = stream.ReadUInt32();
            if (magic != packageTag)
            {
                throw new FormatException("Not an Unreal package!");
            }
            ushort unrealVersion = stream.ReadUInt16();
            ushort licenseeVersion = stream.ReadUInt16();
            switch (unrealVersion)
            {
                case 491 when licenseeVersion == 1008:
                    Game = Mod.MEGame.ME1;
                    break;
                case 512 when licenseeVersion == 130:
                    Game = Mod.MEGame.ME2;
                    break;
                case 684 when licenseeVersion == 194:
                    Game = Mod.MEGame.ME3;
                    break;
                default:
                    throw new FormatException("Not a Mass Effect Package!");
            }
            FullHeaderSize = stream.ReadInt32();
            int foldernameStrLen = stream.ReadInt32();
            //always "None", so don't bother saving result
            if (foldernameStrLen > 0)
                stream.ReadStringASCIINull(foldernameStrLen);
            else
                stream.ReadStringUnicodeNull(foldernameStrLen * -2);

            Flags = (EPackageFlags)stream.ReadUInt32();

            if (Game == Mod.MEGame.ME3 && Flags.HasFlag(EPackageFlags.Cooked))
            {
                //This doesn't seem to be true!
                PackageTypeId = stream.ReadInt32(); //0 = standard, 1 = patch ? Not entirely sure patch_001 files with byte = 0 game does not load
            }

            NameCount = stream.ReadInt32();
            NameOffset = stream.ReadInt32();
            ExportCount = stream.ReadInt32();
            ExportOffset = stream.ReadInt32();
            ImportCount = stream.ReadInt32();
            ImportOffset = stream.ReadInt32();
            DependencyTableOffset = stream.ReadInt32();

            if (Game == Mod.MEGame.ME3)
            {
                ImportExportGuidsOffset = stream.ReadInt32();
                stream.SkipInt32(); //ImportGuidsCount always 0
                stream.SkipInt32(); //ExportGuidsCount always 0
                stream.SkipInt32(); //ThumbnailTableOffset always 0
            }

            PackageGuid = stream.ReadGuid();
            uint generationsTableCount = stream.ReadUInt32();
            if (generationsTableCount > 0)
            {
                generationsTableCount--;
                Gen0ExportCount = stream.ReadInt32();
                Gen0NameCount = stream.ReadInt32();
                Gen0NetworkedObjectCount = stream.ReadInt32();
            }
            //should never be more than 1 generation, but just in case
            stream.Skip(generationsTableCount * 12);

            stream.SkipInt32();//engineVersion          Like unrealVersion and licenseeVersion, these 2 are determined by what game this is,
            stream.SkipInt32();//cookedContentVersion   so we don't have to read them in

            if (Game == Mod.MEGame.ME2 || Game == Mod.MEGame.ME1)
            {
                stream.SkipInt32(); //always 0
                stream.SkipInt32(); //always 47699
                unknown4 = stream.ReadInt32();
                stream.SkipInt32(); //always 1 in ME1, always 1966080 in ME2
            }

            unknown6 = stream.ReadInt32();
            stream.SkipInt32(); //always -1 in ME1 and ME2, always 145358848 in ME3

            if (Game == Mod.MEGame.ME1)
            {
                stream.SkipInt32(); //always -1
            }

            //skip compression type chunks. Decompressor will handle that
            long compressionTypeOffset = stream.Position;

            stream.SkipInt32();
            //Todo: Move decompression of package code here maybe
            //Todo: Refactor decompression code to single unified style
            int numChunks = stream.ReadInt32();
            stream.Skip(numChunks * 16);

            packageSource = stream.ReadUInt32();

            if (Game == Mod.MEGame.ME2 || Game == Mod.MEGame.ME1)
            {
                stream.SkipInt32(); //always 0
            }

            //Doesn't need to be written out, so it doesn't need to be read in
            //keep this here in case one day we learn that this has a purpose
            //Narrator: On Jan 26, 2020 it turns out this was actually necessary to make it work
            // with ME3Tweaks Mixins as old code did not remove this section
            if (Game == Mod.MEGame.ME2 || Game == Mod.MEGame.ME3)
            {
                int additionalPackagesToCookCount = stream.ReadInt32();
                //var additionalPackagesToCook = new string[additionalPackagesToCookCount];
                for (int i = 0; i < additionalPackagesToCookCount; i++)
                {
                    var packageStr = stream.ReadUnrealString();
                    AdditionalPackagesToCook.Add(packageStr);
                }
            }
            #endregion

            if (onlyHeader) return; // That's all we need to parse. 

            Stream inStream = stream;
            if (IsCompressed && numChunks > 0)
            {
                inStream = CompressionHelper.DecompressUDK(stream, compressionTypeOffset);
                //inStream = Game == Mod.MEGame.ME3 ? CompressionHelper.DecompressME3(stream) : CompressionHelper.DecompressME1orME2(stream);
            }

            //read namelist
            inStream.JumpTo(NameOffset);
            for (int i = 0; i < NameCount; i++)
            {
                //Debug.Write($"Reading name at 0x{inStream.Position:X6}: ");
                var name = inStream.ReadUnrealString();
                //Debug.WriteLine(name);
                names.Add(name);
                if (Game == Mod.MEGame.ME1)
                    inStream.Skip(8);
                else if (Game == Mod.MEGame.ME2)
                    inStream.Skip(4);
            }

            //read importTable
            inStream.Seek(ImportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ImportCount; i++)
            {
                ImportEntry imp = new ImportEntry(this, inStream) { Index = i };
                imports.Add(imp);
            }

            //read exportTable (ExportEntry constructor reads export data)
            inStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                ExportEntry e = new ExportEntry(this, inStream) { Index = i };
                exports.Add(e);
            }

            inStream.Seek(DependencyTableOffset, SeekOrigin.Begin);
            OriginalDependencyTableSize = inStream.ReadUInt32();

            if (Game == Mod.MEGame.ME1)
            {
                ReadLocalTLKs();
            }
        }

        private MEPackage(string filePath, Mod.MEGame forceGame = Mod.MEGame.Unknown, bool onlyHeader = false)
        {
            FilePath = Path.GetFullPath(filePath);

            if (forceGame != Mod.MEGame.Unknown)
            {
                //new Package
                Game = forceGame;
                //reasonable defaults?
                Flags = EPackageFlags.Cooked | EPackageFlags.AllowDownload | EPackageFlags.DisallowLazyLoading | EPackageFlags.RequireImportsAlreadyLoaded;
                return;
            }

            using (var fs = File.OpenRead(filePath))
            {
                ReadPackageFromStream(fs, onlyHeader);
            }
        }
        public void save(bool compress = false)
        {
            save(FilePath, compress);
        }

        public void save(string path, bool compress = false)
        {
            bool originallyCompressed = IsCompressed;
            if (!compress || Game == Mod.MEGame.ME1) //do not compress ME1 files
            {
                Flags &= ~EPackageFlags.Compressed;
            }
            else if (compress)
            {
                Flags |= EPackageFlags.Compressed;
            }

            CompressionType compressionType = compress ? (Game == Mod.MEGame.ME3 ? CompressionType.Zlib : CompressionType.LZO) : CompressionType.None;

            try
            {
                if (CanReconstruct)
                {
                    saveByReconstructing(originallyCompressed, path, compressionType);
                }
                else
                {
                    throw new Exception($"Cannot save ME1 packages with compressed textures. Please make an issue on github: {App.BugReportURL}");
                }
            }
            finally
            {
                //If we're doing save as, reset compressed flag to reflect file on disk
                if (path != FilePath && originallyCompressed)
                {
                    Flags |= EPackageFlags.Compressed;
                }
            }
        }

        public MemoryStream saveToStream(bool compress = false, bool includeAdditionalPackagesToCook = true)
        {
            bool compressed = IsCompressed;
            if (!compress || Game == Mod.MEGame.ME1) //do not compress ME1 files
            {
                Flags &= ~EPackageFlags.Compressed;
            }
            else if (compress)
            {
                Flags |= EPackageFlags.Compressed;
            }

            CompressionType compressionType = compress ? (Game == Mod.MEGame.ME3 ? CompressionType.Zlib : CompressionType.LZO) : CompressionType.None;

            try
            {
                if (CanReconstruct)
                {
                    return saveToStreamByReconstructing(compressed, compressionType, includeAdditionalPackagesToCook);
                }
                else
                {
                    throw new Exception($"Cannot save ME1 packages with compressed textures. Please make an issue on github: {App.BugReportURL}");
                }
            }
            finally
            {
                //If we're doing save as, reset compressed flag to reflect file on disk
                if (compressed)
                {
                    Flags |= EPackageFlags.Compressed;
                }
            }
        }

        private void saveByReconstructing(bool packageOriginallyCompressed, string path, CompressionType compressionType = CompressionType.None)
        {
            var saveStream = saveToStreamByReconstructing(packageOriginallyCompressed, compressionType);
            saveStream.WriteToFile(path);
        }
        private MemoryStream saveToStreamByReconstructing(bool packageOriginallyCompressed, CompressionType compressionType = CompressionType.None, bool includeAdditionalPackagesToCook = true)
        {
            if (!includeAdditionalPackagesToCook && !packageOriginallyCompressed)
            {
                includeAdditionalPackagesToCook = true; //always write if decompressed. This flag can only do something when file is becoming decompressed
            }
            //try
            //{
            var uncompressedStream = new MemoryStream();

            //just for positioning. We write over this later when the header values have been updated
            WriteHeader(uncompressedStream, writeAdditionalPackagesToCook: includeAdditionalPackagesToCook);

            //name table
            NameOffset = (int)uncompressedStream.Position;
            NameCount = Gen0NameCount = names.Count;
            foreach (string name in names)
            {
                switch (Game)
                {
                    case Mod.MEGame.ME1:
                        uncompressedStream.WriteUnrealStringASCII(name);
                        uncompressedStream.WriteInt32(0);
                        uncompressedStream.WriteInt32(458768);
                        break;
                    case Mod.MEGame.ME2:
                        uncompressedStream.WriteUnrealStringASCII(name);
                        uncompressedStream.WriteInt32(-14);
                        break;
                    case Mod.MEGame.ME3:
                        uncompressedStream.WriteUnrealStringUnicode(name);
                        break;
                }
            }

            //import table
            ImportOffset = (int)uncompressedStream.Position;
            ImportCount = imports.Count;
            foreach (ImportEntry e in imports)
            {
                uncompressedStream.WriteFromBuffer(e.Header);
            }

            //export table
            ExportOffset = (int)uncompressedStream.Position;
            ExportCount = Gen0ExportCount = exports.Count;
            foreach (ExportEntry e in exports)
            {
                e.HeaderOffset = (uint)uncompressedStream.Position;
                uncompressedStream.WriteFromBuffer(e.Header);
            }

            DependencyTableOffset = (int)uncompressedStream.Position;

            if (OriginalDependencyTableSize > 0)
            {
                //Unreal Engine style
                for (int i = 0; i < ExportCount; i++)
                {
                    uncompressedStream.WriteInt32(0); //write same blank table back out
                }
            }
            else
            {
                //ME3EXP STYLE - BLANK (?) table
                // This shouldn't be done like this...
                uncompressedStream.WriteInt32(0);//zero-count DependencyTable
            }

            FullHeaderSize = ImportExportGuidsOffset = (int)uncompressedStream.Position;

            //export data
            foreach (ExportEntry e in exports)
            {
                switch (Game)
                {
                    case Mod.MEGame.ME1:
                        UpdateME1Offsets(e, (int)uncompressedStream.Position);
                        break;
                    case Mod.MEGame.ME2:
                        UpdateME2Offsets(e, (int)uncompressedStream.Position);
                        break;
                    case Mod.MEGame.ME3:
                        UpdateME3Offsets(e, (int)uncompressedStream.Position);
                        break;
                }

                e.DataOffset = (int)uncompressedStream.Position;


                uncompressedStream.WriteFromBuffer(e.Data);
                //update size and offset in already-written header
                long pos = uncompressedStream.Position;
                uncompressedStream.JumpTo(e.HeaderOffset + 32);
                uncompressedStream.WriteInt32(e.DataSize); //DataSize might have been changed by UpdateOffsets
                uncompressedStream.WriteInt32(e.DataOffset);
                uncompressedStream.JumpTo(pos);
            }

            //re-write header with updated values
            uncompressedStream.JumpTo(0);
            WriteHeader(uncompressedStream, writeAdditionalPackagesToCook: includeAdditionalPackagesToCook);

            if (compressionType == CompressionType.None)
            {
                return uncompressedStream;
            }
            else
            {
                MemoryStream compressedStream = new MemoryStream();
                WriteHeader(compressedStream, writeAdditionalPackagesToCook: includeAdditionalPackagesToCook); //for positioning
                var chunks = new List<CompressionHelper.Chunk>();

                //Compression format:
                //uint ChunkMetaDataTableCount
                //CHUNK METADATA TABLE ENTRIES:
                //uint UncompressedOffset
                //uint UncompressedSize
                //uint CompressedOffset
                //uint CompressedSize
                //
                // After ChunkMetaDataTableCount * 16 bytes the chunk blocks begin
                // Each chunk block has it's own block header.
                //

                CompressionHelper.Chunk chunk = new CompressionHelper.Chunk();
                //Tables chunk
                chunk.uncompressedSize = FullHeaderSize - NameOffset;
                chunk.uncompressedOffset = NameOffset;

                #region DEBUG STUFF
                //string firstElement = "Tables";
                //string lastElement = firstElement;

                //MemoryStream m2 = new MemoryStream();
                //long pos = uncompressedStream.Position;
                //uncompressedStream.Position = NameOffset;
                //m2.WriteFromStream(uncompressedStream, chunk.uncompressedSize);
                //uncompressedStream.Position = pos;
                #endregion

                //Export data chunks
                //chunk = new CompressionHelper.Chunk();
                int chunkNum = 0;
                //Debug.WriteLine($"Exports start at {Exports[0].DataOffset}");
                foreach (ExportEntry e in Exports)
                {
                    if (chunk.uncompressedSize + e.DataSize > CompressionHelper.MAX_CHUNK_SIZE)
                    {
                        //Rollover to the next chunk as this chunk would be too big if we tried to put this export into the chunk
                        chunks.Add(chunk);
                        //Debug.WriteLine($"Chunk {chunkNum} ({chunk.uncompressedSize} bytes) contains {firstElement} to {lastElement} - 0x{chunk.uncompressedOffset:X6} to 0x{(chunk.uncompressedSize + chunk.uncompressedOffset):X6}");
                        chunkNum++;
                        chunk = new CompressionHelper.Chunk
                        {
                            uncompressedSize = e.DataSize,
                            uncompressedOffset = e.DataOffset
                        };
                    }
                    else
                    {
                        chunk.uncompressedSize += e.DataSize; //This chunk can fit this export
                    }
                }
                //Debug.WriteLine($"Chunk {chunkNum} contains {firstElement} to {lastElement}");
                chunks.Add(chunk);

                //Rewrite header with chunk table information so we can position the data blocks after table
                compressedStream.Position = 0;
                WriteHeader(compressedStream, compressionType, chunks, includeAdditionalPackagesToCook);
                MemoryStream m1 = new MemoryStream();

                for (int c = 0; c < chunks.Count; c++)
                {
                    chunk = chunks[c];
                    chunk.compressedOffset = (int)compressedStream.Position;
                    chunk.compressedSize = 0; // filled later

                    int dataSizeRemainingToCompress = chunk.uncompressedSize;
                    //weird way to do this
                    //int newNumBlocks = (chunk.uncompressedSize / CompressionHelper.MAX_BLOCK_SIZE - 1) / CompressionHelper.MAX_BLOCK_SIZE;
                    int numBlocksInChunk = (int)Math.Ceiling(chunk.uncompressedSize * 1.0 / CompressionHelper.MAX_BLOCK_SIZE);
                    // skip chunk header and blocks table - filled later
                    compressedStream.Seek(CompressionHelper.SIZE_OF_CHUNK_HEADER + CompressionHelper.SIZE_OF_CHUNK_BLOCK_HEADER * numBlocksInChunk, SeekOrigin.Current);

                    uncompressedStream.JumpTo(chunk.uncompressedOffset);

                    chunk.blocks = new List<CompressionHelper.Block>();

                    //Calculate blocks by splitting data into 128KB "block chunks".
                    for (int b = 0; b < numBlocksInChunk; b++)
                    {
                        CompressionHelper.Block block = new CompressionHelper.Block();
                        block.uncompressedsize = Math.Min(CompressionHelper.MAX_BLOCK_SIZE, dataSizeRemainingToCompress);
                        dataSizeRemainingToCompress -= block.uncompressedsize;
                        block.uncompressedData = uncompressedStream.ReadToBuffer(block.uncompressedsize);
                        chunk.blocks.Add(block);
                    }

                    if (chunk.blocks.Count != numBlocksInChunk) throw new Exception("Number of blocks does not match expected amount");

                    //Compress blocks
                    Parallel.For(0, chunk.blocks.Count, b =>
                    {
                        CompressionHelper.Block block = chunk.blocks[b];
                        if (compressionType == CompressionType.LZO)
                            block.compressedData = LZO2Helper.LZO2.Compress(block.uncompressedData);
                        else if (compressionType == CompressionType.Zlib)
                            block.compressedData = ZlibHelper.Zlib.Compress(block.uncompressedData);
                        else
                            throw new Exception("Internal error: Unsupported compression type for compressing blocks: " + compressionType);
                        if (block.compressedData.Length == 0)
                            throw new Exception("Internal error: Block compression failed! Compressor returned no bytes");
                        block.compressedsize = (int)block.compressedData.Length;
                        chunk.blocks[b] = block;
                    });

                    //Write compressed data to stream 
                    for (int b = 0; b < numBlocksInChunk; b++)
                    {
                        var block = chunk.blocks[b];
                        compressedStream.Write(block.compressedData, 0, (int)block.compressedsize);
                        chunk.compressedSize += block.compressedsize;
                    }
                    chunks[c] = chunk;
                }

                //Update each chunk header with new information
                for (int c = 0; c < chunks.Count; c++)
                {
                    chunk = chunks[c];
                    compressedStream.JumpTo(chunk.compressedOffset); // jump to blocks header
                    compressedStream.WriteUInt32(packageTag);
                    compressedStream.WriteUInt32(CompressionHelper.MAX_BLOCK_SIZE); //128 KB
                    compressedStream.WriteInt32(chunk.compressedSize);
                    compressedStream.WriteInt32(chunk.uncompressedSize);

                    //write block header table
                    foreach (var block in chunk.blocks)
                    {
                        compressedStream.WriteInt32(block.compressedsize);
                        compressedStream.WriteInt32(block.uncompressedsize);
                    }
                }
                //Write final header
                compressedStream.Position = 0;
                WriteHeader(compressedStream, compressionType, chunks, includeAdditionalPackagesToCook);

                //for (int c = 0; c < chunks.Count; c++)
                //{
                //    chunk = chunks[c];
                //    chunk.blocks.Clear();
                //    chunk.blocks = null;
                //}
                //chunks.Clear();
                //chunks = null;
                return compressedStream;
                //compressedStream.WriteToFile(path);
                //File.WriteAllBytes(path, compressedStream.ToArray());
                // validation
                //var validatePackage = MEPackageHandler.OpenMEPackage(path);
                //foreach (var export in validatePackage.Exports)
                //{
                //    export.GetProperties();
                //}
            }
            //AfterSave();
            //}
            //catch (Exception ex)
            //{
            //    // TODO: Implement this for how Mod Manager saves things.
            //    throw ex;
            //    //MessageBox.Show($"Error saving {FilePath}:\n{App.FlattenException(ex)}");
            //}
        }

        private void WriteHeader(Stream ms, CompressionType compressionType = CompressionType.None, List<CompressionHelper.Chunk> chunks = null, bool writeAdditionalPackagesToCook = true)
        {
            if (chunks == null) chunks = new List<CompressionHelper.Chunk>();
            if (ms.Position != 0) throw new Exception("Must position stream to zero when writing header data to stream");
            ms.WriteUInt32(packageTag);
            //version
            switch (Game)
            {
                case Mod.MEGame.ME1:
                    ms.WriteUInt16(491);
                    ms.WriteUInt16(1008);
                    break;
                case Mod.MEGame.ME2:
                    ms.WriteUInt16(512);
                    ms.WriteUInt16(130);
                    break;
                case Mod.MEGame.ME3:
                    ms.WriteUInt16(684);
                    ms.WriteUInt16(194);
                    break;
            }
            ms.WriteInt32(FullHeaderSize);
            if (Game == Mod.MEGame.ME3)
            {
                ms.WriteUnrealStringUnicode("None");
            }
            else
            {
                ms.WriteUnrealStringASCII("None");
            }

            ms.WriteUInt32((uint)Flags);

            if (Game == Mod.MEGame.ME3 && Flags.HasFlag(EPackageFlags.Cooked))
            {
                //Restore Package ID
                // 0 = Normal Packages
                // 1 = Patch Packages
                ms.WriteInt32(PackageTypeId);
            }

            ms.WriteInt32(NameCount);
            ms.WriteInt32(NameOffset);
            ms.WriteInt32(ExportCount);
            ms.WriteInt32(ExportOffset);
            ms.WriteInt32(ImportCount);
            ms.WriteInt32(ImportOffset);
            ms.WriteInt32(DependencyTableOffset);

            if (Game == Mod.MEGame.ME3)
            {
                ms.WriteInt32(ImportExportGuidsOffset);
                ms.WriteInt32(0); //ImportGuidsCount
                ms.WriteInt32(0); //ExportGuidsCount
                ms.WriteInt32(0); //ThumbnailTableOffset
            }
            ms.WriteGuid(PackageGuid);

            //Write 1 generation
            ms.WriteInt32(1);
            ms.WriteInt32(Gen0ExportCount);
            ms.WriteInt32(Gen0NameCount);
            ms.WriteInt32(Gen0NetworkedObjectCount);

            //engineVersion and cookedContentVersion
            switch (Game)
            {
                case Mod.MEGame.ME1:
                    ms.WriteInt32(3240);
                    ms.WriteInt32(47);
                    break;
                case Mod.MEGame.ME2:
                    ms.WriteInt32(3607);
                    ms.WriteInt32(64);
                    break;
                case Mod.MEGame.ME3:
                    ms.WriteInt32(6383);
                    ms.WriteInt32(196715);
                    break;
            }


            if (Game == Mod.MEGame.ME2 || Game == Mod.MEGame.ME1)
            {
                ms.WriteInt32(0);
                ms.WriteInt32(47699); //No idea what this is, but it's always 47699
                switch (Game)
                {
                    case Mod.MEGame.ME1:
                        ms.WriteInt32(0);
                        ms.WriteInt32(1);
                        break;
                    case Mod.MEGame.ME2:
                        ms.WriteInt32(unknown4);
                        ms.WriteInt32(1966080);
                        break;
                }
            }

            switch (Game)
            {
                case Mod.MEGame.ME1:
                    ms.WriteInt32(0);
                    ms.WriteInt32(-1);
                    break;
                case Mod.MEGame.ME2:
                    ms.WriteInt32(-1);
                    ms.WriteInt32(-1);
                    break;
                case Mod.MEGame.ME3:
                    ms.WriteInt32(unknown6);
                    ms.WriteInt32(145358848);
                    break;
            }

            if (Game == Mod.MEGame.ME1)
            {
                ms.WriteInt32(-1);
            }

            ms.WriteUInt32((uint)compressionType);
            //Chunks
            if (compressionType != CompressionType.None && chunks.Count == 0) throw new Exception("Can't save with compression type if there are no compressed chunks in header!'");
            ms.WriteInt32(chunks.Count);
            int i = 0;
            foreach (var chunk in chunks)
            {
                ms.WriteInt32(chunk.uncompressedOffset);
                ms.WriteInt32(chunk.uncompressedSize);
                ms.WriteInt32(chunk.compressedOffset);
                if (chunk.blocks != null)
                {
                    var chunksize = chunk.compressedSize + CompressionHelper.SIZE_OF_CHUNK_HEADER + CompressionHelper.SIZE_OF_CHUNK_BLOCK_HEADER * chunk.blocks.Count;
                    Debug.WriteLine($"Writing chunk table chunk {i} size: {chunksize}");
                    ms.WriteInt32(chunksize); //Size of compressed data + chunk header + block header * number of blocks in the chunk
                }
                else
                {
                    //list is null - might not be populated yet
                    ms.WriteInt32(0); //write zero for now, we will call this method later with the compressedSize populated.
                }

                i++;
            }

            ms.WriteUInt32(packageSource);

            if (Game == Mod.MEGame.ME2 || Game == Mod.MEGame.ME1)
            {
                ms.WriteInt32(0);
            }

            if (writeAdditionalPackagesToCook && (Game == Mod.MEGame.ME3 || Game == Mod.MEGame.ME2))
            {
                //this code is not in me3exp right now
                ms.WriteInt32(AdditionalPackagesToCook.Count);
                foreach (var pname in AdditionalPackagesToCook)
                {
                    if (Game == Mod.MEGame.ME2)
                    {
                        //ME2 Uses ASCII
                        ms.WriteUnrealStringASCII(pname);
                    }
                    else
                    {
                        ms.WriteUnrealStringUnicode(pname);
                    }
                }
            }
            else if (!writeAdditionalPackagesToCook && (Game == Mod.MEGame.ME3 || Game == Mod.MEGame.ME2))
            {
                ms.WriteInt32(0); //4 bytes...? old pccobject class copied data but it seems to be wrong.
            }


            //Chunk data is written in package saving code
        }

        public List<TalkFileME1> LocalTalkFiles { get; } = new List<TalkFileME1>();
        public object StorageTypes { get; private set; }
        /// <summary>
        /// Used to help debug packages that are loaded from streams that don't have a FilePath
        /// </summary>
        public string FileSourceForDebugging { get; set; }
        private void ReadLocalTLKs()
        {
            LocalTalkFiles.Clear();
            List<ExportEntry> tlkFileSets = Exports.Where(x => x.ClassName == "BioTlkFileSet" && !x.ObjectName.StartsWith("Default__")).ToList();
            var exportsToLoad = new List<ExportEntry>();
            foreach (var tlkFileSet in tlkFileSets)
            {
                MemoryStream r = new MemoryStream(tlkFileSet.Data);
                r.Position = tlkFileSet.propsEnd();
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int langRef = r.ReadInt32();
                    r.ReadInt32(); //second half of name
                    string lang = getNameEntry(langRef);
                    int numTlksForLang = r.ReadInt32(); //I believe this is always 2. Hopefully I am not wrong.
                    int maleTlk = r.ReadInt32();
                    int femaleTlk = r.ReadInt32();

                    //TODO: Implement this somehow. Not sure if this is even relevant for Mod Manager.
                    //if (MassEffectModManager.Properties.Settings.Default.TLKLanguage.Equals(lang, StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    exportsToLoad.Add(getUExport(MassEffectModManager.Properties.Settings.Default.TLKGender_IsMale ? maleTlk : femaleTlk));
                    //    break;
                    //}

                    //r.ReadInt64();
                    //talkFiles.Add(new TalkFile(pcc, r.ReadInt32(), true, langRef, index));
                    //talkFiles.Add(new TalkFile(pcc, r.ReadInt32(), false, langRef, index));
                }
            }

            foreach (var exp in exportsToLoad)
            {
                //Debug.WriteLine("Loading local TLK: " + exp.GetIndexedFullPath);
                LocalTalkFiles.Add(new TalkFileME1(exp));
            }
        }

        private static void UpdateME1Offsets(ExportEntry export, int newDataOffset)
        {
            if (export.IsDefaultObject)
            {
                return; //this is not actually instance of that class
            }
            if (export.IsTexture())
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream binData = new MemoryStream(export.getBinaryData());
                binData.Skip(12);
                binData.WriteInt32(baseOffset + (int)binData.Position + 4);
                for (int i = binData.ReadInt32(); i > 0 && binData.Position < binData.Length; i--)
                {
                    var storageFlags = (StorageFlags)binData.ReadInt32();
                    if (!storageFlags.HasFlag(StorageFlags.externalFile)) //pcc-stored
                    {
                        int uncompressedSize = binData.ReadInt32();
                        int compressedSize = binData.ReadInt32();
                        binData.WriteInt32(baseOffset + (int)binData.Position + 4);//update offset
                        binData.Seek((storageFlags == StorageFlags.noFlags ? uncompressedSize : compressedSize) + 8, SeekOrigin.Current); //skip texture and width + height values
                    }
                    else
                    {
                        binData.Seek(20, SeekOrigin.Current);//skip whole rest of mip definition
                    }
                }
                export.setBinaryData(binData.ToArray());
            }
            else if (export.ClassName == "StaticMeshComponent")
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream bin = new MemoryStream(export.Data);
                bin.JumpTo(export.propsEnd());

                int lodDataCount = bin.ReadInt32();
                for (int i = 0; i < lodDataCount; i++)
                {
                    int shadowMapCount = bin.ReadInt32();
                    bin.Skip(shadowMapCount * 4);
                    int shadowVertCount = bin.ReadInt32();
                    bin.Skip(shadowVertCount * 4);
                    int lightMapType = bin.ReadInt32();
                    if (lightMapType == 0) continue;
                    int lightGUIDsCount = bin.ReadInt32();
                    bin.Skip(lightGUIDsCount * 16);
                    switch (lightMapType)
                    {
                        case 1:
                            bin.Skip(4 + 8);
                            int bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(12 * 4 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            break;
                        case 2:
                            bin.Skip((16) * 4 + 16);
                            break;
                    }
                }
            }
        }

        private static void UpdateME2Offsets(ExportEntry export, int newDataOffset)
        {
            if (export.IsDefaultObject)
            {
                return; //this is not actually instance of that class
            }
            //update offsets for pcc-stored audio in wwisestreams
            if (export.ClassName == "WwiseStream" && export.GetProperty<NameProperty>("Filename") == null)
            {
                byte[] binData = export.getBinaryData();
                if (binData.Length < 44)
                {
                    return; //¯\_(ツ)_ /¯
                }
                binData.OverwriteRange(44, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 48));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored mips in Textures
            else if (export.ClassName == "WwiseBank")
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(20, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 24));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored mips in Textures
            //This I do not think is actually necessary
            //else if (export.IsTexture())
            //{
            //    int baseOffset = newDataOffset + export.propsEnd();
            //    MemoryStream binData = new MemoryStream(export.getBinaryData());
            //    binData.Skip(12);
            //    binData.WriteInt32(baseOffset + (int)binData.Position + 4);
            //    for (int i = binData.ReadInt32(); i > 0 && binData.Position < binData.Length; i--)
            //    {
            //        var storageFlags = (StorageFlags)binData.ReadInt32();
            //        if (!storageFlags.HasFlag(StorageFlags.externalFile)) //pcc-stored
            //        {
            //            int uncompressedSize = binData.ReadInt32();
            //            int compressedSize = binData.ReadInt32();
            //            binData.WriteInt32(baseOffset + (int)binData.Position + 4);//update offset
            //            binData.Seek((storageFlags == StorageFlags.noFlags ? uncompressedSize : compressedSize) + 8, SeekOrigin.Current); //skip texture and width + height values
            //        }
            //        else
            //        {
            //            binData.Seek(20, SeekOrigin.Current);//skip whole rest of mip definition
            //        }
            //    }
            //    export.setBinaryData(binData.ToArray());
            //}
            else if (export.ClassName == "ShaderCache")
            {
                int oldDataOffset = export.DataOffset;

                MemoryStream binData = new MemoryStream(export.Data);
                binData.Seek(export.propsEnd() + 1, SeekOrigin.Begin);

                int nameList1Count = binData.ReadInt32();
                binData.Seek(nameList1Count * 12, SeekOrigin.Current);

                int shaderCount = binData.ReadInt32();
                for (int i = 0; i < shaderCount; i++)
                {
                    binData.Seek(24, SeekOrigin.Current);
                    int nextShaderOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextShaderOffset + newDataOffset);
                    binData.Seek(nextShaderOffset, SeekOrigin.Begin);
                }

                int vertexFactoryMapCount = binData.ReadInt32();
                binData.Seek(vertexFactoryMapCount * 12, SeekOrigin.Current);

                int materialShaderMapCount = binData.ReadInt32();
                for (int i = 0; i < materialShaderMapCount; i++)
                {
                    binData.Seek(16, SeekOrigin.Current);

                    int switchParamCount = binData.ReadInt32();
                    binData.Seek(switchParamCount * 32, SeekOrigin.Current);

                    int componentMaskParamCount = binData.ReadInt32();
                    binData.Seek(componentMaskParamCount * 44, SeekOrigin.Current);

                    int nextMaterialShaderMapOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextMaterialShaderMapOffset + newDataOffset);
                    binData.Seek(nextMaterialShaderMapOffset, SeekOrigin.Begin);
                }

                export.Data = binData.ToArray();
            }
            else if (export.ClassName == "StaticMeshComponent")
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream bin = new MemoryStream(export.Data);
                bin.JumpTo(export.propsEnd());

                int lodDataCount = bin.ReadInt32();
                for (int i = 0; i < lodDataCount; i++)
                {
                    int shadowMapCount = bin.ReadInt32();
                    bin.Skip(shadowMapCount * 4);
                    int shadowVertCount = bin.ReadInt32();
                    bin.Skip(shadowVertCount * 4);
                    int lightMapType = bin.ReadInt32();
                    if (lightMapType == 0) continue;
                    int lightGUIDsCount = bin.ReadInt32();
                    bin.Skip(lightGUIDsCount * 16);
                    switch (lightMapType)
                    {
                        case 1:
                            bin.Skip(4 + 8);
                            int bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(12 * 4 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            break;
                        case 2:
                            bin.Skip((16) * 4 + 16);
                            break;
                    }
                }
            }
        }

        private static void UpdateME3Offsets(ExportEntry export, int newDataOffset)
        {
            if (export.IsDefaultObject)
            {
                return; //this is not actually instance of that class
            }
            //update offsets for pcc-stored audio in wwisestreams
            if ((export.ClassName == "WwiseStream" && export.GetProperty<NameProperty>("Filename") == null) || export.ClassName == "WwiseBank")
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(12, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 16));
                export.setBinaryData(binData);
            }
            //update offsets for pcc-stored movies in texturemovies
            else if (export.ClassName == "TextureMovie" && export.GetProperty<NameProperty>("TextureFileCacheName") == null)
            {
                byte[] binData = export.getBinaryData();
                binData.OverwriteRange(12, BitConverter.GetBytes(newDataOffset + export.propsEnd() + 16));
                export.setBinaryData(binData);
            }
            else if (export.ClassName == "ShaderCache")
            {
                int oldDataOffset = export.DataOffset;

                MemoryStream binData = new MemoryStream(export.Data);
                binData.Seek(export.propsEnd() + 1, SeekOrigin.Begin);

                int nameList1Count = binData.ReadInt32();
                binData.Seek(nameList1Count * 12, SeekOrigin.Current);

                int namelist2Count = binData.ReadInt32();//namelist2
                binData.Seek(namelist2Count * 12, SeekOrigin.Current);

                int shaderCount = binData.ReadInt32();
                for (int i = 0; i < shaderCount; i++)
                {
                    binData.Seek(24, SeekOrigin.Current);
                    int nextShaderOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextShaderOffset + newDataOffset);
                    binData.Seek(nextShaderOffset, SeekOrigin.Begin);
                }

                int vertexFactoryMapCount = binData.ReadInt32();
                binData.Seek(vertexFactoryMapCount * 12, SeekOrigin.Current);

                int materialShaderMapCount = binData.ReadInt32();
                for (int i = 0; i < materialShaderMapCount; i++)
                {
                    binData.Seek(16, SeekOrigin.Current);

                    int switchParamCount = binData.ReadInt32();
                    binData.Seek(switchParamCount * 32, SeekOrigin.Current);

                    int componentMaskParamCount = binData.ReadInt32();
                    binData.Seek(componentMaskParamCount * 44, SeekOrigin.Current);

                    int normalParams = binData.ReadInt32();
                    binData.Seek(normalParams * 29, SeekOrigin.Current);

                    binData.Seek(8, SeekOrigin.Current);

                    int nextMaterialShaderMapOffset = binData.ReadInt32() - oldDataOffset;
                    binData.Seek(-4, SeekOrigin.Current);
                    binData.WriteInt32(nextMaterialShaderMapOffset + newDataOffset);
                    binData.Seek(nextMaterialShaderMapOffset, SeekOrigin.Begin);
                }

                export.Data = binData.ToArray();
            }
        }
    }
}