using System.Diagnostics;
using System.Text;
using System.Windows.Input;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using ME3TweaksCore.Config;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.BasegameFileIdentification;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// In-Window content container for LE1 Coalesced Merge.
    /// </summary>
    public partial class LE1CoalescedMergePanel : MMBusyPanelBase
    {
        private GameTargetWPF CoalescedMergeTarget;

        public const string COALESCED_MERGE_PREFIX = @"CoalescedMerge-";
        public const string COALESCED_MERGE_EXTENSION = @".m3cd";

        public LE1CoalescedMergePanel(GameTargetWPF target)
        {
            if (target?.Game != MEGame.LE1)
            {
                throw new Exception(@"Cannot run coalesced merge panel on game that is not LE1");
            }
            this.CoalescedMergeTarget = target;
        }

        public static bool RunCoalescedMerge(GameTargetWPF target)
        {
            M3Log.Information($@"Performing Coaleseced Merge for game: {target.TargetPath}");
            var coalescedStream = M3Utilities.ExtractInternalFileToStream("ME3TweaksModManager.modmanager.merge.coalesced.LE1.Coalesced_INT.bin");
            var coalescedAssets = CoalescedConverter.DecompileLE1LE2ToAssets(coalescedStream, @"Coalesced_INT.bin");

            var dlcMountsInOrder = MELoadedDLC.GetDLCNamesInMountOrder(target.Game, target.TargetPath);
            foreach (var dlc in dlcMountsInOrder)
            {
                var dlcCookedPath = Path.Combine(M3Directories.GetDLCPath(target), dlc, target.Game.CookedDirName());
                var m3cds = Directory.GetFiles(dlcCookedPath, @"*" + COALESCED_MERGE_EXTENSION, SearchOption.TopDirectoryOnly)
                    .Where(x => Path.GetFileName(x).StartsWith(COALESCED_MERGE_PREFIX)).ToList(); // Find CoalescedMerge-*.m3cd files

                foreach (var m3cd in m3cds)
                {
                    M3Log.Information($@"Merging M3 Coalesced Delta {m3cd}");
                    var m3cdasset = ConfigFileProxy.LoadIni(m3cd);
                    ConfigMerge.PerformMerge(coalescedAssets, m3cdasset, target.Game);
                }
            }

            // Combine the assets
            var inis = new CaseInsensitiveDictionary<DuplicatingIni>();
            foreach (var asset in coalescedAssets)
            {
                inis[asset.Key] = CoalesceAsset.ToIni(asset.Value);
            }

            // Write out the coalesced file
            var coalPath = Path.Combine(M3Directories.GetCookedPath(target), @"Coalesced_INT.bin"); // Todo: Will need copied to other languages
            var compiledStream = CoalescedConverter.CompileLE1LE2FromMemory(inis);
            compiledStream.WriteToFile(coalPath);


            //if (supercedances.TryGetValue(@"PlotManagerUpdate.pmu", out var supercedanes))
            //{
            //    supercedanes.Reverse(); // list goes from highest to lowest. We want to build in lowest to highest
            //    StringBuilder sb = null;
            //    string currentFuncNum = null;
            //    var metaMaps = target.GetMetaMappedInstalledDLC(false);
            //    foreach (var pmuDLCName in supercedanes)
            //    {

            //        var uiName = metaMaps[pmuDLCName]?.ModName ?? TPMIService.GetThirdPartyModInfo(pmuDLCName, target.Game)?.modname ?? pmuDLCName;
            //        combinedNames.Add(uiName);
            //        var text = File.ReadAllLines(Path.Combine(M3Directories.GetDLCPath(target), pmuDLCName, target.Game.CookedDirName(), PLOT_MANAGER_UPDATE_FILENAME));
            //        foreach (var line in text)
            //        {
            //            if (line.StartsWith(@"public function bool F"))
            //            {
            //                if (sb != null)
            //                {
            //                    funcMap[currentFuncNum] = sb.ToString();
            //                    M3Log.Information($@"PlotSync: Adding function {currentFuncNum} from {pmuDLCName}");
            //                    currentFuncNum = null;
            //                }

            //                sb = new StringBuilder();
            //                sb.AppendLine(line);

            //                // Method name
            //                currentFuncNum = line.Substring(22);
            //                currentFuncNum = currentFuncNum.Substring(0, currentFuncNum.IndexOf('('));
            //                if (int.TryParse(currentFuncNum, out var num))
            //                {
            //                    if (num <= 0)
            //                    {
            //                        M3Log.Error($@"Skipping plot manager update: Conditional {num} is not a valid number for use. Values must be greater than 0 and less than 2 billion.");
            //                        TelemetryInterposer.TrackEvent(@"Bad plot manager function", new Dictionary<string, string>() {
            //                            { @"FunctionName", $@"F{currentFuncNum}" },
            //                            { @"DLCName", pmuDLCName }
            //                        });
            //                        sb = null;
            //                        return false;
            //                    }
            //                    else if (num.ToString().Length != currentFuncNum.Length)
            //                    {
            //                        M3Log.Error($@"Skipping plot manager update: Conditional {currentFuncNum} is not a valid number for use. Values must not contain leading zeros");
            //                        TelemetryInterposer.TrackEvent(@"Bad plot manager function", new Dictionary<string, string>() {
            //                            { @"FunctionName", $@"F{currentFuncNum}" },
            //                            { @"DLCName", pmuDLCName }
            //                        });
            //                        sb = null;
            //                        return false;
            //                    }
            //                }
            //                else
            //                {
            //                    M3Log.Error($@"Skipping plot manager update: Conditional {currentFuncNum} is not a valid number for use. Values must be greater than 0 and less than 2 billion.");
            //                    TelemetryInterposer.TrackEvent(@"Bad plot manager function", new Dictionary<string, string>() {
            //                        { @"FunctionName", $@"F{currentFuncNum}" },
            //                        { @"DLCName", pmuDLCName }
            //                    });
            //                    sb = null;
            //                    return false;
            //                }
            //            }
            //            else
            //            {
            //                sb?.AppendLine(line);
            //            }
            //        }

            //        // Add final, if any was found
            //        if (sb != null)
            //        {
            //            funcMap[currentFuncNum] = sb.ToString();
            //            M3Log.Information($@"PlotSync: Adding function {currentFuncNum} from {pmuDLCName}");
            //        }
            //    }
            //}

            //var pmPath = GetPlotManagerPath(target);
            //var vpm = M3Utilities.ExtractInternalFileToStream($@"ME3TweaksModManager.modmanager.plotmanager.{target.Game}.PlotManager.{(target.Game == MEGame.ME1 ? @"u" : @"pcc")}"); // do not localize
            //if (funcMap.Any())
            //{
            //    var plotManager = MEPackageHandler.OpenMEPackageFromStream(vpm, $@"PlotManager.{(target.Game == MEGame.ME1 ? @"u" : @"pcc")}"); // do not localize
            //    var clonableFunction = plotManager.Exports.FirstOrDefault(x => x.ClassName == @"Function");

            //    // STEP 1: ADD ALL NEW FUNCTIONS BEFORE WE INITIALIZE THE FILELIB.
            //    foreach (var v in funcMap)
            //    {
            //        var pmKey = $@"BioAutoConditionals.F{v.Key}";
            //        var exp = plotManager.FindExport(pmKey);
            //        if (exp == null)
            //        {
            //            // Adding a new conditional
            //            exp = EntryCloner.CloneEntry(clonableFunction);
            //            exp.ObjectName = new NameReference($@"F{v.Key}", 0);
            //            exp.FileRef.InvalidateLookupTable(); // We changed the name.

            //            // Reduces trash
            //            UFunction uf = ObjectBinary.From<UFunction>(exp);
            //            uf.Children = 0;
            //            uf.ScriptBytes = Array.Empty<byte>(); // No script data
            //            exp.WriteBinary(uf);
            //            M3Log.Information($@"Generated new blank conditional function export: {exp.UIndex} {exp.InstancedFullPath}", Settings.LogModInstallation);
            //        }
            //    }

            //    // Relink child chain
            //    UClass uc = ObjectBinary.From<UClass>(plotManager.FindExport(@"BioAutoConditionals"));
            //    uc.UpdateChildrenChain();
            //    uc.UpdateLocalFunctions();
            //    uc.Export.WriteBinary(uc);


            //    // STEP 2: UPDATE FUNCTIONS
            //    Stopwatch sw = Stopwatch.StartNew();
            //    var fl = new FileLib(plotManager);
            //    bool initialized = fl.Initialize(new RelativePackageCache() { RootPath = M3Directories.GetBioGamePath(target) }, target.TargetPath, canUseBinaryCache: false);
            //    if (!initialized)
            //    {
            //        M3Log.Error(@"Error initializing FileLib for plot manager sync:");
            //        foreach (var v in fl.InitializationLog.AllErrors) M3Log.Error(v.Message);
            //        throw new Exception(M3L.GetString(M3L.string_interp_fileLibInitFailedPlotManager, string.Join(Environment.NewLine, fl.InitializationLog.AllErrors.Select(x => x.Message)))); //force localize
            //    }
            //    sw.Stop();
            //    Debug.WriteLine($@"Took {sw.ElapsedMilliseconds}ms to load filelib");

            //    bool relinkChain = false;
            //    foreach (var v in funcMap)
            //    {
            //        var pmKey = $@"BioAutoConditionals.F{v.Key}";
            //        M3Log.Information($@"Updating conditional entry: {pmKey}", Settings.LogModInstallation);
            //        var exp = plotManager.FindExport(pmKey);

            //        (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(exp, v.Value, fl);
            //        if (log.AllErrors.Any())
            //        {
            //            M3Log.Error($@"Error compiling function {exp.InstancedFullPath}:");
            //            foreach (var l in log.AllErrors)
            //            {
            //                M3Log.Error(l.Message);
            //            }

            //            throw new Exception(M3L.GetString(M3L.string_interp_errorCompilingFunctionReason, exp, string.Join('\n', log.AllErrors.Select(x => x.Message))));
            //            return false;
            //        }
            //    }

            //    if (plotManager.IsModified)
            //    {
            //        plotManager.Save(pmPath, true);
            //        // Update local file DB
            //        var bgfe = new BasegameFileRecord(pmPath.Substring(target.TargetPath.Length + 1), (int)new FileInfo(pmPath).Length, target.Game, M3L.GetString(M3L.string_interp_plotManagerSyncForX, string.Join(@", ", combinedNames)), M3Utilities.CalculateMD5(pmPath));
            //        BasegameFileIdentificationService.AddLocalBasegameIdentificationEntries(new List<BasegameFileRecord>(new[] { bgfe }));
            //    }
            //}
            //else
            //{
            //    // Just write out vanilla.
            //    vpm.WriteToFile(pmPath);
            //}

            return true;
        }

        private static string GetPlotManagerPath(GameTargetWPF target)
        {
            switch (target.Game)
            {
                case MEGame.ME1:
                    return Path.Combine(M3Directories.GetCookedPath(target), @"PlotManager.u");
                case MEGame.LE1:
                case MEGame.ME2:
                case MEGame.LE2:
                    return Path.Combine(M3Directories.GetCookedPath(target), @"PlotManager.pcc");
            }

            return null;
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"CoalescedMerge");
            nbw.DoWork += (a, b) =>
            {
                RunCoalescedMerge(CoalescedMergeTarget);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }
    }
}
