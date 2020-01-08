using IniParser;
using IniParser.Model;
using IniParser.Parser;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
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
        public Action SetModNotFoundCallback;

        private const int MaxModmakerCores = 4;
        public ModMakerCompiler(int code = 0)
        {
            this.code = code;
        }

        public Mod DownloadAndCompileMod(string delta = null)
        {
            if (delta != null)
            {
                return CompileMod(delta);
            }
            else if (code != 0)
            {
                //Try cache
                string cachedFilename = Path.Combine(Utilities.GetModmakerDefinitionsCache(), code + @".xml");
                if (File.Exists(cachedFilename))
                {
                    //Going to compile cached item
                    Log.Information("Compiling cached modmaker mode with code " + code);
                    return CompileMod(File.ReadAllText(cachedFilename));
                }
            }
            return null; //could not compile mod

        }

        /// <summary>
        /// Compiles a mod using the specified mod definition text
        /// </summary>
        /// <param name="modxml">XML document for the mod</param>
        private Mod CompileMod(string modxml)
        {
            Log.Information("Compiling modmaker mod");
            var xmlDoc = XDocument.Parse(modxml);

            var mod = GenerateLibraryModFromDocument(xmlDoc);
            if (mod != null)
            {
                compileMixins(xmlDoc, mod);
                compileCoalesceds(xmlDoc, mod);
                //compileTLKs(xmlDoc, mod); //Compile TLK
                finalizeModdesc(mod);
                return mod;
            }
            else
            {
                SetModNameCallback?.Invoke("Mod not found on server");
                SetModNotFoundCallback?.Invoke();
                return null;
            }
        }

        private void compileMixins(XDocument xmlDoc, Mod mod)
        {
            SetCurrentTaskStringCallback?.Invoke("Preparing Mixin patch data");
            SetCurrentTaskIndeterminateCallback?.Invoke(true);

            //Build mixin list by module=>files=>list of mixins for file
            var mixinNode = xmlDoc.XPathSelectElement(@"/ModMaker/MixInData");
            var me3tweaksmixinsdata = mixinNode.Elements("MixIn").Select(x => int.Parse(x.Value.Substring(0, x.Value.IndexOf("v")))).ToList();
            var dynamicmixindata = mixinNode.Elements("DynamicMixIn").ToList();

            List<Mixin> allmixins = new List<Mixin>();
            allmixins.AddRange(me3tweaksmixinsdata.Select(MixinHandler.GetMixinByME3TweaksID));
            allmixins.AddRange(dynamicmixindata.Select(MixinHandler.ReadDynamicMixin));

            var compilingListsPerModule = new Dictionary<ModJob.JobHeader, Dictionary<string, List<Mixin>>>();
            var modules = allmixins.Select(x => x.TargetModule).Distinct().ToList();
            foreach (var module in modules)
            {
                var moduleMixinMapping = new Dictionary<string, List<Mixin>>();
                var mixinsForModule = allmixins.Where(x => x.TargetModule == module).ToList();
                foreach (var mixin in mixinsForModule)
                {
                    List<Mixin> mixinListForFile;
                    if (!moduleMixinMapping.TryGetValue(mixin.TargetFile, out mixinListForFile))
                    {
                        mixinListForFile = new List<Mixin>();
                        moduleMixinMapping[mixin.TargetFile] = mixinListForFile;
                    }

                    //make sure finalizer is last
                    if (mixin.IsFinalizer)
                    {
                        CLog.Information($@"Adding finalizer mixin to mixin list for file {Path.GetFileName(mixin.TargetFile)}: {mixin.PatchName}", Settings.LogModMakerCompiler);
                        mixinListForFile.Add(mixin);
                    }
                    else
                    {
                        CLog.Information($@"Adding mixin to mixin list for file {Path.GetFileName(mixin.TargetFile)}: {mixin.PatchName}", Settings.LogModMakerCompiler);
                        mixinListForFile.Insert(0, mixin);
                    }
                }

                //verify only one finalizer
                foreach (var list in moduleMixinMapping)
                {
                    if (list.Value.Count(x => x.IsFinalizer) > 1)
                    {
                        Log.Error(@"ERROR: MORE THAN ONE FINALIZER IS PRESENT FOR FILE: " + list.Key);
                        //do something here to abort
                    }
                }
                compilingListsPerModule[module] = moduleMixinMapping;
            }

            int totalMixinsToApply = compilingListsPerModule.Sum(x => x.Value.Values.Sum(y => y.Count()));
            int numMixinsApplied = 0;
            SetCurrentMaxCallback(totalMixinsToApply);
            SetCurrentValueCallback(0);
            SetCurrentTaskIndeterminateCallback?.Invoke(false);
            SetCurrentTaskStringCallback?.Invoke("Applying Mixins");
            void completedSingleApplicationCallback()
            {
                var numdone = Interlocked.Increment(ref numMixinsApplied);
                if (numdone > totalMixinsToApply)
                {
                    Log.Warning($@"Error in progress calculation, numdone > total. Done: {numdone} Total: {totalMixinsToApply}");
                }
                SetCurrentValueCallback?.Invoke(numdone);
            };
            //Mixins are ready to be applied
            Parallel.ForEach(compilingListsPerModule, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount > MaxModmakerCores ? MaxModmakerCores : Environment.ProcessorCount }, mapping =>
             {
                 var dlcFolderName = chunkNameToDLCFoldername(mapping.Key.ToString());
                 var outdir = Path.Combine(mod.ModPath, headerToModFoldername(mapping.Key), @"CookedPCConsole");
                 Directory.CreateDirectory(outdir);
                 if (mapping.Key == ModJob.JobHeader.BASEGAME)
                 {
                     //basegame
                     foreach (var file in mapping.Value)
                     {
                         using var packageAsStream = VanillaDatabaseService.FetchBasegameFile(Mod.MEGame.ME3, Path.GetFileName(file.Key));
                         using var decompressedStream = MEPackage.GetDecompressedPackageStream(packageAsStream);
                         using var finalStream = MixinHandler.ApplyMixins(decompressedStream, file.Value, completedSingleApplicationCallback);
                         var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));
                         finalStream.WriteToFile(outfile);
                         //File.WriteAllBytes(outfile, finalStream.ToArray());
                     }
                 }
                 else
                 {
                     //dlc
                     var dlcPackage = VanillaDatabaseService.FetchVanillaSFAR(dlcFolderName); //do not have to open file multiple times.
                     foreach (var file in mapping.Value)
                     {
                         using var packageAsStream = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcFolderName, file.Key, forcedDLC: dlcPackage);
                         using var decompressedStream = MEPackage.GetDecompressedPackageStream(packageAsStream);
                         using var finalStream = MixinHandler.ApplyMixins(decompressedStream, file.Value, completedSingleApplicationCallback);
                         var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));
                         finalStream.WriteToFile(outfile);
                     }
                 }
             });


            //var filename = Path.GetFileName(mixin2.TargetFile);
            //filedata.Position = 0;
            //var packageTest = MEPackageHandler.OpenMEPackage(filedata);
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

            Parallel.ForEach(jobCollection, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount > MaxModmakerCores ? MaxModmakerCores : Environment.ProcessorCount }, (xmlChunk) => compileCoalescedChunk(xmlChunk, mod));
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
                var dlcFolderName = chunkNameToDLCFoldername(chunkName);
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

                var outFolder = Path.Combine(mod.ModPath, chunkName, "CookedPCConsole");
                if (chunkName == "BALANCE_CHANGES")
                {
                    outFolder = Path.Combine(mod.ModPath, chunkName);
                }
                Directory.CreateDirectory(outFolder);
                var outFile = Path.Combine(outFolder, coalescedFilename);

                newFileStream.WriteToFile(outFile);
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

        private void finalizeModdesc(Mod mod)
        {
            //Update moddesc
            IniData ini = new FileIniDataParser().ReadFile(mod.ModDescPath);
            var dirs = Directory.GetDirectories(mod.ModPath);

            foreach (var dir in dirs)
            {
                //automap
                var dirname = Path.GetFileName(dir);
                var headername = defaultFoldernameToHeader(dirname).ToString();
                ini[headername]["moddir"] = dirname;
                if (dirname != "BALANCE_CHANGES")
                {
                    ini[headername]["newfiles"] = "CookedPCConsole";

                    string inGameDestdir = @"BIOGame\";
                    if (dirname == "BASEGAME")
                    {
                        inGameDestdir = @"BIOGame/CookedPCConsole";
                    }
                    else
                    {
                        //DLC
                        inGameDestdir = $@"BIOGame/DLC/{chunkNameToDLCFoldername(dirname)}/CookedPCConsole";
                    }

                    ini[headername]["replacefiles"] = inGameDestdir;
                    ini[headername]["gamedirectorystructure"] = "true";
                }
                else
                {
                    ini[headername]["newfiles"] = "ServerCoalesced.bin"; //BALANCE_CHANGES
                }
            }
            CLog.Information("Writing finalized moddesc to library", Settings.LogModMakerCompiler);
            File.WriteAllText(mod.ModDescPath, ini.ToString());
        }

        private string headerToModFoldername(ModJob.JobHeader header)
        {
            switch (header)
            {
                case ModJob.JobHeader.RESURGENCE:
                    return "MP1";
                case ModJob.JobHeader.REBELLION:
                    return "MP2";
                case ModJob.JobHeader.EARTH:
                    return "MP3";
                case ModJob.JobHeader.RETALIATION:
                    return "MP4";
                case ModJob.JobHeader.RECKONING:
                    return "MP5";
            }
            return header.ToString();
        }

        private ModJob.JobHeader defaultFoldernameToHeader(string foldername)
        {
            if (Enum.TryParse<ModJob.JobHeader>(foldername, out var header))
            {
                return header;
            }
            switch (foldername)
            {
                case "MP1":
                    return ModJob.JobHeader.RESURGENCE;
                case "MP2":
                    return ModJob.JobHeader.REBELLION;
                case "MP3":
                    return ModJob.JobHeader.EARTH;
                case "MP4":
                    return ModJob.JobHeader.RETALIATION;
                case "MP5":
                    return ModJob.JobHeader.RECKONING;
            }
            throw new Exception("Unknown default foldername: " + foldername);
        }

        private string chunkNameToDLCFoldername(string chunkName)
        {
            switch (chunkName)
            {
                case "BASEGAME":
                    return null; //kind of a hack. This is not a DLC folder but i don't want to return error log
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
