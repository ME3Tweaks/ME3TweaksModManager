using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.me3tweakscoreextended;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    public partial class Mod
    {
        /// <summary>
        /// Coaleceses the TLK merges into groups by filename.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, List<string>> CoalesceTLKMergeFiles(List<string> filenames)
        {
            var dict = new Dictionary<string, List<string>>();

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
        /// Installs a TLK merge. Returns null if OK, otherwise returns an error string.
        /// </summary>
        /// <param name="tlkXmlName"></param>
        /// <param name="gameFileMapping"></param>
        /// <returns></returns>
        public string InstallTLKMerge(string tlkXmlName, Dictionary<string, string> gameFileMapping, bool savePackage, PackageCache cache, GameTargetWPF target, Mod modBeingInstalled, Action<BasegameFileRecord> addCloudDBEntry)
        {
            // Need to load file into memory
            string xmlContents;
            var sourcePath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, Mod.Game1EmbeddedTlkFolderName, tlkXmlName);
            if (Archive != null)
            {
                var ms = new MemoryStream();
                Archive.ExtractFile(sourcePath, ms);
                ms.Position = 0;
                xmlContents = new StreamReader(ms).ReadToEnd();
            }
            else
            {
                xmlContents = File.ReadAllText(sourcePath);
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
                        var flags = int.Parse(node.Element(@"flags").Value);
                        var data = node.Element(@"data").Value;

                        TLKStringRef strRef = talkFile.StringRefs.FirstOrDefault(x => x.StringID == tlkId);
                        if (strRef == null)
                        {
                            M3Log.Information($@"Adding new TLK id {tlkId}", Settings.LogModInstallation);
                            strRefs.Add(new TLKStringRef(tlkId, flags, data));
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
                            strRef.Flags = flags;
                        }

                        numDone++;
                    }

                    HuffmanCompression huff = new HuffmanCompression();
                    huff.LoadInputData(strRefs);
                    huff.serializeTalkfileToExport(exp);
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
