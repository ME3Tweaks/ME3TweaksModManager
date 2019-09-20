using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Gammtek.Conduit.Extensions.IO;
using MassEffectModManager;
using MassEffectModManager.GameDirectories;
using MassEffectModManager.gamefileformats;
using MassEffectModManager.modmanager;
using MassEffectModManager.modmanager.helpers;
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

    public sealed class MEPackage : UnrealPackageFile, IMEPackage
    {
        public Mod.MEGame Game { get; private set; } //can only be ME1, ME2, or ME3. UDK is a seperate class

        public bool CanReconstruct =>
            Game == Mod.MEGame.ME3 ||
            Game == Mod.MEGame.ME2 ||
            Game == Mod.MEGame.ME1; //mod manager has no ui to modify things and we won't be modifying ME1 files except for blank TLK files.

        public int FullHeaderSize { get; private set; }
        public EPackageFlags Flags { get; private set; }

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

        static bool isInitialized;
        public static Func<string, Mod.MEGame, MEPackage> Initialize()
        {
            if (isInitialized)
            {
                throw new Exception(nameof(MEPackage) + " can only be initialized once");
            }

            isInitialized = true;
            return (f, g) => new MEPackage(f, g);
        }

        private MEPackage(string filePath, Mod.MEGame forceGame = Mod.MEGame.Unknown)
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
                #region Header

                uint magic = fs.ReadUInt32();
                if (magic != packageTag)
                {
                    throw new FormatException("Not an Unreal package!");
                }
                ushort unrealVersion = fs.ReadUInt16();
                ushort licenseeVersion = fs.ReadUInt16();
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
                FullHeaderSize = fs.ReadInt32();
                int foldernameStrLen = fs.ReadInt32();
                //always "None", so don't bother saving result
                if (foldernameStrLen > 0)
                    fs.ReadStringASCIINull(foldernameStrLen);
                else
                    fs.ReadStringUnicodeNull(foldernameStrLen * -2);

                Flags = (EPackageFlags)fs.ReadUInt32();

                if (Game == Mod.MEGame.ME3 && Flags.HasFlag(EPackageFlags.Cooked))
                {
                    fs.SkipInt32(); //always 0
                }

                NameCount = fs.ReadInt32();
                NameOffset = fs.ReadInt32();
                ExportCount = fs.ReadInt32();
                ExportOffset = fs.ReadInt32();
                ImportCount = fs.ReadInt32();
                ImportOffset = fs.ReadInt32();
                DependencyTableOffset = fs.ReadInt32();

                if (Game == Mod.MEGame.ME3)
                {
                    ImportExportGuidsOffset = fs.ReadInt32();
                    fs.SkipInt32(); //ImportGuidsCount always 0
                    fs.SkipInt32(); //ExportGuidsCount always 0
                    fs.SkipInt32(); //ThumbnailTableOffset always 0
                }

                PackageGuid = fs.ReadGuid();
                uint generationsTableCount = fs.ReadUInt32();
                if (generationsTableCount > 0)
                {
                    generationsTableCount--;
                    Gen0ExportCount = fs.ReadInt32();
                    Gen0NameCount = fs.ReadInt32();
                    Gen0NetworkedObjectCount = fs.ReadInt32();
                }
                //should never be more than 1 generation, but just in case
                fs.Skip(generationsTableCount * 12);

                fs.SkipInt32();//engineVersion          Like unrealVersion and licenseeVersion, these 2 are determined by what game this is,
                fs.SkipInt32();//cookedContentVersion   so we don't have to read them in

                if (Game == Mod.MEGame.ME2 || Game == Mod.MEGame.ME1)
                {
                    fs.SkipInt32(); //always 0
                    fs.SkipInt32(); //always 47699
                    unknown4 = fs.ReadInt32();
                    fs.SkipInt32(); //always 1 in ME1, always 1966080 in ME2
                }

                unknown6 = fs.ReadInt32();
                fs.SkipInt32(); //always -1 in ME1 and ME2, always 145358848 in ME3

                if (Game == Mod.MEGame.ME1)
                {
                    fs.SkipInt32(); //always -1
                }

                //skip compression type chunks. Decompressor will handle that
                fs.SkipInt32();
                int numChunks = fs.ReadInt32();
                fs.Skip(numChunks * 16);

                packageSource = fs.ReadUInt32();

                if (Game == Mod.MEGame.ME2 || Game == Mod.MEGame.ME1)
                {
                    fs.SkipInt32(); //always 0
                }

                //Doesn't need to be written out, so it doesn't need to be read in
                //keep this here in case one day we learn that this has a purpose
                /*if (Game == MEGame.ME2 || Game == MEGame.ME3)
                {
                    int additionalPackagesToCookCount = fs.ReadInt32();
                    var additionalPackagesToCook = new string[additionalPackagesToCookCount];
                    for (int i = 0; i < additionalPackagesToCookCount; i++)
                    {
                        int strLen = fs.ReadInt32();
                        if (strLen > 0)
                        {
                            additionalPackagesToCook[i] = fs.ReadStringASCIINull(strLen);
                        }
                        else
                        {
                            additionalPackagesToCook[i] = fs.ReadStringUnicodeNull(strLen * -2);
                        }
                    }
                }*/
                #endregion

                Stream inStream = fs;
                if (IsCompressed && numChunks > 0)
                {
                    inStream = Game == Mod.MEGame.ME3 ? CompressionHelper.DecompressME3(fs) : CompressionHelper.DecompressME1orME2(fs);
                }

                //read namelist
                inStream.JumpTo(NameOffset);
                for (int i = 0; i < NameCount; i++)
                {
                    names.Add(inStream.ReadUnrealString());
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

                if (Game == Mod.MEGame.ME1)
                {
                    ReadLocalTLKs();
                }
            }
        }
        public void save()
        {
            save(FilePath);
        }

        public void save(string path, bool compress = false)
        {
            bool compressed = IsCompressed;
            if (!compress || Game == Mod.MEGame.ME1) //do not compress ME1 files
            {
                Flags &= ~EPackageFlags.Compressed;
            }
            else
            {
                Flags |= EPackageFlags.Compressed;
            }

            try
            {
                if (CanReconstruct)
                {
                    saveByReconstructing(path, compress);
                }
                else
                {
                    throw new Exception($"Cannot save ME1 packages with compressed textures. Please make an issue on github: {App.BugReportURL}");
                }
            }
            finally
            {
                //If we're doing save as, reset compressed flag to reflect file on disk
                if (path != FilePath && compressed)
                {
                    Flags |= EPackageFlags.Compressed;
                }
            }
        }

        private void saveByReconstructing(string path, bool compress = false)
        {
            try
            {
                var uncompressedStream = new MemoryStream();

                //just for positioning. We write over this later when the header values have been updated
                WriteHeader(uncompressedStream, compress);

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
                uncompressedStream.WriteInt32(0);//zero-count DependencyTable
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
                WriteHeader(uncompressedStream);

                if (!compress)
                {
                    File.WriteAllBytes(path, uncompressedStream.ToArray());
                }
                else
                {
                    MemoryStream fs = new MemoryStream();
                    WriteHeader(fs, true); //for positioning
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
                    //chunks.Add(chunk); //header table can include export data if it fits.

                    //Export data chunks
                    chunk = new CompressionHelper.Chunk();
                    foreach (ExportEntry e in Exports)
                    {
                        if (chunk.uncompressedSize + e.DataSize > CompressionHelper.MAX_CHUNK_SIZE)
                        {
                            //Rollover to the next chunk as this chunk would be too big if we tried to put this export into the chunk
                            chunks.Add(chunk);
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

                    //If maybe the package is empty or something or a perfect boundary alignment occured
                    //this will ensure last chunk is added.
                    if (chunk.uncompressedSize > 0)
                    {
                        chunks.Add(chunk);
                    }

                    //Rewrite header with chunk table information
                    var compressionType = Game == Mod.MEGame.ME3 ? CompressionType.Zlib : CompressionType.LZO;
                    uncompressedStream.WriteUInt32((uint)compressionType);
                    uncompressedStream.WriteInt32(0);//numChunks 

                    uncompressedStream.WriteUInt32(packageSource);

                    if (Game == Mod.MEGame.ME2 || Game == Mod.MEGame.ME1)
                    {
                        uncompressedStream.WriteInt32(0);
                    }

                    if (Game == Mod.MEGame.ME3 || Game == Mod.MEGame.ME2)
                    {
                        uncompressedStream.WriteInt32(0);//empty additionalPackagesToCook array
                    }

                    int compressionTypeOffset = 0;
                    switch (Game)
                    {
                        case Mod.MEGame.ME1:
                            compressionTypeOffset =
                            break;
                        case Mod.MEGame.ME2:

                            break;
                        case Mod.MEGame.ME3:
                            compressionTypeOffset = 0x7E;
                            break;
                    }
                    uncompressedStream.Position = NameOffset -
                    fs.Write(packageHeader, 0, packageHeader.Length);
                    fs.WriteUInt32((uint)targetCompression);
                    fs.WriteUInt32((uint)chunks.Count);
                    fs.Skip(SizeOfChunk * chunks.Count); // skip chunks table - filled later
                    fs.WriteUInt32(someTag);
                    if (version == packageFileVersionME2)
                        fs.WriteUInt32(0); // const 0
                    //saveExtraNames(fs);

                    for (int c = 0; c < chunks.Count; c++)
                    {
                        chunk = chunks[c];
                        chunk.comprOffset = (uint)fs.Position;
                        chunk.comprSize = 0; // filled later

                        uint dataBlockLeft = chunk.uncomprSize;
                        uint newNumBlocks = (chunk.uncomprSize + maxBlockSize - 1) / maxBlockSize;
                        // skip blocks header and table - filled later
                        fs.Seek(SizeOfChunk + SizeOfChunkBlock * newNumBlocks, SeekOrigin.Current);

                        tempOutput.JumpTo(chunk.uncomprOffset);

                        chunk.blocks = new List<ChunkBlock>();
                        for (int b = 0; b < newNumBlocks; b++)
                        {
                            ChunkBlock block = new ChunkBlock();
                            block.uncomprSize = Math.Min(maxBlockSize, dataBlockLeft);
                            dataBlockLeft -= block.uncomprSize;
                            block.uncompressedBuffer = tempOutput.ReadToBuffer(block.uncomprSize);
                            chunk.blocks.Add(block);
                        }

                        Parallel.For(0, chunk.blocks.Count, b =>
                        {
                            ChunkBlock block = chunk.blocks[b];
                            if (Game == Mod.MEGame.ME2)
                                block.compressedBuffer = LZO2Helper.LZO2.Compress(block.uncompressedBuffer);
                            else if (Game == Mod.MEGame.ME3 || Game == Mod.MEGame.ME1) //Todo: check this is accurate. I am pretty sure ME1 uses ZLib...
                                block.compressedBuffer = ZlibHelper.Zlib.Compress(block.uncompressedBuffer);
                            else
                                throw new Exception("Compression type not expected!");
                            if (block.compressedBuffer.Length == 0)
                                throw new Exception("Compression failed!");
                            block.comprSize = (uint)block.compressedBuffer.Length;
                            chunk.blocks[b] = block;
                        });

                        for (int b = 0; b < newNumBlocks; b++)
                        {
                            ChunkBlock block = chunk.blocks[b];
                            fs.Write(block.compressedBuffer, 0, (int)block.comprSize);
                            chunk.comprSize += block.comprSize;
                        }
                        chunks[c] = chunk;
                    }

                    for (int c = 0; c < chunks.Count; c++)
                    {
                        chunk = chunks[c];
                        fs.JumpTo(chunksTableOffset + c * SizeOfChunk); // jump to chunks table
                        fs.WriteUInt32(chunk.uncomprOffset);
                        fs.WriteUInt32(chunk.uncomprSize);
                        fs.WriteUInt32(chunk.comprOffset);
                        fs.WriteUInt32(chunk.comprSize + SizeOfChunk + SizeOfChunkBlock * (uint)chunk.blocks.Count);
                        fs.JumpTo(chunk.comprOffset); // jump to blocks header
                        fs.WriteUInt32(packageTag);
                        fs.WriteUInt32(maxBlockSize);
                        fs.WriteUInt32(chunk.comprSize);
                        fs.WriteUInt32(chunk.uncomprSize);
                        foreach (ChunkBlock block in chunk.blocks)
                        {
                            fs.WriteUInt32(block.comprSize);
                            fs.WriteUInt32(block.uncomprSize);
                        }
                    }
                    for (int c = 0; c < chunks.Count; c++)
                    {
                        chunk = chunks[c];
                        chunk.blocks.Clear();
                        chunk.blocks = null;
                    }
                    chunks.Clear();
                    chunks = null;
                }
                AfterSave();
            }
            catch (Exception ex)
            {
                // TODO: Implement this for how Mod Manager saves things.
                throw ex;
                //MessageBox.Show($"Error saving {FilePath}:\n{App.FlattenException(ex)}");
            }
        }

        private void WriteHeader(Stream ms, bool compress = false)
        {
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
                ms.WriteInt32(0);
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

            //TODO: Unsure if this code from MEM is correct, if I recall ZLib for ME2 had terrible performance, this should probably be LZO for ME2
            ms.WriteUInt32((uint)(compress ? (Game == Mod.MEGame.ME2 ? CompressionType.LZO : CompressionType.Zlib) : CompressionType.None));
            ms.WriteInt32(0);//numChunks 

            ms.WriteUInt32(packageSource);

            if (Game == Mod.MEGame.ME2 || Game == Mod.MEGame.ME1)
            {
                ms.WriteInt32(0);
            }

            if (Game == Mod.MEGame.ME3 || Game == Mod.MEGame.ME2)
            {
                ms.WriteInt32(0);//empty additionalPackagesToCook array
            }
        }

        public List<TalkFileME1> LocalTalkFiles { get; } = new List<TalkFileME1>();
        public object StorageTypes { get; private set; }

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
            else if (export.IsTexture())
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
            //update offsets for pcc-stored mips in Textures
            else if (export.IsTexture())
            {
                int baseOffset = newDataOffset + export.propsEnd();
                MemoryStream binData = new MemoryStream(export.getBinaryData());
                for (int i = binData.ReadInt32(); i > 0 && binData.Position < binData.Length; i--)
                {
                    if (binData.ReadInt32() == 0) //pcc-stored
                    {
                        int uncompressedSize = binData.ReadInt32();
                        binData.Seek(4, SeekOrigin.Current); //skip compressed size
                        binData.WriteInt32(baseOffset + (int)binData.Position + 4);//update offset
                        binData.Seek(uncompressedSize + 8, SeekOrigin.Current); //skip texture and width + height values
                    }
                    else
                    {
                        binData.Seek(20, SeekOrigin.Current);//skip whole rest of mip definition
                    }
                }
                export.setBinaryData(binData.ToArray());
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
                    int bulkDataSize;
                    switch (lightMapType)
                    {
                        case 1:
                            bin.Skip(4 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(12 * 3 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            break;
                        case 2:
                            bin.Skip((16) * 3 + 16);
                            break;
                        case 3:
                            bin.Skip(8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(24);
                            break;
                        case 4:
                        case 6:
                            bin.Skip(124);
                            break;
                        case 5:
                            bin.Skip(4 + 8);
                            bulkDataSize = bin.ReadInt32();
                            bin.WriteInt32(baseOffset + (int)bin.Position + 4);
                            bin.Skip(bulkDataSize);
                            bin.Skip(12);
                            break;
                    }
                }
            }
        }
    }
}