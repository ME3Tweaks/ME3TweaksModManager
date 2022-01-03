using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using IniParser;
using IniParser.Model;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME2ME3;
using ME3TweaksCore.Services;
using ME3TweaksCore.Services.Backup;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols;
using Microsoft.AppCenter.Analytics;

namespace ME3TweaksModManager.modmanager.me3tweaks
{
    public class ModMakerCompiler
    {
        private readonly int code;
        private string modName;

        //Callbacks. The caller should set these to update the UI.
        public Action<int> SetOverallMaxCallback;
        public Action<int> SetOverallValueCallback;
        public Action<int> SetCurrentMaxCallback;
        public Action<int> SetCurrentValueCallback;
        public Action<bool> SetCurrentTaskIndeterminateCallback;
        public Action<string> SetCurrentTaskStringCallback;
        public Action<string> SetModNameCallback;
        public Action<string> ShowErrorMessageCallback;
        public Func<List<string>, bool> NotifySomeDLCIsMissing;
        public Action SetCompileStarted;
        public Action SetModNotFoundCallback;

        private const int MaxModmakerCores = 4;
        private int OverallProgressMax;
        private int OverallProgressValue;

        public ModMakerCompiler(int code = 0)
        {
            this.code = code;
        }

        public Mod DownloadAndCompileMod(string delta = null, string modPathOverride = null)
        {
            if (delta != null)
            {
                return CompileMod(delta, modPathOverride);
            }
            else if (code != 0)
            {
                //Try cache
                string cachedFilename = Path.Combine(M3Utilities.GetModmakerDefinitionsCache(), code + @".xml");
                if (File.Exists(cachedFilename))
                {
                    //Going to compile cached item
                    M3Log.Information(@"Compiling cached modmaker mode with code " + code);
                    return CompileMod(File.ReadAllText(cachedFilename), modPathOverride);
                }
            }
            return null; //could not compile mod

        }

        /// <summary>
        /// Compiles a mod using the specified mod definition text
        /// </summary>
        /// <param name="modxml">XML document for the mod</param>
        private Mod CompileMod(string modxml, string modPathOverride = null)
        {
            M3Log.Information(@"Compiling modmaker mod");
            var xmlDoc = XDocument.Parse(modxml);

            var mod = GenerateLibraryModFromDocument(xmlDoc, modPathOverride);
            if (mod != null)
            {
                var requiredDLC = calculateNumberOfTasks(xmlDoc);
                //Check Required DLC
                List<string> missingDlc = new List<string>();
                var dlcFolderPath = Path.Combine(BackupService.GetGameBackupPath(MEGame.ME3), @"BioGame", @"DLC");
                foreach (var reqDlc in requiredDLC)
                {
                    if (reqDlc == @"DLC_TestPatch") continue; //don't do this one
                    var reqDlcSfar = Path.Combine(dlcFolderPath, reqDlc, @"CookedPCConsole", @"Default.sfar");
                    if (!File.Exists(reqDlcSfar))
                    {
                        missingDlc.Add($@"{reqDlc} ({ME3Directory.OfficialDLCNames[reqDlc]})");
                    }
                }

                if (missingDlc.Any())
                {
                    if (NotifySomeDLCIsMissing != null && !NotifySomeDLCIsMissing.Invoke(missingDlc))
                    {
                        //User canceled build
                        SetModNameCallback?.Invoke(M3L.GetString(M3L.string_downloadAborted));
                        return null;
                    }
                }

                compileTLKs(xmlDoc, mod); //Compile TLK
                Debug.WriteLine($@"Progress at end of TLK compiling: {OverallProgressValue}");
                compileMixins(xmlDoc, mod);
                Debug.WriteLine($@"Progress at end of mixin compilation: {OverallProgressValue}");
                compileCoalesceds(xmlDoc, mod);
                Debug.WriteLine($@"Progress at end of coalesceds: {OverallProgressValue}");
                finalizeModdesc(xmlDoc, mod);
                MixinHandler.AttemptResetMemoryManager();
                Analytics.TrackEvent(@"Downloaded ModMaker Mod", new Dictionary<string, string>()
                {
                    {@"Code", code.ToString() },
                    {@"Mod Name", modName }
                });
                return mod;
            }
            else
            {
                SetModNameCallback?.Invoke(M3L.GetString(M3L.string_modNotFoundOnServer));
                SetModNotFoundCallback?.Invoke();
                return null;
            }
        }

        private SortedSet<string> calculateNumberOfTasks(XDocument xmlDoc)
        {
            SortedSet<string> requiredDLCFolders = new SortedSet<string>();
            var backupDir = BackupService.GetGameBackupPath(MEGame.ME3, true);

            var backupDlcDir = MEDirectories.GetDLCPath(MEGame.ME3, backupDir);
            DLCFolders = Directory.EnumerateDirectories(backupDlcDir).Select(x => Path.GetFileName(x)).ToList();

            int numTasks = 0;
            //TLK
            var tlkNode = xmlDoc.XPathSelectElement(@"/ModMaker/TLKData");
            if (tlkNode != null)
            {
                var tlknodescount = tlkNode.Elements().Count();
                numTasks += tlknodescount * TLK_OVERALL_WEIGHT; //TLK is worth 3 units
            }
            Debug.WriteLine($@"Num tasks after TLK: {numTasks}");
            //MIXINS
            var mixinNode = xmlDoc.XPathSelectElement(@"/ModMaker/MixInData");
            if (mixinNode != null)
            {
                int mixincount = 0;
                var me3tweaksmixinsdata = mixinNode.Elements(@"MixIn")
                    .Select(x => int.Parse(x.Value.IndexOf(@"v") > 0 ? x.Value.Substring(0, x.Value.IndexOf(@"v")) : x.Value)).ToList();
                foreach (var mixin in me3tweaksmixinsdata)
                {
                    var mixinobj = MixinHandler.GetMixinByME3TweaksID(mixin);
                    if (mixinobj != null)
                    {
                        var tmtext = mixinobj.TargetModule;
                        var tm = ModmakerChunkNameToDLCFoldername(tmtext.ToString());
                        if (tm != null) requiredDLCFolders.Add(tm); //null is basegame and balance changes
                        if (tm == null || tm == @"DLC_TestPatch" || DLCFolders.Contains(tm, StringComparer.InvariantCultureIgnoreCase))
                        {
                            mixincount++;
                        }
                        else
                        {
                            Debug.WriteLine(@"Not adding " + tm);
                        }
                    }
                    else
                    {
                        M3Log.Error($@"MixinHandler returned null for mixinid {mixin}! Has the MixinPackage loaded?");
                    }
                }

                var dmixins = mixinNode.Elements(@"DynamicMixIn");
                foreach (var mixin in dmixins)
                {
                    var tmtext = mixin.Attribute(@"targetmodule");
                    var tm = ModmakerChunkNameToDLCFoldername(tmtext.Value);
                    if (tm != null) requiredDLCFolders.Add(tm); //null is basegame or balance changes
                    if (tm == null || tm == @"DLC_TestPatch" || DLCFolders.Contains(tm, StringComparer.InvariantCultureIgnoreCase))
                    {
                        mixincount++;
                    }
                    else
                    {
                        Debug.WriteLine(@"Not adding " + tm);
                    }
                }
                numTasks += mixincount * MIXIN_OVERALL_WEIGHT; //Mixin is 1 unit.
            }

            Debug.WriteLine($@"Num tasks after Mixins: {numTasks}");


            //COALESCED
            var jobs = xmlDoc.XPathSelectElements(@"/ModMaker/ModData/*");
            if (jobs != null)
            {
                foreach (var job in jobs)
                {
                    var foldername = ModmakerChunkNameToDLCFoldername(job.Name.LocalName);
                    if (foldername != null) requiredDLCFolders.Add(foldername);
                    if (job.Name.LocalName == @"BASEGAME" || job.Name.LocalName == @"BALANCE_CHANGES" || job.Name.LocalName == @"TESTPATCH" || DLCFolders.Contains(foldername, StringComparer.InvariantCultureIgnoreCase))
                    {
                        numTasks += job.Elements().Count() * COALESCED_CHUNK_OVERALL_WEIGHT;
                        numTasks += COALESCED_CHUNK_OVERALL_WEIGHT; //Compile adds weight
                    }
                    else
                    {
                        Debug.WriteLine(@"Not adding " + foldername);
                    }
                }
            }

            // add compile of coalesced
            Debug.WriteLine($@"Num tasks after Coalesced: {numTasks}");

            OverallProgressMax = numTasks;
            SetOverallMaxCallback?.Invoke(numTasks);
            return requiredDLCFolders;
        }


        private void compileTLKs(XDocument xmlDoc, Mod mod)
        {
            var tlkNode = xmlDoc.XPathSelectElement(@"/ModMaker/TLKData");
            if (tlkNode != null)
            {
                var tlknodes = tlkNode.Elements();

                int totalTLKSteps = tlknodes.Count() * 3; //decomp modify recomp
                int numDoneTLKSteps = 0;
                SetCurrentValueCallback?.Invoke(0);
                SetCurrentMaxCallback?.Invoke(totalTLKSteps);
                SetCurrentTaskStringCallback?.Invoke(M3L.GetString(M3L.string_compilingTLKFiles));
                Parallel.ForEach(tlknodes, new ParallelOptions() { MaxDegreeOfParallelism = MaxModmakerCores }, tlknode =>
                {
                    var lang = tlknode.Name;
                    string loggingPrefix = $@"[TLK][{lang}]: ";
                    var newstringnodes = tlknode.Elements(@"TLKProperty");
                    string filename = $@"BIOGame_{lang}.tlk";
                    var vanillaTLK = VanillaDatabaseService.FetchBasegameFile(MEGame.ME3, filename);
                    if (vanillaTLK != null)
                    {
                        var tf = new ME2ME3TalkFile();
                        tf.LoadTlkDataFromStream(vanillaTLK);
                        SetCurrentValueCallback?.Invoke(Interlocked.Increment(ref numDoneTLKSteps)); //decomp
                        foreach (var strnode in newstringnodes)
                        {
                            var id = int.Parse(strnode.Attribute(@"id").Value);
                            var matchingref = tf.StringRefs.FirstOrDefault(x => x.StringID == id);
                            if (matchingref != null)
                            {
                                matchingref.Data = strnode.Value;
                                M3Log.Information($@"{loggingPrefix}Set {id} to {matchingref.Data}",
                                    Settings.LogModMakerCompiler);
                            }
                            else
                            {
                                M3Log.Warning($@"{loggingPrefix} Could not find string id {id} in TLK");
                            }
                        }

                        SetCurrentValueCallback?.Invoke(Interlocked.Increment(ref numDoneTLKSteps)); //modify

                        string outfolder = Path.Combine(mod.ModPath, @"BASEGAME", @"CookedPCConsole");
                        Directory.CreateDirectory(outfolder);
                        string outfile = Path.Combine(outfolder, filename);
                        M3Log.Information($@"{loggingPrefix} Saving TLK", Settings.LogModMakerCompiler);
                        HuffmanCompression.SaveToTlkFile(outfile, tf.StringRefs);
                        M3Log.Information($@"{loggingPrefix} Saved TLK to mod BASEGAME folder",
                            Settings.LogModMakerCompiler);
                        SetCurrentValueCallback?.Invoke(Interlocked.Increment(ref numDoneTLKSteps)); //recomp
                    }
                    else
                    {
                        M3Log.Warning($@"TLK file not found: {vanillaTLK}, skipping");
                        SetCurrentValueCallback?.Invoke(Interlocked.Add(ref numDoneTLKSteps, 3)); //skip 3 steps
                    }

                    var numOverallDone = Interlocked.Add(ref OverallProgressValue, TLK_OVERALL_WEIGHT);
                    SetOverallValueCallback?.Invoke(numOverallDone);
                });

            }
            else
            {
                M3Log.Information(@"This mod does not have a TLKData section. TLKs will not be compiled.", Settings.LogModMakerCompiler);
            }
        }

        private void compileMixins(XDocument xmlDoc, Mod mod)
        {
            SetCurrentTaskStringCallback?.Invoke(M3L.GetString(M3L.string_preparingMixinPatchData));
            SetCurrentTaskIndeterminateCallback?.Invoke(true);

            //Build mixin list by module=>files=>list of mixins for file
            var mixinNode = xmlDoc.XPathSelectElement(@"/ModMaker/MixInData");
            if (mixinNode != null)
            {
                var me3tweaksmixinsdata = mixinNode.Elements(@"MixIn")
                    .Select(x => int.Parse(x.Value.IndexOf(@"v") > 0 ? x.Value.Substring(0, x.Value.IndexOf(@"v")) : x.Value)).ToList();
                var dynamicmixindata = mixinNode.Elements(@"DynamicMixIn").ToList();

                List<Mixin> allmixins = new List<Mixin>();
                allmixins.AddRange(me3tweaksmixinsdata.Select(MixinHandler.GetMixinByME3TweaksID));

                //Controller addins
                if (Settings.ModMakerControllerModOption)
                {
                    if (allmixins.Any(x => Path.GetFileName(x.TargetFile) == @"SFXGame.pcc"))
                    {
                        M3Log.Information(@"Added controller camera mixin as this mod modifies SFXGame and controller option is on");
                        allmixins.Add(MixinHandler.GetMixinByME3TweaksID(1533));
                        Interlocked.Increment(ref OverallProgressMax);
                    }
                    if (allmixins.Any(x => Path.GetFileName(x.TargetFile) == @"Patch_BioPlayerController.pcc"))
                    {
                        M3Log.Information(@"Added controller vibration mixin as this mod modifies Patch_BioPlayerController and controller option is on");
                        allmixins.Add(MixinHandler.GetMixinByME3TweaksID(1557));
                        Interlocked.Increment(ref OverallProgressMax);
                    }

                    // We need to re-issue this as the number of mixins to install has changed. We could technically precalculate this but it would require additional work.
                    SetOverallMaxCallback?.Invoke(OverallProgressMax);
                }

                MixinHandler.LoadPatchDataForMixins(allmixins); //before dynamic
                allmixins.AddRange(dynamicmixindata.Select(MixinHandler.ReadDynamicMixin));

                var backupDir = BackupService.GetGameBackupPath(MEGame.ME3, true);
                if (backupDir != null)
                {
                    var backupDlcDir = MEDirectories.GetDLCPath(MEGame.ME3, backupDir);
                    var dlcFolders = Directory.EnumerateDirectories(backupDlcDir).Select(x => Path.GetFileName(x)).ToList();
                    allmixins = allmixins.Where(x => x.TargetModule == ModJob.JobHeader.BASEGAME
                                                     || x.TargetModule == ModJob.JobHeader.TESTPATCH
                        || dlcFolders.Contains(ModmakerChunkNameToDLCFoldername(x.TargetModule.ToString()))).ToList();
                }

                var compilingListsPerModule = MixinHandler.GetMixinApplicationList(allmixins, ShowErrorMessageCallback);
                int totalMixinsToApply = compilingListsPerModule.Sum(x => x.Value.Values.Sum(y => y.Count()));
                int numMixinsApplied = 0;
                SetCurrentMaxCallback?.Invoke(totalMixinsToApply);
                SetCurrentValueCallback?.Invoke(0);
                SetCurrentTaskIndeterminateCallback?.Invoke(false);
                SetCurrentTaskStringCallback?.Invoke(M3L.GetString(M3L.string_applyingMixins));

                void completedSingleApplicationCallback()
                {
                    var numdone = Interlocked.Increment(ref numMixinsApplied);
                    if (numdone > totalMixinsToApply)
                    {
                        M3Log.Warning(
                            $@"Error in progress calculation, numdone > total. Done: {numdone} Total: {totalMixinsToApply}");
                    }

                    SetCurrentValueCallback?.Invoke(numdone);
                    var numOverallDone = Interlocked.Add(ref OverallProgressValue, MIXIN_OVERALL_WEIGHT);
                    SetOverallValueCallback?.Invoke(numOverallDone);
                }

                //Mixins are ready to be applied
                Parallel.ForEach(compilingListsPerModule,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount > MaxModmakerCores
                            ? MaxModmakerCores
                            : Environment.ProcessorCount
                    }, mapping =>
                    {
                        //var dlcFolderName = ModmakerChunkNameToDLCFoldername(mapping.Key.ToString());
                        //var outdir = Path.Combine(mod.ModPath, HeaderToDefaultFoldername(mapping.Key), @"CookedPCConsole");
                        //Directory.CreateDirectory(outdir);
                        
                        MixinManager.ApplyMixinsToModule(mapping, mod.ModPath, completedSingleApplicationCallback, null);
                        
                        /*if (mapping.Key == ModJob.JobHeader.BASEGAME)
                        {
                            //basegame
                            foreach (var file in mapping.Value)
                            {
                                using var packageAsStream =
                                    VanillaDatabaseService.FetchBasegameFile(MEGame.ME3,
                                        Path.GetFileName(file.Key));
                                using var decompressedStream = MEPackage.GetDecompressedPackageStream(packageAsStream, true);
                                using var finalStream = MixinHandler.ApplyMixins(decompressedStream, file.Value, Settings.LogModMakerCompiler, completedSingleApplicationCallback);
                                M3Log.Information(@"Compressing package to mod directory: " + file.Key, Settings.LogModMakerCompiler);
                                finalStream.Position = 0;
                                var package = MEPackageHandler.OpenMEPackageFromStream(finalStream);
                                var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));
                                package.Save(outfile, true, true, false); //set to true once compression bugs are fixed
                                                                          //finalStream.WriteToFile(outfile);
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
                                using var finalStream = MixinHandler.ApplyMixins(packageAsStream, file.Value, Settings.LogModMakerCompiler, completedSingleApplicationCallback);
                                //as file comes from backup, we don't need to decompress it, it will always be decompressed in sfar
                                M3Log.Information(@"Compressing package to mod directory: " + file.Key, Settings.LogModMakerCompiler);
                                finalStream.Position = 0;
                                var package = MEPackageHandler.OpenMEPackageFromStream(finalStream);
                                var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));
                                package.Save(outfile, true, true, true);
                            }
                        }*/
                    });
                MixinHandler.FreeME3TweaksPatchData();
                M3Log.Information(@"Finished compiling Mixins.", Settings.LogModMakerCompiler);
            }
            else
            {
                M3Log.Information(@"This modmaker mod does not have a MixinData section. Skipping mixin compiler.", Settings.LogModMakerCompiler);
            }
        }

        int totalNumCoalescedFileChunks = 0;
        int numDoneCoalescedFileChunks = 0;

        private void compileCoalesceds(XDocument xmlDoc, Mod mod)
        {
            SetCurrentTaskStringCallback?.Invoke(M3L.GetString(M3L.string_compilingCoalescedFiles));
            List<XElement> jobCollection = new List<XElement>();
            var jobs = xmlDoc.XPathSelectElements(@"/ModMaker/ModData/*");
            if (jobs != null)
            {
                foreach (var job in jobs)
                {
                    if (job is XElement node)
                    {
                        M3Log.Information(@"Found coalesced modifier for DLC: " + node.Name,
                            Settings.LogModMakerCompiler);
                        var foldername = ModmakerChunkNameToDLCFoldername(job.Name.LocalName);
                        if (job.Name.LocalName == @"BASEGAME" || job.Name.LocalName == @"BALANCE_CHANGES" || job.Name.LocalName == @"TESTPATCH" || DLCFolders.Contains(foldername, StringComparer.InvariantCultureIgnoreCase))
                        {
                            jobCollection.Add(node);
                            totalNumCoalescedFileChunks += node.Elements().Count();
                        }
                    }
                }
            }

            SetCurrentMaxCallback?.Invoke(totalNumCoalescedFileChunks);
            //Todo: Precheck assets are available.

            Parallel.ForEach(jobCollection,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount > MaxModmakerCores
                        ? MaxModmakerCores
                        : Environment.ProcessorCount
                }, (xmlChunk) => compileCoalescedChunk(xmlChunk, mod));
            Debug.WriteLine($@"Progress at end of coalesced chunking: {OverallProgressValue}");
            M3Log.Information(@"Finished compiling coalesceds.", Settings.LogModMakerCompiler);
        }

        private bool compileCoalescedChunk(XElement xmlChunk, Mod mod)
        {
            var chunkName = xmlChunk.Name.LocalName;
            string loggingPrefix = $@"ModMaker Compiler [{chunkName}]";
            //var header = Enum.Parse(typeof(ModJob.JobHeader), chunkName);
            //string dlcFoldername = ModJob.GetHeadersToDLCNamesMap(MEGame.ME3)[header];
            var outPath = Directory.CreateDirectory(Path.Combine(mod.ModPath, chunkName)).FullName;
            //Debug.WriteLine(@"Compiling chunk: " + chunkName);

            //File fetch
            Dictionary<string, string> coalescedFilemapping = null;
            string coalescedFilename = null;
            if (chunkName == @"BASEGAME")
            {
                var coalPath = Path.Combine(BackupService.GetGameBackupPath(MEGame.ME3), @"BioGame", @"CookedPCConsole", @"Coalesced.bin");
                coalescedFilename = @"Coalesced.bin";
                if (File.Exists(coalPath))
                {
                    using FileStream fs = new FileStream(coalPath, FileMode.Open);
                    coalescedFilemapping = CoalescedConverter.DecompileGame3ToMemory(fs);
                }
                else
                {
                    M3Log.Error(@"Could not get file data for coalesced chunk BASEGAME as Coalesced.bin file was missing");
                    return false;
                }
            }
            else if (chunkName == @"BALANCE_CHANGES")
            {
                var serverCoalesced = M3Utilities.ExtractInternalFileToStream(@"ME3TweaksModManager.modmanager.me3tweaks.LiveIni.bin");
                coalescedFilemapping = CoalescedConverter.DecompileGame3ToMemory(serverCoalesced);
                coalescedFilename = @"ServerCoalesced.bin";
            }
            else
            {
                var dlcFolderName = ModmakerChunkNameToDLCFoldername(chunkName);
                var coalescedData = VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcFolderName, $@"Default_{dlcFolderName}.bin");
                coalescedFilename = $@"Default_{dlcFolderName}.bin";
                if (coalescedData != null)
                {
                    coalescedFilemapping = CoalescedConverter.DecompileGame3ToMemory(coalescedData);
                }
                else
                {
                    M3Log.Error(@"Could not get file data for coalesced chunk: " + chunkName);
                    return false;
                }
            }

            if (coalescedFilemapping != null)
            {
                // get filenames for chunk
                foreach (var fileNode in xmlChunk.Elements())
                {
                    //Debug.WriteLine($@"{loggingPrefix} {fileNode.Name.LocalName}");
                    var matchingCoalFile = coalescedFilemapping[fileNode.Name + @".xml"];
                    var coalFileDoc = XDocument.Parse(matchingCoalFile);
                    string updatedDocumentText = compileCoalescedChunkFile(coalFileDoc, fileNode, $@"{loggingPrefix}[{fileNode.Name}]: ");
                    coalescedFilemapping[fileNode.Name + @".xml"] = updatedDocumentText;
                }

                if (Settings.ModMakerAutoInjectCustomKeybindsOption && chunkName == @"BASEGAME" && File.Exists(KeybindsInjectorPanel.GetDefaultKeybindsOverride(MEGame.ME3)))
                {
                    M3Log.Information(@"Injecting keybinds file into mod: " + KeybindsInjectorPanel.GetDefaultKeybindsOverride(MEGame.ME3));
                    coalescedFilemapping[@"BioInput.xml"] = File.ReadAllText(KeybindsInjectorPanel.GetDefaultKeybindsOverride(MEGame.ME3));
                }

                M3Log.Information($@"{loggingPrefix} Recompiling coalesced file", Settings.LogModMakerCompiler);
                var newFileStream = CoalescedConverter.CompileFromMemory(coalescedFilemapping);


                var outFolder = Path.Combine(mod.ModPath, chunkName, @"CookedPCConsole");
                if (chunkName == @"BALANCE_CHANGES")
                {
                    outFolder = Path.Combine(mod.ModPath, chunkName);
                }
                Directory.CreateDirectory(outFolder);
                var outFile = Path.Combine(outFolder, coalescedFilename);

                newFileStream.WriteToFile(outFile);
                Interlocked.Add(ref OverallProgressValue, COALESCED_CHUNK_OVERALL_WEIGHT);
                SetOverallValueCallback?.Invoke(OverallProgressValue);
                M3Log.Information($@"{loggingPrefix} Compiled coalesced file, chunk finished", Settings.LogModMakerCompiler);
            }

            return true;
        }

        private const string OP_ADDITION = @"addition";
        private const string OP_SUBTRACTION = @"subtraction";
        private const string OP_ASSIGNMENT = @"assignment";
        private const string OP_MODIFY = @"modify"; //same as assignment, except used for array values

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
            var sectionsToHandle = modDeltaDocument.Elements(@"Section");
            foreach (var section in sectionsToHandle)
            {
                var sectionName = section.Attribute(@"name").Value;
                var operation = section.Attribute(@"operation").Value;
                if (operation == OP_ADDITION)
                {
                    var sectionsGroup = targetDocument.XPathSelectElement(@"/CoalesceAsset/Sections");
                    var newSection = new XElement(@"Section");
                    newSection.SetAttributeValue(@"name", sectionName);
                    sectionsGroup.Add(newSection);
                    M3Log.Information($@"{loggingPrefix}Added section: {sectionName}", Settings.LogModMakerCompiler);
                }
                else if (operation == OP_SUBTRACTION)
                {
                    var targetSection = targetDocument.XPathSelectElement($@"/CoalesceAsset/Sections/Section[@name='{sectionName}']");
                    if (targetSection != null)
                    {
                        targetSection.Remove();
                        M3Log.Warning($@"{loggingPrefix}Removed section: {sectionName}", Settings.LogModMakerCompiler);
                    }
                    else
                    {
                        M3Log.Warning($@"{loggingPrefix}Could not find section to remove: {sectionName}", Settings.LogModMakerCompiler);
                    }
                }
            }
            #endregion

            #region Properties - Assignments
            // Really old modmaker stuff did not assign operations to everything. The default was Assignment
            var deltaPropertyAssignments = modDeltaDocument.Elements(@"Property")
                .Where(x => x.Attribute(@"operation") == null || x.Attribute(@"operation").Value == OP_ASSIGNMENT);
            foreach (var deltaProperty in deltaPropertyAssignments)
            {
                var sectionName = deltaProperty.Attribute(@"path").Value;
                var propertyName = deltaProperty.Attribute(@"name").Value;
                var type = deltaProperty.Attribute(@"type").Value;
                var value = deltaProperty.Value;

                var targetElement = targetDocument.XPathSelectElement($@"/CoalesceAsset/Sections/Section[@name='{sectionName}']/Property[@name='{propertyName}']");
                if (targetElement == null)
                {
                    Debug.WriteLine($@"Not found {sectionName}']/Property[@name='{propertyName}' and type '{type}'");
                }
                else
                {
                    if (targetElement.Value != value)
                    {
                        targetElement.Value = value;
                        //targetElement.Attribute("type").Value = type; //i don't think this is necessary but is part of old modmaker code.
                        M3Log.Information($@"{loggingPrefix}Assigned new value to property {sectionName} => {propertyName}, new value: {value}", Settings.LogModMakerCompiler);
                    }
                    else
                    {
                        //Not assigned, same value.
                        M3Log.Information($@"{loggingPrefix}Skipping same-value for {sectionName} => {propertyName}", Settings.LogModMakerCompiler);
                    }
                }
            }
            #endregion

            #region Properties - Subtraction
            var deltaPropertySubtractions = modDeltaDocument.Elements(@"Property").Where(x => x.Attribute(@"operation") != null && x.Attribute(@"operation").Value == OP_SUBTRACTION);
            foreach (var deltaProperty in deltaPropertySubtractions)
            {
                var sectionName = deltaProperty.Attribute(@"path").Value;
                var propertyName = deltaProperty.Attribute(@"name").Value;
                var value = deltaProperty.Value;

                var targetElement = targetDocument.XPathSelectElement($@"/CoalesceAsset/Sections/Section[@name='{sectionName}']/Property[@name='{propertyName}']");
                if (targetElement == null)
                {
                    M3Log.Warning($@"{loggingPrefix}Could not find property to remove: {sectionName} => {propertyName}", Settings.LogModMakerCompiler);
                }
                else
                {
                    if (targetElement.Value == value)
                    {
                        targetElement.Remove();
                        M3Log.Information($@"{loggingPrefix}Removed property {sectionName} => {propertyName}", Settings.LogModMakerCompiler);
                    }
                    else
                    {
                        //Not assigned, same value.
                        M3Log.Warning($@"{loggingPrefix}Did not remove property, values did not match! {sectionName} => {propertyName}. Expected '{value}', found '{targetElement.Value}'", Settings.LogModMakerCompiler);
                    }
                }
            }
            #endregion

            #region Properties - Addition
            var deltaPropertyAdditions = modDeltaDocument.Elements(@"Property").Where(x => x.Attribute(@"operation") != null && x.Attribute(@"operation").Value == OP_ADDITION);
            foreach (var deltaProperty in deltaPropertyAdditions)
            {
                var sectionName = deltaProperty.Attribute(@"path").Value;
                var propertyName = deltaProperty.Attribute(@"name").Value;
                var value = deltaProperty.Value;
                var type = deltaProperty.Attribute(@"type").Value;

                var targetElement = targetDocument.XPathSelectElement($@"/CoalesceAsset/Sections/Section[@name='{sectionName}']");
                if (targetElement == null)
                {
                    M3Log.Warning($@"{loggingPrefix}Could not find property to remove: {sectionName} => {propertyName}", Settings.LogModMakerCompiler);
                }
                else
                {
                    var newSection = new XElement(@"Property", value);
                    newSection.SetAttributeValue(@"name", propertyName);
                    newSection.SetAttributeValue(@"type", type);
                    targetElement.Add(newSection);
                    M3Log.Information($@"{loggingPrefix}Added property {sectionName} => {propertyName}", Settings.LogModMakerCompiler);
                }
            }
            #endregion

            #region ArrayProperty
            var deltaArrayProperties = modDeltaDocument.Elements(@"ArrayProperty");
            foreach (var deltaProperty in deltaArrayProperties)
            {
                try
                {
                    var pathTokens = deltaProperty.Attribute(@"path").Value.Split('&');
                    var sectionName = pathTokens[0];
                    var propertyName = pathTokens[1];
                    var matchOnType = deltaProperty.Attribute(@"matchontype").Value;
                    var type = deltaProperty.Attribute(@"type").Value;
                    var value = deltaProperty.Value;
                    var arrayContainer = targetDocument.XPathSelectElement(
                        $@"/CoalesceAsset/Sections/Section[@name='{sectionName}']/Property[@name='{propertyName}']");

                    if (arrayContainer == null)
                    {
                        M3Log.Error(
                            $@"{loggingPrefix}Did not find arrayproperty @name='{sectionName}']/Property[@name='{propertyName}' and @type='{matchOnType}']");
                    }
                    else
                    {
                        //Log.Information($@"{loggingPrefix}Found array countainer {sectionName} => {propertyName}");
                        var operation = deltaProperty.Attribute(@"operation").Value;
                        if (operation == OP_ADDITION)
                        {
                            var newArrayElement = new XElement(@"Value", value);
                            newArrayElement.SetAttributeValue(@"type", type);
                            arrayContainer.Add(newArrayElement);
                            M3Log.Information($@"{loggingPrefix}Added array element {sectionName} => {propertyName} -> type({type}): {value}", Settings.LogModMakerCompiler);
                        }
                        else if (operation == OP_SUBTRACTION)
                        {
                            var matchingAlgorithm = deltaProperty.Attribute(@"arraytype").Value;
                            var values = arrayContainer.Descendants(@"Value");
                            var matchingItem = findArrayElementBasedOnAlgoritm(sectionName, propertyName, values,
                                matchingAlgorithm, matchOnType, value);
                            if (matchingItem == null)
                            {
                                M3Log.Warning($@"{loggingPrefix}Could not find array element to remove: {sectionName} => {propertyName} -> type({matchOnType}): {value}",
                                    Settings.LogModMakerCompiler);
                            }
                            else
                            {
                                matchingItem.Remove();
                                M3Log.Information($@"{loggingPrefix}Removed array element: {sectionName} => {propertyName} -> type({matchOnType}): {value}",
                                    Settings.LogModMakerCompiler);
                            }
                        }
                        else if (operation == OP_ASSIGNMENT || operation == OP_MODIFY)
                        {
                            //Algorithms based
                            var matchingAlgorithm = deltaProperty.Attribute(@"arraytype").Value;
                            var values = arrayContainer.Descendants(@"Value");
                            var matchingItem = findArrayElementBasedOnAlgoritm(sectionName, propertyName, values,
                                matchingAlgorithm, matchOnType, value);
                            if (matchingItem == null)
                            {
                                M3Log.Warning($@"Could not find matching element: {sectionName} => {propertyName}, type({type}), algorithm {matchingAlgorithm}",
                                    Settings.LogModMakerCompiler);
                            }
                            else
                            {
                                //Debug.WriteLine($"Found matching item {sectionName} => {propertyName}, type({type}), algorithm {matchingAlgorithm}");
                                if (matchingAlgorithm == @"wavelist")
                                {
                                    //On Jan 7 2020 I discovered a bug in the output code of ME3Tweaks ModMaker server publisher that has been present since late 2014.
                                    //The , between enemies in the wavelist lists would not be output if the enemy did not have emax set (max num on field). 
                                    //ME3CMM did not have issues with this as it's weak struct parser could parse the list 
                                    //and since I was building modmaker I opted to stress test my implementation
                                    //by making the client also parse and rebuild the string, even though this was not necessary as the assignment data was already known.
                                    //M3 does not parse the item beyond identification purposes, so an )( items will need to be substituted for ),(, but only for arraytype wavelist.
                                    matchingItem.Value = value.Replace(@")(", @"),("); //assign
                                }
                                else
                                {
                                    matchingItem.Value = value; //assign
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    M3Log.Error(@"Error applying delta property: " + e.Message);
                }
            }
            #endregion

            var numdone = Interlocked.Increment(ref numDoneCoalescedFileChunks);
            SetCurrentValueCallback?.Invoke(numdone);

            var numOverallDone = Interlocked.Add(ref OverallProgressValue, COALESCED_CHUNK_OVERALL_WEIGHT);
            SetOverallValueCallback?.Invoke(numOverallDone);
            return targetDocument.ToString();
        }

        /// <summary>
        /// This is the vanilla value for Plat Collectors Wave 5. It should be Do_Level4 but bioware set it to 3.
        /// </summary>
        private static readonly string CollectorsPlatWave5WrongText = "(Difficulty=DO_Level3,Enemies=( (EnemyType=\"WAVE_COL_Scion\"), (EnemyType=\"WAVE_COL_Praetorian\", MinCount=1, MaxCount=1), (EnemyType=\"WAVE_CER_Phoenix\", MinCount=2, MaxCount=2), (EnemyType=\"WAVE_CER_Phantom\", MinCount=3, MaxCount=3) ))"; //do not localize

        private int MIXIN_OVERALL_WEIGHT = 1;
        private int TLK_OVERALL_WEIGHT = 4;
        private int COALESCED_CHUNK_OVERALL_WEIGHT = 2;
        private List<string> DLCFolders;


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
                case @"exactvalue":
                    {
                        return values.FirstOrDefault(x => x.Value == value && matchOnType == x.Attribute(@"type").Value);
                    }
                case @"id":
                    {
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier(@"ID", matchOnType, element, newValues))
                            {
                                return element;
                            }
                        }
                        M3Log.Warning(@"Could not find element using ID algorithm for value " + value, Settings.LogModMakerCompiler);
                        break;
                    }
                case @"wavecost":
                case @"enemytype":
                    {
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier(@"EnemyType", matchOnType, element, newValues))
                            {
                                return element;
                            }
                        }
                        M3Log.Warning(@"Could not find element using enemytype/wavecost algorithm for value " + value, Settings.LogModMakerCompiler);
                        break;
                    }
                case @"biodifficulty":
                    {
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier(@"Category", matchOnType, element, newValues))
                            {
                                return element;
                            }
                        }
                        M3Log.Warning(@"Could not find element using category algorithm for value " + value, Settings.LogModMakerCompiler);
                    }
                    break;
                case @"wavelist":
                    {
                        //Collector Plat Wave 5 is set to DO_Level3 even though it should be 4.
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier(@"Difficulty", matchOnType, element, newValues))
                            {
                                return element;
                            }
                            else if (newValues[@"Difficulty"] == @"DO_Level4" && sectionName == @"sfxwave_horde_collector5 sfxwave_horde_collector" && propertyName == @"enemies" && element.Value == CollectorsPlatWave5WrongText)
                            {
                                Debug.WriteLine(@"Found wrong collectors wave 5 data from bioware, returning");
                                return element;
                            }
                        }
                        M3Log.Warning(@"Could not find element using wavelist algorithm for value " + value, Settings.LogModMakerCompiler);
                    }
                    break;
                case @"possessionwaves":
                case @"shareddifficulty":
                case @"wavebudget":
                    {
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier(@"Difficulty", matchOnType, element, newValues))
                            {
                                return element;
                            }
                        }
                        M3Log.Warning(@"Could not find element using shareddifficulty/wavebudget/possessionwaves algorithm for value " + value, Settings.LogModMakerCompiler);
                    }
                    break;
                case @"waveclass":
                    {
                        var newValues = StringStructParser.GetCommaSplitValues(value);
                        foreach (var element in values)
                        {
                            if (matchesOnIdentifier(@"WaveClassName", matchOnType, element, newValues))
                            {
                                return element;
                            }
                        }
                    }
                    M3Log.Warning(@"Could not find element using enemytype algorithm for value " + value, Settings.LogModMakerCompiler);
                    break;
                default:
                    M3Log.Error($@"Unknown array value matching algorithm: { matchingAlgorithm}. Ths modification of this value will be skipped: {sectionName} -> {propertyName} for {value}");
                    break;
            }
            return null;
        }
        private bool matchesOnIdentifier(string identifierKey, string matchOnType, XElement element, Dictionary<string, string> newValues)
        {
            var type = element.Attribute(@"type").Value;
            if (!type.Equals(matchOnType, StringComparison.InvariantCultureIgnoreCase)) return false;
            var elementValues = StringStructParser.GetCommaSplitValues(element.Value);
            return elementValues[identifierKey] == newValues[identifierKey];
        }

        private void finalizeModdesc(XDocument doc, Mod mod)
        {
            SetCurrentTaskStringCallback?.Invoke(M3L.GetString(M3L.string_finalizingMod));
            //Update moddesc
            IniData ini = new FileIniDataParser().ReadFile(mod.ModDescPath);
            var dirs = Directory.GetDirectories(mod.ModPath);

            foreach (var dir in dirs)
            {
                //automap
                var dirname = Path.GetFileName(dir);
                var headername = DefaultFoldernameToHeader(dirname).ToString();
                ini[headername][@"moddir"] = dirname;
                if (dirname != @"BALANCE_CHANGES")
                {
                    ini[headername][@"newfiles"] = @"CookedPCConsole";

                    string inGameDestdir;
                    if (dirname == @"BASEGAME")
                    {
                        inGameDestdir = @"BIOGame/CookedPCConsole";
                    }
                    else
                    {
                        //DLC
                        inGameDestdir = $@"BIOGame/DLC/{ModmakerChunkNameToDLCFoldername(dirname)}/CookedPCConsole";
                    }

                    ini[headername][@"replacefiles"] = inGameDestdir;
                    ini[headername][@"gamedirectorystructure"] = @"true";
                }
                else
                {
                    ini[headername][@"newfiles"] = @"ServerCoalesced.bin"; //BALANCE_CHANGES
                }
            }

            ini[@"ModInfo"][@"compiledagainst"] = doc.XPathSelectElement(@"/ModMaker/ModInfo/ModMakerVersion").Value;
            M3Log.Information(@"Writing finalized moddesc to library", Settings.LogModMakerCompiler);
            File.WriteAllText(mod.ModDescPath, ini.ToString());
        }

        public static string HeaderToDefaultFoldername(ModJob.JobHeader header)
        {
            switch (header)
            {
                case ModJob.JobHeader.RESURGENCE:
                    return @"MP1";
                case ModJob.JobHeader.REBELLION:
                    return @"MP2";
                case ModJob.JobHeader.EARTH:
                    return @"MP3";
                case ModJob.JobHeader.RETALIATION:
                    return @"MP4";
                case ModJob.JobHeader.RECKONING:
                    return @"MP5";
            }
            return header.ToString();
        }

        public static ModJob.JobHeader DefaultFoldernameToHeader(string foldername)
        {
            if (Enum.TryParse<ModJob.JobHeader>(foldername, out var header))
            {
                return header;
            }
            switch (foldername)
            {
                case @"MP1":
                    return ModJob.JobHeader.RESURGENCE;
                case @"MP2":
                    return ModJob.JobHeader.REBELLION;
                case @"MP3":
                    return ModJob.JobHeader.EARTH;
                case @"MP4":
                    return ModJob.JobHeader.RETALIATION;
                case @"MP5":
                    return ModJob.JobHeader.RECKONING;
            }
            throw new Exception(@"Unknown default foldername: " + foldername);
        }

        public static string ModmakerChunkNameToDLCFoldername(string chunkName)
        {
            switch (chunkName)
            {
                case @"BASEGAME":
                    return null; //kind of a hack. This is not a DLC folder but i don't want to return error log
                case @"RESURGENCE":
                case @"MP1":
                    return @"DLC_CON_MP1";
                case @"REBELLION":
                case @"MP2":
                    return @"DLC_CON_MP2";
                case @"EARTH":
                case @"MP3":
                    return @"DLC_CON_MP3";
                case @"RETALIATION":
                case @"MP4":
                    return @"DLC_CON_MP4";
                case @"RECKONING":
                case @"MP5":
                    return @"DLC_CON_MP5";
                case @"PATCH1":
                    return @"DLC_UPD_Patch01";
                case @"PATCH2":
                    return @"DLC_UPD_Patch02";
                //case @"BASEGAME":
                //    return @"Coalesced";
                case @"TESTPATCH":
                    return @"DLC_TestPatch"; //special case, must be handled 
                case @"FROM_ASHES":
                    return @"DLC_HEN_PR";
                case @"APPEARANCE":
                    return @"DLC_CON_APP01";
                case @"FIREFIGHT":
                    return @"DLC_CON_GUN01";
                case @"GROUNDSIDE":
                    return @"DLC_CON_GUN02";
                case @"EXTENDED_CUT":
                    return @"DLC_CON_END";
                case @"LEVIATHAN":
                    return @"DLC_EXP_Pack001";
                case @"OMEGA":
                    return @"DLC_EXP_Pack002";
                case @"CITADEL":
                    return @"DLC_EXP_Pack003";
                case @"CITADEL_BASE":
                    return @"DLC_EXP_Pack003_Base";
                case @"BALANCE_CHANGES":
                    return null; //This is not handled by this code. But it's not an error
                //Special case
                //case @"BALANCE_CHANGES":
                //    return @"ServerCoalesced";
                default:
                    M3Log.Error(@"Unknown chunk name: " + chunkName);
                    return null;
            }
        }



        /// <summary>
        /// Generates the initial mod folder and mod
        /// </summary>
        /// <param name="xmlDoc">mod document</param>
        /// <returns>Mod object</returns>
        private Mod GenerateLibraryModFromDocument(XDocument xmlDoc, string modPathOverride = null)
        {
            var hasError = xmlDoc.XPathSelectElement(@"/ModMaker/error");
            if (hasError != null)
            {
                M3Log.Error(@"Mod was not found server.");
                return null;
            }
            SetCompileStarted?.Invoke();
            modName = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/Name").Value;
            SetModNameCallback?.Invoke(modName);
            M3Log.Information(@"Compiling mod: " + modName);

            var modDev = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/Author").Value;
            var revisionElement = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/Revision");
            string modVersion = @"1";
            if (revisionElement != null)
            {
                modVersion = revisionElement.Value;
            }
            var modDescription = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/Description").Value;
            var modmakerServerVer = xmlDoc.XPathSelectElement(@"/ModMaker/ModInfo/ModMakerVersion").Value;

            //Write mod ini
            IniData ini = new IniData();
            ini[@"ModManager"][@"cmmver"] = App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture); //prevent commas
            ini[@"ModInfo"][@"game"] = @"ME3";
            ini[@"ModInfo"][@"modname"] = modName;
            ini[@"ModInfo"][@"moddev"] = modDev;
            ini[@"ModInfo"][@"moddesc"] = M3Utilities.ConvertNewlineToBr(modDescription);
            ini[@"ModInfo"][@"modver"] = modVersion;
            ini[@"ModInfo"][@"modid"] = code.ToString();
            ini[@"ModInfo"][@"compiledagainst"] = modmakerServerVer;
            ini[@"ModInfo"][@"modsite"] = @"https://me3tweaks.com/modmaker/mods/" + code;

            var outputDir = modPathOverride ?? Path.Combine(M3Utilities.GetME3ModsDirectory(), M3Utilities.SanitizePath(modName));
            M3Log.Information(@"Generating new mod directory: " + outputDir, Settings.LogModMakerCompiler);
            if (Directory.Exists(outputDir))
            {
                M3Utilities.DeleteFilesAndFoldersRecursively(outputDir);
            }
            //apparently system is too fast to respond
            Thread.Sleep(100);
            Directory.CreateDirectory(outputDir);

            var moddescPath = Path.Combine(outputDir, @"moddesc.ini");
            File.WriteAllText(moddescPath, ini.ToString());

            //Generate and load mod - it will be invalid as it has no jobs yet.
            Mod m = new Mod(moddescPath, MEGame.ME3, blankLoad: true);
            return m;
        }
    }
}
