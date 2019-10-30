using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager;
using MassEffectModManagerCore.modmanager.helpers;
using Buffer = System.Buffer;
using MassEffectModManagerCore.modmanager.objects;

namespace MassEffectModManagerCore.gamefileformats.unreal
{


    public class Texture2D
    {
        public List<Texture2DMipInfo> Mips { get; }
        public readonly bool NeverStream;
        public readonly ExportEntry Export;
        public readonly string TextureFormat;
        public Guid TextureGuid;

        public Texture2D(ExportEntry export)
        {
            Export = export;
            PropertyCollection properties = export.GetProperties();
            TextureFormat = properties.GetProp<EnumProperty>("Format").Value.Name;
            var cache = properties.GetProp<NameProperty>("TextureFileCacheName");

            NeverStream = properties.GetProp<BoolProperty>("NeverStream") ?? false;
            Mips = GetTexture2DMipInfos(export, cache?.Value);
            if (Export.Game != Mod.MEGame.ME1)
            {
                TextureGuid = new Guid(Export.Data.Skip(Export.Data.Length - 16).Take(16).ToArray());
            }
        }

        //Not used in Mod Manager
        //public static uint GetTextureCRC(ExportEntry export)
        //{
        //    PropertyCollection properties = export.GetProperties();
        //    var format = properties.GetProp<EnumProperty>("Format");
        //    var cache = properties.GetProp<NameProperty>("TextureFileCacheName");
        //    List<Texture2DMipInfo> mips = Texture2D.GetTexture2DMipInfos(export, cache != null ? cache.Value : null);
        //    var topmip = mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
        //    return Texture2D.GetMipCRC(topmip, format.Value);
        //}

        public void RemoveEmptyMipsFromMipList()
        {
            Mips.RemoveAll(x => x.storageType == StorageTypes.empty);
        }

        internal Texture2DMipInfo GetTopMip()
        {
            return Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
        }

        public static StorageTypes GetTopMipStorageType(ExportEntry exportEntry)
        {
            MemoryStream ms = new MemoryStream(exportEntry.Data);
            ms.Seek(exportEntry.propsEnd(), SeekOrigin.Begin);
            if (exportEntry.FileRef.Game != Mod.MEGame.ME3)
            {
                ms.Skip(4);//BulkDataFlags
                ms.Skip(4);//ElementCount
                int bulkDataSize = ms.ReadInt32();
                ms.Seek(4, SeekOrigin.Current); // position in the package
                ms.Skip(bulkDataSize); //skips over thumbnail png, if it exists
            }

            var mips = new List<Texture2DMipInfo>();
            int numMipMaps = ms.ReadInt32();
            for (int l = 0; l < numMipMaps; l++)
            {
                var storageType = (StorageTypes)ms.ReadInt32();
                if (storageType != StorageTypes.empty) return storageType;
                var uncompressedSize = ms.ReadInt32();
                var compressedSize = ms.ReadInt32();
                var externalOffset = ms.ReadInt32();
                var localExportOffset = (int)ms.Position;
            }
            return StorageTypes.empty;
        }

        public static List<Texture2DMipInfo> GetTexture2DMipInfos(ExportEntry exportEntry, string cacheName)
        {
            MemoryStream ms = new MemoryStream(exportEntry.Data);
            ms.Seek(exportEntry.propsEnd(), SeekOrigin.Begin);
            if (exportEntry.FileRef.Game != Mod.MEGame.ME3)
            {
                ms.Skip(4);//BulkDataFlags
                ms.Skip(4);//ElementCount
                int bulkDataSize = ms.ReadInt32();
                ms.Seek(4, SeekOrigin.Current); // position in the package
                ms.Skip(bulkDataSize); //skips over thumbnail png, if it exists
            }

            var mips = new List<Texture2DMipInfo>();
            int numMipMaps = ms.ReadInt32();
            for (int l = 0; l < numMipMaps; l++)
            {
                Texture2DMipInfo mip = new Texture2DMipInfo
                {
                    Export = exportEntry,
                    index = l,
                    storageType = (StorageTypes)ms.ReadInt32(),
                    uncompressedSize = ms.ReadInt32(),
                    compressedSize = ms.ReadInt32(),
                    externalOffset = ms.ReadInt32(),
                    localExportOffset = (int)ms.Position,
                    TextureCacheName = cacheName //If this is ME1, this will simply be ignored in the setter
                };
                switch (mip.storageType)
                {
                    case StorageTypes.pccUnc:
                        ms.Seek(mip.uncompressedSize, SeekOrigin.Current);
                        break;
                    case StorageTypes.pccLZO:
                    case StorageTypes.pccZlib:
                        ms.Seek(mip.compressedSize, SeekOrigin.Current);
                        break;
                }

                mip.width = ms.ReadInt32();
                mip.height = ms.ReadInt32();
                if (mip.width == 4 && mips.Exists(m => m.width == mip.width))
                    mip.width = mips.Last().width / 2;
                if (mip.height == 4 && mips.Exists(m => m.height == mip.height))
                    mip.height = mips.Last().height / 2;
                if (mip.width == 0)
                    mip.width = 1;
                if (mip.height == 0)
                    mip.height = 1;
                mips.Add(mip);
            }

            return mips;
        }

        public byte[] GetImageBytesForMip(Texture2DMipInfo info, GameTarget target, bool useLowerMipsIfTFCMissing)
        {
            byte[] imageBytes = null;
            try
            {
                imageBytes = GetTextureData(info, target);
            }
            catch (FileNotFoundException e)
            {
                if (useLowerMipsIfTFCMissing)
                {
                    Debug.WriteLine("External cache not found. Defaulting to internal mips.");
                    //External archive not found - using built in mips (will be hideous, but better than nothing)
                    info = Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                    if (info != null)
                    {
                        imageBytes = GetTextureData(info, target);
                    }
                }
                else
                {
                    throw e; //rethrow
                }
            }
            if (imageBytes == null)
            {
                throw new Exception("Could not fetch texture data for texture " + info?.Export.ObjectName);
            }
            return imageBytes;
        }

        internal byte[] SerializeNewData()
        {
            MemoryStream ms = new MemoryStream();
            if (Export.FileRef.Game != Mod.MEGame.ME3)
            {
                for (int i = 0; i < 12; i++)
                    ms.WriteByte(0); //12 0s
                for (int i = 0; i < 4; i++)
                    ms.WriteByte(0); //position in the package. will be updated later
            }

            ms.WriteInt32(Mips.Count);
            foreach (var mip in Mips)
            {
                ms.WriteUInt32((uint)mip.storageType);
                ms.WriteInt32(mip.uncompressedSize);
                ms.WriteInt32(mip.compressedSize);
                ms.WriteInt32(mip.externalOffset);
                if (mip.storageType == StorageTypes.pccUnc ||
                    mip.storageType == StorageTypes.pccLZO ||
                    mip.storageType == StorageTypes.pccZlib)
                {
                    ms.Write(mip.newDataForSerializing, 0, mip.newDataForSerializing.Length);
                }
                ms.WriteInt32(mip.width);
                ms.WriteInt32(mip.height);
            }
            ms.WriteInt32(0);
            if (Export.Game != Mod.MEGame.ME1)
            {
                ms.WriteValueGuid(TextureGuid);
            }
            return ms.ToArray();
        }

        internal void ReplaceMips(List<Texture2DMipInfo> mipmaps)
        {
            Mips.Clear();
            Mips.AddRange(mipmaps);
        }


        public static byte[] GetTextureData(Texture2DMipInfo mipToLoad, GameTarget target, bool decompress = true)
        {
            var imagebytes = new byte[decompress ? mipToLoad.uncompressedSize : mipToLoad.compressedSize];
            //Debug.WriteLine("getting texture data for " + mipToLoad.Export.FullPath);
            if (mipToLoad.storageType == StorageTypes.pccUnc)
            {
                Buffer.BlockCopy(mipToLoad.Export.Data, mipToLoad.localExportOffset, imagebytes, 0, mipToLoad.uncompressedSize);
            }
            else if (mipToLoad.storageType == StorageTypes.pccLZO || mipToLoad.storageType == StorageTypes.pccZlib)
            {
                if (decompress)
                {
                    try
                    {
                        TextureCompression.DecompressTexture(imagebytes,
                                                             new MemoryStream(mipToLoad.Export.Data, mipToLoad.localExportOffset, mipToLoad.compressedSize),
                                                             mipToLoad.storageType, mipToLoad.uncompressedSize, mipToLoad.compressedSize);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"{e.Message}\nStorageType: {mipToLoad.storageType}\n");
                    }
                }
                else
                {
                    Buffer.BlockCopy(mipToLoad.Export.Data, mipToLoad.localExportOffset, imagebytes, 0, mipToLoad.compressedSize);
                }
            }
            else if (mipToLoad.storageType == StorageTypes.extUnc || mipToLoad.storageType == StorageTypes.extLZO || mipToLoad.storageType == StorageTypes.extZlib)
            {
                string filename = null;
                List<string> loadedFiles = MEDirectories.EnumerateGameFiles(target.Game, target.TargetPath);
                if (mipToLoad.Export.Game == Mod.MEGame.ME1)
                {
                    var fullPath = loadedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(mipToLoad.TextureCacheName, StringComparison.InvariantCultureIgnoreCase));
                    if (fullPath != null)
                    {
                        filename = fullPath;
                    }
                    else
                    {
                        throw new FileNotFoundException($"Externally referenced texture file not found in game: {mipToLoad.TextureCacheName}.");
                    }
                }
                else
                {
                    string archive = mipToLoad.TextureCacheName + ".tfc";
                    var localDirectoryTFCPath = Path.Combine(Path.GetDirectoryName(mipToLoad.Export.FileRef.FilePath), archive);
                    if (File.Exists(localDirectoryTFCPath))
                    {
                        filename = localDirectoryTFCPath;
                    }
                    else
                    {
                        var tfcs = loadedFiles.Where(x => x.EndsWith(".tfc")).ToList();

                        var fullPath = loadedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(archive, StringComparison.InvariantCultureIgnoreCase));
                        if (fullPath != null)
                        {
                            filename = fullPath;
                        }
                        else
                        {
                            throw new FileNotFoundException($"Externally referenced texture cache not found: {archive}.");
                        }
                    }
                }

                //exceptions above will prevent filename from being null here

                try
                {
                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        try
                        {
                            fs.Seek(mipToLoad.externalOffset, SeekOrigin.Begin);
                            if (mipToLoad.storageType == StorageTypes.extLZO || mipToLoad.storageType == StorageTypes.extZlib)
                            {
                                if (decompress)
                                {
                                    using (MemoryStream tmpStream = new MemoryStream(fs.ReadToBuffer(mipToLoad.compressedSize)))
                                    {
                                        try
                                        {
                                            TextureCompression.DecompressTexture(imagebytes, tmpStream, mipToLoad.storageType, mipToLoad.uncompressedSize, mipToLoad.compressedSize);
                                        }
                                        catch (Exception e)
                                        {
                                            throw new Exception(e.Message + "\n" + "File: " + filename + "\n" +
                                                                "StorageType: " + mipToLoad.storageType + "\n" +
                                                                "External file offset: " + mipToLoad.externalOffset);
                                        }
                                    }
                                }
                                else
                                {
                                    fs.Read(imagebytes, 0, mipToLoad.compressedSize);
                                }
                            }
                            else
                            {
                                fs.Read(imagebytes, 0, mipToLoad.uncompressedSize);
                            }
                        }
                        catch (Exception e)
                        {
                            throw new Exception(e.Message + "\n" + "File: " + filename + "\n" +
                                "StorageType: " + mipToLoad.storageType + "\n" +
                                "External file offset: " + mipToLoad.externalOffset);
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + "\n" + "File: " + filename + "\n" +
                        "StorageType: " + mipToLoad.storageType + "\n" +
                        "External file offset: " + mipToLoad.externalOffset);
                }
            }
            return imagebytes;
        }

        //Not used in Mod Manager
        //public static uint GetMipCRC(Texture2DMipInfo mip, string textureFormat)
        //{
        //    byte[] data = GetTextureData(mip);
        //    if (data == null) return 0;
        //    if (textureFormat == "PF_NormalMap_HQ")
        //    {
        //        // only ME1 and ME2
        //        return (uint)~ParallelCRC.Compute(data, 0, data.Length / 2);
        //    }
        //    return (uint)~ParallelCRC.Compute(data);
        //}
    }

    [DebuggerDisplay("Texture2DMipInfo for {Export.ObjectName.Instanced} | {width}x{height} | {storageType}")]
    public class Texture2DMipInfo
    {
        public ExportEntry Export;
        public bool NeverStream; //copied from parent
        public int index;
        public int uncompressedSize;
        public int compressedSize;
        public int width;
        public int height;
        public int externalOffset;
        public int localExportOffset;
        public StorageTypes storageType;
        private string _textureCacheName;
        public byte[] newDataForSerializing;

        public string TextureCacheName
        {
            get
            {
                if (Export.Game != Mod.MEGame.ME1) return _textureCacheName; //ME2/ME3 have property specifying the name. ME1 uses package lookup

                //ME1 externally references the UPKs. I think. It doesn't load external textures from SFMs
                string baseName = Export.FileRef.FollowLink(Export.idxLink).Split('.')[0].ToUpper() + ".upk"; //get top package name

                if (storageType == StorageTypes.extLZO || storageType == StorageTypes.extZlib || storageType == StorageTypes.extUnc)
                {
                    return baseName;
                }

                //NeverStream is set if there are more than 6 mips. Some sort of design implementation of ME1 texture streaming
                if (baseName != "" && !NeverStream)
                {
                    var gameFiles = MELoadedFiles.GetFilesLoadedInGame(Mod.MEGame.ME1);
                    if (gameFiles.ContainsKey(baseName)) //I am pretty sure these will only ever resolve to UPKs...
                    {
                        return baseName;
                    }
                }

                return null;
            }
            set => _textureCacheName = value; //This isn't INotifyProperty enabled so we don't need to SetProperty this
        }

        public string MipDisplayString
        {
            get
            {
                string mipinfostring = "Mip " + index;
                mipinfostring += "\nStorage Type: ";
                mipinfostring += storageType;
                if (storageType == StorageTypes.extLZO || storageType == StorageTypes.extZlib || storageType == StorageTypes.extUnc)
                {
                    mipinfostring += "\nLocated in: ";
                    mipinfostring += TextureCacheName ?? "(NULL!)";
                }

                mipinfostring += "\nUncompressed size: ";
                mipinfostring += uncompressedSize;
                mipinfostring += "\nCompressed size: ";
                mipinfostring += compressedSize;
                mipinfostring += "\nOffset: ";
                mipinfostring += externalOffset;
                mipinfostring += "\nWidth: ";
                mipinfostring += width;
                mipinfostring += "\nHeight: ";
                mipinfostring += height;
                return mipinfostring;
            }
        }
    }
}
