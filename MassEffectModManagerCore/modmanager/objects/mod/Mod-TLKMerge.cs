using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using ME3TweaksCore.ME3Tweaks.ModManager;
using ME3TweaksCore.Services.Shared.BasegameFileIdentification;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.me3tweakscoreextended;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.tlk;
using System.Diagnostics;
using System.Xml.Linq;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    public partial class Mod
    {
        /// <summary>
        /// List of all available option keys for TLK merge (LE1). This is only used when the source files are on disk (which will be the case during development of the mod)
        /// </summary>
        public List<string> LE1TLKMergeAllOptionKeys;

        /// <summary>
        /// List of chosen option keys for TLK merge (LE1).
        /// </summary>
        public List<string> LE1TLKMergeChosenOptionKeys;

        /// <summary>
        /// Coalesces the TLK merges into groups by filename.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, List<string>> CoalesceTLKMergeFiles(IReadOnlyList<string> allFilenames, CompressedTLKMergeData compressTlkMergeData)
        {
            // Input values can be null.
            if (allFilenames == null && compressTlkMergeData == null)
                throw new Exception(@"CoalesceTLKMergeFiles() must have a non null parameter!");

            if (allFilenames == null) // will be null if loading from compressed data
            {
                // The guard at start of method will ensure compressed data is never null
                if (ModDescTargetVersion >= ModDescConsts.MODDESC_VERSION_9_0)
                {
                    // Mod Manager 9: Filter files based on option keys

                    // Verify option keys first
                    compressTlkMergeData.VerifyOptionKeys(this);
                    allFilenames = compressTlkMergeData.GetFileListing(LE1TLKMergeChosenOptionKeys);
                }
                else
                {
                    // Mod Manager 8.x and below
                    allFilenames = compressTlkMergeData.GetFileListing();
                }
            }
            else if (LE1TLKMergeAllOptionKeys != null) // Has filtering, disk based
            {

                List<string> filteredFiles = new List<string>();
                foreach (var f in allFilenames)
                {
                    if (f.Contains('/') || f.Contains('\\'))
                    {
                        var parentName = Directory.GetParent(f).Name;
                        if (parentName != Mod.Game1EmbeddedTlkFolderName)
                        {
                            if (LE1TLKMergeChosenOptionKeys == null || LE1TLKMergeChosenOptionKeys.Contains(parentName))
                            {
                                filteredFiles.Add(f);
                            }
                        }
                    }
                    else
                    {
                        filteredFiles.Add(f);
                    }
                }

                allFilenames = filteredFiles;
            }


            // Map of package name -> TLK filenames to install into it
            var dict = new Dictionary<string, List<string>>();

            // Build map of files
            foreach (var tlkM in allFilenames)
            {
                var subStr = Path.GetFileName(tlkM);
                var packageName = subStr.Substring(0, subStr.IndexOf('.'));
                List<string> l;
                if (!dict.TryGetValue(packageName, out l))
                {
                    l = new List<string>();
                    dict[packageName] = l;
                }
                l.Add(tlkM); // Use full tlk path here.
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
        /// Returns list of files to modify mapped to the list of TLK xml filenames to read to use as an update source of that file
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, List<string>> PrepareTLKMerge(out CompressedTLKMergeData compressedTlkData)
        {
            compressedTlkData = null;
            List<string> allTlkFilenames = null;
            if (ModDescTargetVersion >= ModDescConsts.MODDESC_VERSION_8_0)
            {
                // ModDesc 8 mods can use this feature
                compressedTlkData = ReadCompressedTlkMergeFile();
            }

            // Legacy and fallback: Use raw files
            if (compressedTlkData == null)
            {
                allTlkFilenames = InstallationJobs.Where(x => x.Game1TLKXmls != null).SelectMany(x => x.Game1TLKXmls).ToList();
            }

            return CoalesceTLKMergeFiles(allTlkFilenames, compressedTlkData);
        }

        /// <summary>
        /// Installs a TLK merge. Returns null if OK, otherwise returns an error string.
        /// </summary>
        /// <param name="tlkXmlName"></param>
        /// <param name="gameFileMapping"></param>
        /// <returns></returns>
        public string InstallTLKMerge(string tlkXmlName, CompressedTLKMergeData compressedTlkMergeData, Dictionary<string, string> gameFileMapping, bool savePackage, PackageCache cache, GameTarget target, Mod modBeingInstalled, Action<BasegameFileRecord> addBasegameRecord)
        {
            // Need to load file into memory
            string xmlContents;

            if (compressedTlkMergeData != null)
            {
                // Load from the compressed TLK merge data file
                var loadInfo = compressedTlkMergeData.GetFileInfo(tlkXmlName);
                var compressedData = compressedTlkMergeData.LoadedCompressedData.AsSpan(loadInfo.dataStartOffset, loadInfo.compressedSize);
                compressedTlkMergeData.DecompressTextFile(loadInfo, compressedData);
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
                var tlkPackageStr = Path.GetFileName(tlkXmlName);
                var packageName = tlkPackageStr.Substring(0, tlkPackageStr.IndexOf('.'));
                var exportPath = Path.GetFileNameWithoutExtension(tlkPackageStr.Substring(packageName.Length + 1));

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
                    if (talkFile == null)
                    {
                        // It was not loaded with the package due to default load settings
                        talkFile = new ME1TalkFile(exp);
                    }

                    var strRefs = talkFile.StringRefs.ToList();
                    int numDone = 0;
                    if (strRefs.Any())
                    {
                        M3Log.Information($@"Installing {tlkXmlName} into {Path.GetRelativePath(target.TargetPath, package.FilePath)} {exportPath}", Settings.LogModInstallation);
                    }
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
                        addBasegameRecord?.Invoke(new M3BasegameFileRecord(package.FilePath, (int)new FileInfo(package.FilePath).Length, target, modBeingInstalled));
                        cache.DropPackageFromCache(packagePath); // we are not doing more operations on this file so drop it out
                    }
                }
            }
            // Logic subject to change in future!
            return null;
        }
    }
}
