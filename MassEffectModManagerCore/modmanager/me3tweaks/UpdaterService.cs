using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using MassEffectModManager;
using MassEffectModManagerCore.modmanager.helpers;
using Newtonsoft.Json;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    public partial class OnlineContent
    {
        private const string UpdaterEndpoint = "https://me3tweaks.com/mods/getlatest_batch";

        public static List<ModUpdateInfo> CheckForModUpdates(List<Mod> modsToCheck, bool forceRecheck)
        {
            string updateFinalRequest = UpdaterEndpoint;
            bool first = true;
            foreach (var mod in modsToCheck)
            {
                if (mod.ModModMakerID > 0)
                {
                    if (first)
                    {
                        updateFinalRequest += "?";
                        first = false;
                    }
                    else
                    {
                        updateFinalRequest += "&";
                    }

                    //Modmaker style
                    updateFinalRequest += "modmakerupdatecode[]=" + mod.ModModMakerID;

                }
                else if (mod.ModClassicUpdateCode > 0)
                {
                    //Classic style
                    if (first)
                    {
                        updateFinalRequest += "?";
                        first = false;
                    }
                    else
                    {
                        updateFinalRequest += "&";
                    }

                    updateFinalRequest += "classicupdatecode[]=" + mod.ModClassicUpdateCode;
                }
            }

            using var wc = new System.Net.WebClient();
            string updatexml = wc.DownloadStringAwareOfEncoding(updateFinalRequest);
            Debug.WriteLine(updatexml);

            XElement rootElement = XElement.Parse(updatexml);
            var modUpdateInfos = (from e in rootElement.Elements("mod")
                                  select new ModUpdateInfo
                                  {
                                      changelog = (string)e.Attribute("changelog"),
                                      version = (double)e.Attribute("version"),
                                      updatecode = (int)e.Attribute("updatecode"),
                                      serverfolder = (string)e.Attribute("folder"),
                                      sourceFiles = (from f in e.Elements("sourcefile")
                                                     select new SourceFile
                                                     {
                                                         lzmahash = (string)f.Attribute("lzmahash"),
                                                         hash = (string)f.Attribute("hash"),
                                                         size = (int)f.Attribute("size"),
                                                         lzmasize = (int)f.Attribute("lzmasize"),
                                                         relativefilepath = f.Value,
                                                         timestamp = (Int64?)f.Attribute("timestamp") ?? (Int64)0
                                                     }).ToList(),
                                  }).ToList();
            foreach (var modUpdateInfo in modUpdateInfos)
            {
                //Calculate update information
                var matchingMod = modsToCheck.FirstOrDefault(x => x.ModClassicUpdateCode == modUpdateInfo.updatecode);
                if (matchingMod != null && (forceRecheck || matchingMod.ParsedModVersion < modUpdateInfo.version))
                {
                    string modBasepath = matchingMod.ModPath;
                    foreach (var serverFile in modUpdateInfo.sourceFiles)
                    {
                        var localFile = Path.Combine(modBasepath, serverFile.relativefilepath);
                        if (File.Exists(localFile))
                        {
                            var info = new FileInfo(localFile);
                            if (info.Length != serverFile.size)
                            {
                                modUpdateInfo.applicableUpdates.Add(serverFile);
                            }
                            else
                            {
                                //Check hash
                                var md5 = Utilities.CalculateMD5(localFile);
                                if (md5 != serverFile.hash)
                                {
                                    modUpdateInfo.applicableUpdates.Add(serverFile);
                                }
                            }
                        }
                        else
                        {
                            modUpdateInfo.applicableUpdates.Add(serverFile);
                        }
                    }

                    //Files to remove calculation
                    var modFiles = Directory.GetFiles(modBasepath, "*", SearchOption.AllDirectories).Select(x => x.Substring(modBasepath.Length + 1).ToLowerInvariant()).ToList();
                    modUpdateInfo.filesToDelete = modFiles.Except(modUpdateInfo.sourceFiles.Select(x => x.relativefilepath.ToLower())).ToList();
                }

            }


            return modUpdateInfos;
        }

        public static bool UpdateMod(Mod mod, ModUpdateInfo updateInfo, Action<string> progressUpdateCallback)
        {
            string modPath = mod.ModPath;
            Parallel.ForEach(updateInfo.applicableUpdates, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (sourcefile) =>
               {

               });

            return true;
        }

        public class ModUpdateInfo
        {
            public List<SourceFile> sourceFiles;
            public string changelog;
            public string serverfolder;
            public int updatecode;
            public double version;
            public List<SourceFile> applicableUpdates = new List<SourceFile>();
            public List<string> filesToDelete = new List<string>();
        }

        public class SourceFile
        {
            public string lzmahash;
            public string relativefilepath;
            public int lzmasize;
            public int size;
            public string hash;
            public Int64 timestamp;
            public SourceFile() { }
        }
    }
}
