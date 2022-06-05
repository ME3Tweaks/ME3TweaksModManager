using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.me3tweakscoreextended;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.tlk;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    public partial class Mod
    {
        /// <summary>
        /// Coalesces the TLK merges into groups by filename.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, List<string>> CoalesceTLKMergeFiles(IReadOnlyList<string> filenames, CompressedTLKMergeData compressTlkMergeData)
        {
            // Input values can be null.
            if (filenames == null && compressTlkMergeData == null)
                throw new Exception(@"CoalesceTLKMergeFiles() must have a non null parameter!");

            var dict = new Dictionary<string, List<string>>();

            if (filenames == null)
            {
                // The guard at start of method will ensure compressed data is never null
                filenames = compressTlkMergeData.GetFileListing();
            }

            foreach (var tlkM in filenames)
            {
                var packageName = tlkM.Substring(0, tlkM.IndexOf('.'));
                List<string> l;
                if (!dict.TryGetValue(packageName, out l))
                {
                    l = new List<string>();
                    dict[packageName] = l;
                }
                l.Add(tlkM);
            }

            return dict;
        }

        /// <summary>
        /// Loads the CompressedTLKMergeData file for the mod. Returns null if one isn't found or the game this mod is for doesn't support that feature
        /// </summary>
        /// <returns></returns>
        public CompressedTLKMergeData ReadCompressedTlkMergeFile()
        {
            if (!Game.IsGame1())
                return null; // Other games don't use this feature


            if (Archive != null)
            {
                // Read from archive
                var archivePath = FilesystemInterposer.PathCombine(true, ModPath, Game1EmbeddedTlkFolderName, Game1EmbeddedTlkCompressedFilename);
                var ms = new MemoryStream();
                if (FilesystemInterposer.FileExists(archivePath, Archive))
                {
                    Archive.ExtractFile(archivePath, ms);
                    ms.Position = 0;
                    return CompressedTLKMergeData.ReadCompressedTlkMergeFile(ms, true);
                }
                else
                {
                    var diskPath = Path.Combine(ModPath, Game1EmbeddedTlkFolderName, Game1EmbeddedTlkCompressedFilename);
                    if (File.Exists(diskPath))
                    {
                        using var compressedStream = File.OpenRead(Path.Combine(ModPath, Game1EmbeddedTlkFolderName, Game1EmbeddedTlkCompressedFilename));
                        return CompressedTLKMergeData.ReadCompressedTlkMergeFile(compressedStream, true);
                    }
                }
            }
            else
            {
                // Read from disk
                var combinedDiskPath = Path.Combine(ModPath, Game1EmbeddedTlkFolderName, Game1EmbeddedTlkCompressedFilename);
                if (File.Exists(combinedDiskPath))
                {
                    using var m3zaf = File.OpenRead(combinedDiskPath);
                    return CompressedTLKMergeData.ReadCompressedTlkMergeFile(m3zaf, true);
                }
            }

            return null; // No compressed merge file was found.
        }

        /// <summary>
        /// Installs a TLK merge. Returns null if OK, otherwise returns an error string.
        /// </summary>
        /// <param name="tlkXmlName"></param>
        /// <param name="gameFileMapping"></param>
        /// <returns></returns>
        public string InstallTLKMerge(string tlkXmlName, CompressedTLKMergeData compressedTlkMergeData, Dictionary<string, string> gameFileMapping, bool savePackage, PackageCache cache, GameTargetWPF target, Mod modBeingInstalled, Action<BasegameFileRecord> addCloudDBEntry)
        {
            // Need to load file into memory
            string xmlContents;

            if (compressedTlkMergeData != null)
            {
                // Load from the compressed TLK merge data file
                var loadInfo = compressedTlkMergeData.GetFileInfo(tlkXmlName);
                var compressedData = compressedTlkMergeData.LoadedCompressedData.AsSpan(loadInfo.dataStartOffset, loadInfo.compressedSize);
                var decomp = new byte[loadInfo.decompressedSize];
                LZMA.Decompress(compressedData, decomp);
                xmlContents = new StreamReader(new MemoryStream(decomp)).ReadToEnd();
            }
            else
            {
                var sourcePath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, Mod.Game1EmbeddedTlkFolderName, tlkXmlName);
                if (Archive != null)
                {
                    var ms = new MemoryStream();
                    // non-compressed
                    Archive.ExtractFile(sourcePath, ms);
                    ms.Position = 0;
                    xmlContents = new StreamReader(ms).ReadToEnd();
                }
                else
                {
                    // Read from disk
                    xmlContents = File.ReadAllText(sourcePath);
                }
            }

            var tlkDoc = XDocument.Parse(xmlContents);
            var stringNodes = tlkDoc.Root.Descendants(@"string").ToList();
            if (stringNodes.Any())
            {
                // Open package
                var packageName = tlkXmlName.Substring(0, tlkXmlName.IndexOf('.'));
                var exportPath = Path.GetFileNameWithoutExtension(tlkXmlName.Substring(packageName.Length + 1));

                string packagePath = null; ;

                if (Game is MEGame.LE1)
                {
                    gameFileMapping.TryGetValue(packageName + @".pcc", out packagePath);
                }
                else if (Game is MEGame.ME1)
                {
                    gameFileMapping.TryGetValue(packageName + @".sfm", out packagePath);
                    if (packagePath == null)
                        gameFileMapping.TryGetValue(packageName + @".u", out packagePath);
                    if (packagePath == null)
                        gameFileMapping.TryGetValue(packageName + @".upk", out packagePath);
                }

                if (packagePath != null)
                {
                    var package = cache.GetCachedPackage(packagePath);
                    var exp = package.FindExport(exportPath);
                    if (exp == null)
                    {
                        // WRONGLY NAMED EXPORT!
                        M3Log.Error($@"Could not find export in package {packagePath} for TLK merge: {exportPath}");
                        return M3L.GetString(M3L.string_interp_tlkmerge_couldNotFindExportInPackage, packagePath, exportPath);
                    }

                    var talkFile = package.LocalTalkFiles.FirstOrDefault(x => x.UIndex == exp.UIndex);
                    var strRefs = talkFile.StringRefs.ToList();
                    int numDone = 0;
                    foreach (var node in stringNodes)
                    {
                        var tlkId = int.Parse(node.Element(@"id").Value);
                        var data = node.Element(@"data").Value;

                        TLKStringRef strRef = talkFile.StringRefs.FirstOrDefault(x => x.StringID == tlkId);
                        if (strRef == null)
                        {
                            M3Log.Information($@"Adding new TLK id {tlkId}", Settings.LogModInstallation);
                            strRefs.Add(new TLKStringRef(tlkId, data));
                        }
                        else
                        {
                            if (numDone <= 25)
                            {
                                //CLog.Information($@"Updating TLK id {tlkId}", Settings.LogModInstallation);
                                if (numDone == 25)
                                {
                                    //CLog.Information($@"Remaining updates will not be logged for this TLK to trim log size...", Settings.LogModInstallation);
                                }
                            }
                            strRef.Data = data;
                        }

                        numDone++;
                    }

                    HuffmanCompression huff = new HuffmanCompression();
                    huff.LoadInputData(strRefs);
                    huff.SerializeTalkfileToExport(exp);
                    if (savePackage && package.IsModified)
                    {
                        M3Log.Information($@"Saving TLKMerged package {packagePath}");
                        package.Save();
                        addCloudDBEntry?.Invoke(new M3BasegameFileRecord(package.FilePath, (int)new FileInfo(package.FilePath).Length, target, modBeingInstalled));
                        cache.DropPackageFromCache(packagePath); // we are not doing more operations on this file so drop it out
                    }
                }
            }
            // Logic subject to change in future!
            return null;
        }
    }
}
