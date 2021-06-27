using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;
using MassEffectModManagerCore.modmanager.localizations;
using Microsoft.AppCenter;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects.mod
{
    public partial class Mod
    {
        /// <summary>
        /// Installs a TLK merge. Returns null if OK, otherwise returns an error string.
        /// </summary>
        /// <param name="tlkXmlName"></param>
        /// <param name="gameFileMapping"></param>
        /// <returns></returns>
        public string InstallTLKMerge(string tlkXmlName, Dictionary<string, string> gameFileMapping)
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
                    var package = MEPackageHandler.OpenMEPackage(packagePath);
                    var exp = package.FindExport(exportPath);
                    if (exp == null)
                    {
                        // WRONGLY NAMED EXPORT!
                        Log.Error($@"Could not find export in package {packagePath} for TLK merge: {exportPath}");
                        return M3L.GetString(M3L.string_interp_tlkmerge_couldNotFindExportInPackage, packagePath, exportPath);
                    }

                    var talkFile = package.LocalTalkFiles.FirstOrDefault(x => x.UIndex == exp.UIndex);
                    var strRefs = talkFile.StringRefs.ToList();
                    foreach (var node in stringNodes)
                    {
                        var tlkId = int.Parse(node.Element(@"id").Value);
                        var flags = int.Parse(node.Element(@"flags").Value);
                        var data = node.Element(@"data").Value;

                        ME1TalkFile.TLKStringRef strRef = talkFile.StringRefs.FirstOrDefault(x => x.StringID == tlkId);
                        if (strRef == null)
                        {
                            CLog.Information($@"Adding new TLK id {tlkId}", Settings.LogModInstallation);
                            strRefs.Add(new ME1TalkFile.TLKStringRef(tlkId, flags, data));
                        }
                        else
                        {
                            CLog.Information($@"Updating TLK id {tlkId}", Settings.LogModInstallation);
                            strRef.Data = data;
                            strRef.Flags = flags;
                        }
                    }

                    HuffmanCompression huff = new HuffmanCompression();
                    huff.LoadInputData(strRefs);
                    huff.serializeTalkfileToExport(exp);
                    if (package.IsModified)
                        package.Save();
                    Log.Information($@"Saving TLKMerged package {packagePath}");
                }
            }
            // Logic subject to change in future!
            return null;
        }
    }
}
