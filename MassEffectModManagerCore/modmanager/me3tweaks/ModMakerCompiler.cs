using IniParser.Model;
using MassEffectModManagerCore.modmanager.helpers;
using ME3Explorer.Packages;
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

        //Callbacks. The caller should set these to update the UI.
        public Action<int> SetOverallMaxCallback;
        public Action<int> SetOverallValueCallback;
        public Action<int> SetCurrentMaxCallback;
        public Action<int> SetCurrentValueCallback;
        public Action<bool> SetCurrentTaskIndeterminateCallback;
        public Action<string> SetCurrentTaskStringCallback;
        public Action<string> SetModNameCallback;
        public Action SetCompileStarted;

        public ModMakerCompiler(int code = 0)
        {
            this.code = code;
        }

        public void DownloadAndCompileMod(string delta = null)
        {
            if (delta != null)
            {
                CompileMod(delta);
            }
            else if (code != 0)
            {
                //Try cache
                string cachedFilename = Path.Combine(Utilities.GetModmakerDefinitionsCache(), code + @".xml");
                if (File.Exists(cachedFilename))
                {
                    //Going to compile cached item
                    Log.Information("Compiling cached modmaker mode with code " + code);
                    CompileMod(File.ReadAllText(cachedFilename));
                }
            }

        }

        /// <summary>
        /// Compiles a mod using the specified mod definition text
        /// </summary>
        /// <param name="modxml">XML document for the mod</param>
        private void CompileMod(string modxml)
        {
            Log.Information("Compiling modmaker mod");
            var xmlDoc = XDocument.Parse(modxml);

            var mod = GenerateLibraryModFromDocument(xmlDoc);
            if (mod != null)
            {
                compileMixins(xmlDoc, mod);
                compileCoalesceds(xmlDoc, mod);
            }
            else
            {
                SetModNameCallback?.Invoke("Mod not found on server");
            }
        }

        private void compileMixins(XDocument xmlDoc, Mod mod)
        {
            //TESTING ONLY
            var mixinNode = xmlDoc.XPathSelectElement(@"/ModMaker/MixInData");
            var me3tweaksmixins = mixinNode.Elements("MixIn").Select(x => int.Parse(x.Value.Substring(0,x.Value.IndexOf("v")))).ToList();
            var dynamicmixins = mixinNode.Elements("DynamicMixIn").ToList();

            var firstMixinTest = me3tweaksmixins.First();
            var mixin = MixinHandler.GetMixinByME3TweaksID(firstMixinTest);
            var dlcFolderName = chunkNameToFoldername(mixin.TargetModule.ToString());
            var filename = Path.GetFileName(mixin.TargetFile);
            var filedata = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcFolderName, filename);
            filedata.Position = 0;
            var packageTest = MEPackageHandler.OpenMEPackage(filedata);
        }

        int totalNumCoalescedFileChunks = 0;
        int numDoneCoalescedFileChunks = 0;
        private void compileCoalesceds(XDocument xmlDoc, Mod mod)
        {
            SetCurrentTaskStringCallback?.Invoke("Compiling Coalesced files");
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
                        totalNumCoalescedFileChunks += node.Elements().Count();
                    }
                }
            }

            SetCurrentMaxCallback?.Invoke(totalNumCoalescedFileChunks);
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
            string coalescedFilename = null;
            if (chunkName == "BASEGAME")
            {
                var coalPath = Path.Combine(Utilities.GetGameBackupPath(MEGame.ME3), "BioGame", "CookedPCConsole", "Coalesced.bin");
                coalescedFilename = "Coalesced.bin";
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
                var serverCoalesced = Utilities.ExtractInternalFileToStream("MassEffectModManagerCore.modmanager.me3tweaks.LiveIni.bin");
                coalescedFilemapping = MassEffect3.Coalesce.Converter.DecompileToMemory(serverCoalesced);
                coalescedFilename = "ServerCoalesced.bin";
            }
            else
            {
                var dlcFolderName = chunkNameToFoldername(chunkName);
                var coalescedData = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcFolderName, $"Default_{dlcFolderName}.bin");
                coalescedFilename = $"Default_{dlcFolderName}.bin";
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
                    coalescedFilemapping[fileNode.Name + ".xml"] = updatedDocumentText;
                }
                CLog.Information($"{loggingPrefix} Recompiling coalesced file", Settings.LogModMakerCompiler);
                var newFileStream = MassEffect3.Coalesce.Converter.CompileFromMemory(coalescedFilemapping);

                var outFolder = Path.Combine(mod.ModPath, chunkName);
                Directory.CreateDirectory(outFolder);
                var outFile = Path.Combine(outFolder, coalescedFilename);

                File.WriteAllBytes(outFile, newFileStream.ToArray());
                CLog.Information($"{loggingPrefix} Compiled coalesced file, chunk finished", Settings.LogModMakerCompiler);
            }

            return true;
        }

        private const string OP_ADDITION = "addition";
        private const string OP_SUBTRACTION = "subtraction";
        private const string OP_ASSIGNMENT = "assignment";
        private const string OP_MODIFY = "modify"; //same as assignment, except used for array values

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
            var deltaPropertyAssignments = modDeltaDocument.Elements("Property").Where(x => x.Attribute("operation").Value == OP_ASSIGNMENT);
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
            }
            #endregion

            #region Properties - Subtraction
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
            #endregion

            #region Properties = Addition
            var deltaPropertyAdditions = modDeltaDocument.Elements("Property").Where(x => x.Attribute("operation").Value == OP_ADDITION);
            foreach (var deltaProperty in deltaPropertyAdditions)
            {
                var sectionName = deltaProperty.Attribute("path").Value;
                var propertyName = deltaProperty.Attribute("name").Value;
                var value = deltaProperty.Value;
                var type = deltaProperty.Attribute("type").Value;

                var targetElement = targetDocument.XPathSelectElement($"/CoalesceAsset/Sections/Section[@name='{sectionName}']");
                if (targetElement == null)
                {
                    CLog.Warning($"{loggingPrefix}Could not find property to remove: {sectionName} => {propertyName}", Settings.LogModMakerCompiler);
                }
                else
                {
                    var newSection = new XElement("Property", value);
                    newSection.SetAttributeValue("name", propertyName);
                    newSection.SetAttributeValue("type", type);
                    targetElement.Add(newSection);
                    CLog.Information($"{loggingPrefix}Added property {sectionName} => {propertyName}", Settings.LogModMakerCompiler);
                }
            }
            #endregion

            #region ArrayProperty
            var deltaArrayProperties = modDeltaDocument.Elements("ArrayProperty");
            foreach (var deltaProperty in deltaArrayProperties)
            {
                var pathTokens = deltaProperty.Attribute("path").Value.Split('&');
                var sectionName = pathTokens[0];
                var propertyName = pathTokens[1];
                var matchOnType = deltaProperty.Attribute("matchontype").Value;
                var type = deltaProperty.Attribute("type").Value;
                var value = deltaProperty.Value;
                var arrayContainer = targetDocument.XPathSelectElement($"/CoalesceAsset/Sections/Section[@name='{sectionName}']/Property[@name='{propertyName}']");

                if (arrayContainer == null)
                {
                    Log.Error($"{loggingPrefix}Did not find arrayproperty @name='{sectionName}']/Property[@name='{propertyName}' and @type='{matchOnType}']");
                }
                else
                {
                    //Log.Information($"{loggingPrefix}Found array countainer {sectionName} => {propertyName}");
                    var operation = deltaProperty.Attribute("operation").Value;
                    if (operation == OP_ADDITION)
                    {
                        var newArrayElement = new XElement("Value", value);
                        newArrayElement.SetAttributeValue("type", type);
                        arrayContainer.Add(newArrayElement);
                        CLog.Information($"{loggingPrefix}Added array element {sectionName} => {propertyName} -> type({type}): {value}", Settings.LogModMakerCompiler);
                    }
                    else if (operation == OP_SUBTRACTION)
                    {
                        var matchingAlgorithm = deltaProperty.Attribute("arraytype").Value;
                        var values = arrayContainer.Descendants("Value");
                        var matchingItem = findArrayElementBasedOnAlgoritm(sectionName, propertyName, values, matchingAlgorithm, matchOnType, value);
                        if (matchingItem == null)
                        {
                            CLog.Warning($"{loggingPrefix}Could not find array element to remove: {sectionName} => {propertyName} -> type({matchOnType}): {value}", Settings.LogModMakerCompiler);
                        }
                        else
                        {
                            matchingItem.Remove();
                            CLog.Information($"{loggingPrefix}Removed array element: {sectionName} => {propertyName} -> type({matchOnType}): {value}", Settings.LogModMakerCompiler);
                        }
                    }
                    else if (operation == OP_ASSIGNMENT || operation == OP_MODIFY)
                    {
                        //Algorithms based
                        var matchingAlgorithm = deltaProperty.Attribute("arraytype").Value;
                        var values = arrayContainer.Descendants("Value");
                        var matchingItem = findArrayElementBasedOnAlgoritm(sectionName, propertyName, values, matchingAlgorithm, matchOnType, value);
                        if (matchingItem == null)
                        {
                            CLog.Warning($"Could not find matching element: {sectionName} => {propertyName}, type({type}), algorithm {matchingAlgorithm}", Settings.LogModMakerCompiler);
                        }
                        else
                        {
                            Debug.WriteLine($"Found matching item {sectionName} => {propertyName}, type({type}), algorithm {matchingAlgorithm}");
                            matchingItem.Value = value; //assign
                        }
                    }
                }
            }
            #endregion

            var numdone = Interlocked.Increment(ref numDoneCoalescedFileChunks);
            SetCurrentValueCallback?.Invoke(numdone);
            return targetDocument.ToString();
        }

        /// <summary>
        /// This is the vanilla value for Plat Collectors Wave 5. It should be Do_Level4 but bioware set it to 3.
        /// </summary>
        private static readonly string CollectorsPlatWave5WrongText = "(Difficulty=DO_Level3,Enemies=( (EnemyType=\"WAVE_COL_Scion\"), (EnemyType=\"WAVE_COL_Praetorian\", MinCount=1, MaxCount=1), (EnemyType=\"WAVE_CER_Phoenix\", MinCount=2, MaxCount=2), (EnemyType=\"WAVE_CER_Phantom\", MinCount=3, MaxCount=3) ))";



        /// <summary>
        /// Finds an array element (Property->Value) based on a list of array types from ME3Tweaks ModMaker. THis is used to find items in a list to modify without fully having to rewrite the list.
        /// </summary>
        /// <param name="values">List of values to search through</param>
        /// <param name="matchingAlgorithm">The algorithm type (arraytype)</param>
        /// <param name="matchOnType">What type to match on</param>
        /// <param name="value">The value information for lookup (may be new data for assignment in conjunction with algorithm)</param>
        /// <returns></returns>
        private XElement findArrayElementBasedOnAlgoritm(string sectionName, string propertyName, IEnumerable<XElement> values, string matchingAlgorithm, string matchOnType, string value)
        {
            switch (matchingAlgorithm)
            {
                case "exactvalue":
                    {
                        return values.FirstOrDefault(x => x.Value == value && matchOnType == x.Attribute("type").Value);
                    }
                case "id":
                    {
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier("ID", matchOnType, element, newValues))
                            {
                                return element;
                            }
                        }
                        CLog.Warning("Could not find element using ID algorithm for value " + value, Settings.LogModMakerCompiler);
                        break;
                    }
                case "wavecost":
                case "enemytype":
                    {
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier("EnemyType", matchOnType, element, newValues))
                            {
                                return element;
                            }
                        }
                        CLog.Warning("Could not find element using enemytype/wavecost algorithm for value " + value, Settings.LogModMakerCompiler);
                        break;
                    }
                case "biodifficulty":
                    {
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier("Category", matchOnType, element, newValues))
                            {
                                return element;
                            }
                        }
                        CLog.Warning("Could not find element using category algorithm for value " + value, Settings.LogModMakerCompiler);
                    }
                    break;
                case "wavelist":
                    {
                        //Collector Plat Wave 5 is set to DO_Level3 even though it should be 4.
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier("Difficulty", matchOnType, element, newValues))
                            {
                                return element;
                            }
                            else if (newValues["Difficulty"] == "DO_Level4" && sectionName == "sfxwave_horde_collector5 sfxwave_horde_collector" && propertyName == "enemies" && element.Value == CollectorsPlatWave5WrongText)
                            {
                                Debug.WriteLine("Found wrong collectors wave 5 data from bioware, returning");
                                return element;
                            }
                        }
                        CLog.Warning("Could not find element using wavelist algorithm for value " + value, Settings.LogModMakerCompiler);
                    }
                    break;
                case "possessionwaves":
                case "shareddifficulty":
                case "wavebudget":
                    {
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier("Difficulty", matchOnType, element, newValues))
                            {
                                return element;
                            }
                        }
                        CLog.Warning("Could not find element using shareddifficulty/wavebudget/possessionwaves algorithm for value " + value, Settings.LogModMakerCompiler);
                    }
                    break;
                case "waveclass":
                    {
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier("WaveClassName", matchOnType, element, newValues))
                            {
                                return element;
                            }
                        }
                    }
                    CLog.Warning("Could not find element using enemytype algorithm for value " + value, Settings.LogModMakerCompiler);
                    break;
                default:
                    Log.Error($"Unknown array value matching algorithm: { matchingAlgorithm}. Ths modification of this value will be skipped: {sectionName} -> {propertyName} for {value}");
                    break;
            }
            return null;
        }
        private bool matchesOnIdentifier(string identifierKey, string matchOnType, XElement element, Dictionary<string, string> newValues)
        {
            var type = element.Attribute("type").Value;
            if (type != matchOnType) return false;
            var elementValues = StringStructParser.GetCommaSplitValues(element.Value);
            return elementValues[identifierKey] == newValues[identifierKey];
        }

        private string chunkNameToFoldername(string chunkName)
        {
            switch (chunkName)
            {
                case "RESURGENCE":
                case "MP1":
                    return "DLC_CON_MP1";
                case "REBELLION":
                case "MP2":
                    return "DLC_CON_MP2";
                case "EARTH":
                case "MP3":
                    return "DLC_CON_MP3";
                case "RETALIATION":
                case "MP4":
                    return "DLC_CON_MP4";
                case "RECKONING":
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
            var hasError = xmlDoc.XPathSelectElement(@"/ModMaker/Error");
            if (hasError != null)
            {
                Log.Information("Mod was not found server.");
                return null;
            }
            SetCompileStarted?.Invoke();
            var modName = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/Name").Value;
            SetModNameCallback?.Invoke(modName);
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
            CLog.Information("Generating new mod directory: " + outputDir, Settings.LogModMakerCompiler);
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
