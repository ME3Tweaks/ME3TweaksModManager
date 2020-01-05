using IniParser.Model;
using MassEffectModManagerCore.modmanager.helpers;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
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
            var xmlDoc = XDocument.Parse(modxml);

            var mod = GenerateLibraryModFromDocument(xmlDoc);
            compileCoalesceds(xmlDoc, mod);
        }

        private void compileCoalesceds(XDocument xmlDoc, Mod mod)
        {
            List<XElement> jobCollection = new List<XElement>();
            var jobs = xmlDoc.XPathSelectElements(@"/ModMaker/ModData/*");
            if (jobs != null)
            {
                foreach (var job in jobs)
                {
                    if (job is XElement node)
                    {
                        CLog.Information(@"Found coalesced modifier for DLC: " + node.Name, Settings.LogModMakerCompiler);
                        jobCollection.Add(node);
                    }
                }
            }

            //Todo: Precheck assets are available.

            Parallel.ForEach(jobCollection, new ParallelOptions { MaxDegreeOfParallelism = 1 }, (xmlChunk) => compileCoalescedChunk(xmlChunk, mod));
        }

        private bool compileCoalescedChunk(XElement xmlChunk, Mod mod)
        {
            var chunkName = xmlChunk.Name.LocalName;
            string loggingPrefix = $"ModMaker Compiler [{chunkName}]";
            //var header = Enum.Parse(typeof(ModJob.JobHeader), chunkName);
            //string dlcFoldername = ModJob.GetHeadersToDLCNamesMap(MEGame.ME3)[header];
            var outPath = Directory.CreateDirectory(Path.Combine(mod.ModPath, chunkName)).FullName;
            Debug.WriteLine("Compiling chunk: " + chunkName);

            //File fetch
            Dictionary<string, string> coalescedFilemapping = null;
            if (chunkName == "BASEGAME")
            {
                var coalPath = Path.Combine(Utilities.GetGameBackupPath(MEGame.ME3), "BioGame", "CookedPCConsole", "Coalesced.bin");
                if (File.Exists(coalPath))
                {
                    using FileStream fs = new FileStream(coalPath, FileMode.Open);
                    coalescedFilemapping = MassEffect3.Coalesce.Converter.DecompileToMemory(fs);
                }
                else
                {
                    Log.Error("Could not get file data for coalesced chunk BASEGAME as Coalesced.bin file was missing");
                    return false;
                }
            }
            else if (chunkName == "BALANCE_CHANGES")
            {
                // do later
                // fetch from cache
            }
            else
            {
                var dlcFolderName = chunkNameToFoldername(chunkName);
                var coalescedData = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcFolderName, $"Default_{dlcFolderName}.bin");
                if (coalescedData != null)
                {
                    coalescedFilemapping = MassEffect3.Coalesce.Converter.DecompileToMemory(coalescedData);
                }
                else
                {
                    Log.Error("Could not get file data for coalesced chunk: " + chunkName);
                    return false;
                }
            }

            if (coalescedFilemapping != null)
            {
                // get filenames for chunk
                foreach (var fileNode in xmlChunk.Elements())
                {
                    Debug.WriteLine($"{loggingPrefix} {fileNode.Name.LocalName}");
                    var matchingCoalFile = coalescedFilemapping[fileNode.Name + ".xml"];
                    var coalFileDoc = XDocument.Parse(matchingCoalFile);
                    string updatedDocumentText = compileCoalescedChunkFile(coalFileDoc, fileNode, $"{loggingPrefix}[{fileNode.Name}]: ");
                }
            }

            return true;
        }

        private const string OP_ADDITION = "addition";
        private const string OP_SUBTRACTION = "subtraction";
        private const string OP_ASSIGNMENT = "assignment";

        /// <summary>
        /// Compile's a chunk's subfile. e.g. DLC_CON_MP5's BioGame file.
        /// </summary>
        /// <param name="targetDocument"></param>
        /// <param name="modDeltaDocument"></param>
        /// <returns></returns>
        private string compileCoalescedChunkFile(XDocument targetDocument, XElement modDeltaDocument, string loggingPrefix)
        {
            //Sections
            #region Sections
            
            var sectionsToHandle = modDeltaDocument.Elements("Section");
            foreach (var section in sectionsToHandle)
            {
                var sectionName = section.Attribute("name").Value;
                var operation = section.Attribute("operation").Value;
                if (operation == OP_ADDITION)
                {
                    var sectionsGroup = targetDocument.XPathSelectElement("/CoalesceAsset/Sections");
                    var newSection = new XElement("Section");
                    newSection.SetAttributeValue("name", sectionName);
                    sectionsGroup.Add(newSection);
                    CLog.Information($"{loggingPrefix}Added section: {sectionName}", Settings.LogModMakerCompiler);
                }
                else if (operation == OP_SUBTRACTION)
                {
                    var targetSection = targetDocument.XPathSelectElement($"/CoalesceAsset/Sections/Section[@name='{sectionName}']");
                    if (targetSection != null)
                    {
                        targetSection.Remove();
                        CLog.Warning($"{loggingPrefix}Removed section: {sectionName}", Settings.LogModMakerCompiler);
                    }
                    else
                    {
                        CLog.Warning($"{loggingPrefix}Could not find section to remove: {sectionName}", Settings.LogModMakerCompiler);
                    }
                }
            }

            #endregion

            #region Properties - Assignments
            /*var deltaPropertyAssignments = modDeltaDocument.Elements("Property").Where(x => x.Attribute("operation").Value == OP_ASSIGNMENT);
            foreach (var deltaProperty in deltaPropertyAssignments)
            {
                var sectionName = deltaProperty.Attribute("path").Value;
                var propertyName = deltaProperty.Attribute("name").Value;
                var type = deltaProperty.Attribute("type").Value;
                var value = deltaProperty.Value;

                var targetElement = targetDocument.XPathSelectElement($"/CoalesceAsset/Sections/Section[@name='{sectionName}']/Property[@name='{propertyName}']");
                if (targetElement == null)
                {
                    Debug.WriteLine($"Not found {sectionName}']/Property[@name='{propertyName}' and type '{type}'");
                }
                else
                {
                    if (targetElement.Value != value)
                    {
                        targetElement.Value = value;
                        //targetElement.Attribute("type").Value = type; //i don't think this is necessary but is part of old modmaker code.
                        CLog.Information($"{loggingPrefix}Assigned new value to property {sectionName} => {propertyName}, new value: {value}", Settings.LogModMakerCompiler);
                    }
                    else
                    {
                        //Not assigned, same value.
                        CLog.Information($"{loggingPrefix}Skipping same-value for {sectionName} => {propertyName}", Settings.LogModMakerCompiler);
                    }
                }
            }*/
            #endregion

            #region Properties - Addition
            /*
            var deltaPropertySubtractions = modDeltaDocument.Elements("Property").Where(x => x.Attribute("operation").Value == OP_SUBTRACTION);
            foreach (var deltaProperty in deltaPropertySubtractions)
            {
                var sectionName = deltaProperty.Attribute("path").Value;
                var propertyName = deltaProperty.Attribute("name").Value;
                var value = deltaProperty.Value;

                var targetElement = targetDocument.XPathSelectElement($"/CoalesceAsset/Sections/Section[@name='{sectionName}']/Property[@name='{propertyName}']");
                if (targetElement == null)
                {
                    CLog.Warning($"{loggingPrefix}Could not find property to remove: {sectionName} => {propertyName}", Settings.LogModMakerCompiler);
                }
                else
                {
                    if (targetElement.Value == value)
                    {
                        targetElement.Remove();
                        CLog.Information($"{loggingPrefix}Removed property {sectionName} => {propertyName}", Settings.LogModMakerCompiler);
                    }
                    else
                    {
                        //Not assigned, same value.
                        CLog.Warning($"{loggingPrefix}Did not remove property, values did not match! {sectionName} => {propertyName}. Expected '{value}', found '{targetElement.Value}'", Settings.LogModMakerCompiler);
                    }
                }
            }
            */
            #endregion

            #region ArrayProperty
            var deltaArrayProperties = modDeltaDocument.Elements("ArrayProperty");
            foreach (var deltaProperty in deltaArrayProperties)
            {
                var pathTokens = deltaProperty.Attribute("path").Value.Split('&');
                var sectionName = pathTokens[0];
                var propertyName = pathTokens[1];
                var matchOnType = deltaProperty.Attribute("matchontype").Value;
                var arrayContainer = targetDocument.XPathSelectElement($"/CoalesceAsset/Sections/Section[@name='{sectionName}']/Property[@name='{propertyName}']");

                if (arrayContainer == null)
                {
                    Log.Error($"{loggingPrefix}Did not find arrayproperty @name='{sectionName}']/Property[@name='{propertyName}' and @type='{matchOnType}']");
                }
            }
            #endregion
            return "";
        }

        private string chunkNameToFoldername(string chunkName)
        {
            switch (chunkName)
            {
                case "MP1":
                    return "DLC_CON_MP1";
                case "MP2":
                    return "DLC_CON_MP2";
                case "MP3":
                    return "DLC_CON_MP3";
                case "MP4":
                    return "DLC_CON_MP4";
                case "MP5":
                    return "DLC_CON_MP5";
                case "PATCH1":
                    return "DLC_UPD_Patch01";
                case "PATCH2":
                    return "DLC_UPD_Patch02";
                //case "BASEGAME":
                //    return "Coalesced";
                case "TESTPATCH":
                    return "DLC_TestPatch"; //special case, must be handled 
                case "FROM_ASHES":
                    return "DLC_HEN_PR";
                case "APPEARANCE":
                    return "DLC_CON_APP01";
                case "FIREFIGHT":
                    return "DLC_CON_GUN01";
                case "GROUNDSIDE":
                    return "DLC_CON_GUN02";
                case "EXTENDED_CUT":
                    return "DLC_CON_END";
                case "LEVIATHAN":
                    return "DLC_EXP_Pack001";
                case "OMEGA":
                    return "DLC_EXP_Pack002";
                case "CITADEL":
                    return "DLC_EXP_Pack003";
                case "CITADEL_BASE":
                    return "DLC_EXP_Pack003_Base";

                //Special case
                //case "BALANCE_CHANGES":
                //    return "ServerCoalesced";
                default:
                    Log.Error("Unkonwn chunk name: " + chunkName);
                    return null;
            }
        }



        /// <summary>
        /// Generates the initial mod folder and mod
        /// </summary>
        /// <param name="xmlDoc">mod document</param>
        /// <returns>Mod object</returns>
        private Mod GenerateLibraryModFromDocument(XDocument xmlDoc)
        {
            var modName = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/Name").Value;
            Log.Information(@"Compiling mod: " + modName);

            var modDev = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/Author").Value;
            var modVersion = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/Revision").Value;
            var modDescription = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/Description").Value;
            var modmakerServerVer = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/ModMakerVersion").Value;

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
