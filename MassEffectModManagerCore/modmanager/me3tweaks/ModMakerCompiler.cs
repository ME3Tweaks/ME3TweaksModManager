using IniParser.Model;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using static MassEffectModManagerCore.modmanager.Mod;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    public class ModMakerCompiler
    {
        private readonly int code;

        public ModMakerCompiler(int code)
        {
            this.code = code;
        }
        public void DownloadAndCompileMod()
        {
            string cachedFilename = Path.Combine(Utilities.GetModmakerDefinitionsCache(), code + @".xml");
            if (File.Exists(cachedFilename))
            {
                //Going to compile cached item
                CompileMod(File.ReadAllText(cachedFilename));
            }
        }

        /// <summary>
        /// Compiles a mod using the specified mod definition text
        /// </summary>
        /// <param name="modxml">XML document for the mod</param>
        private void CompileMod(string modxml)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(modxml);

            var mod = GenerateLibraryModFromDocument(xmlDoc);
            compileCoalesceds(xmlDoc, mod);
        }

        private void compileCoalesceds(XmlDocument xmlDoc, Mod mod)
        {
            List<XmlNode> jobCollection = new List<XmlNode>();
            var jobs = xmlDoc.SelectNodes(@"/ModMaker/ModData/*");
            if (jobs != null)
            {
                foreach (var job in jobs)
                {
                    if (job is XmlNode node)
                    {
                        CLog.Information(@"Found coalesced modifier for DLC: " + node.Name, Settings.LogModMakerCompiler);
                        jobCollection.Add(node);
                    }
                }
            }

            //Todo: Precheck assets are available.

            Parallel.ForEach(jobCollection, new ParallelOptions { MaxDegreeOfParallelism = 4 }, (xmlChunk) => compileCoalescedChunk(xmlChunk, mod));
        }

        private void compileCoalescedChunk(XmlNode xmlChunk, Mod mod)
        {
            var chunkName = xmlChunk.Name;
            var outPath = Directory.CreateDirectory(Path.Combine(mod.ModPath, chunkName)).FullName;
        
            // Fetch vanilla asset from backup

            //
        
        }



        /// <summary>
        /// Generates the initial mod folder and mod
        /// </summary>
        /// <param name="xmlDoc">mod document</param>
        /// <returns>Mod object</returns>
        private Mod GenerateLibraryModFromDocument(XmlDocument xmlDoc)
        {
            var modName = xmlDoc.SelectSingleNode(@"/ModMaker/ModInfo/Name").InnerText;
            Log.Information(@"Compiling mod: " + modName);

            var modDev = xmlDoc.SelectSingleNode(@"/ModMaker/ModInfo/Author").InnerText;
            var modVersion = xmlDoc.SelectSingleNode(@"/ModMaker/ModInfo/Revision").InnerText;
            var modDescription = xmlDoc.SelectSingleNode(@"/ModMaker/ModInfo/Description").InnerText;
            var modmakerServerVer = xmlDoc.SelectSingleNode(@"/ModMaker/ModInfo/ModMakerVersion").InnerText;

            //Write mod ini
            IniData ini = new IniData();
            ini[@"ModManager"][@"cmmver"] = App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture); //prevent commas
            ini[@"ModInfo"][@"game"] = @"ME3";
            ini[@"ModInfo"][@"modname"] = modName;
            ini[@"ModInfo"][@"moddev"] = modDev;
            ini[@"ModInfo"][@"moddesc"] = modDescription;
            ini[@"ModInfo"][@"modver"] = modVersion;
            ini[@"ModInfo"][@"modmakercode"] = code.ToString();
            ini[@"ModInfo"][@"compiledagainst"] = modmakerServerVer;
            ini[@"ModInfo"][@"modsite"] = @"https://me3tweaks.com/modmaker/mods/" + code;

            var outputDir = Path.Combine(Utilities.GetME3ModsDirectory(), Utilities.SanitizePath(modName));
            if (Directory.Exists(outputDir))
            {
                Utilities.DeleteFilesAndFoldersRecursively(outputDir);
            }
            //apparently system is too fast to respond
            Thread.Sleep(100);
            Directory.CreateDirectory(outputDir);

            var moddescPath = Path.Combine(outputDir, @"moddesc.ini");
            File.WriteAllText(moddescPath, ini.ToString());

            //Generate and load mod - it will be invalid as it has no jobs yet.
            Mod m = new Mod(moddescPath, MEGame.ME3);
            return m;
        }
    }
}
