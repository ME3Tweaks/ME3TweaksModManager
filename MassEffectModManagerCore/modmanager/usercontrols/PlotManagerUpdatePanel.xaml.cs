using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using System.Windows.Input;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// In-Window content container for Plot Manager Update.
    /// </summary>
    public partial class PlotManagerUpdatePanel : MMBusyPanelBase
    {
        private GameTarget PlotManagerUpdateTarget;

        public PlotManagerUpdatePanel(GameTarget target)
        {
            this.PlotManagerUpdateTarget = target ?? throw new Exception(@"Null target specified for PlotManagerUpdatePanel");
            InitializeComponent();
        }

        public static bool RunPlotManagerUpdate(GameTarget target)
        {
            Log.Information($@"Updating PlotManager for game: {target.TargetPath}");
            var supercedances = M3Directories.GetFileSupercedances(target, new[] { @".pmu" });
            Dictionary<string, string> funcMap = new();
            List<string> combinedNames = new List<string>();

            if (supercedances.TryGetValue(@"PlotManagerUpdate.pmu", out var supercedanes))
            {
                supercedanes.Reverse(); // list goes from highest to lowest. We want to build in lowest to highest
                StringBuilder sb = null;
                string currentFuncNum = null;
                var metaMaps = M3Directories.GetMetaMappedInstalledDLC(target, false);
                foreach (var pmuDLCName in supercedanes)
                {

                    var uiName = metaMaps[pmuDLCName]?.ModName ?? ThirdPartyServices.GetThirdPartyModInfo(pmuDLCName, target.Game)?.modname ?? pmuDLCName;
                    combinedNames.Add(uiName);
                    var text = File.ReadAllLines(Path.Combine(M3Directories.GetDLCPath(target), pmuDLCName, target.Game.CookedDirName(), @"PlotManagerUpdate.pmu"));
                    foreach (var line in text)
                    {
                        if (line.StartsWith(@"public function bool F"))
                        {
                            if (sb != null)
                            {
                                funcMap[currentFuncNum] = sb.ToString();
                                currentFuncNum = null;
                            }

                            sb = new StringBuilder();
                            sb.AppendLine(line);

                            // Method name
                            currentFuncNum = line.Substring(22);
                            currentFuncNum = currentFuncNum.Substring(0, currentFuncNum.IndexOf('('));
                            if (int.TryParse(currentFuncNum, out var num))
                            {
                                if (num <= 0)
                                {
                                    Log.Error($@"Skipping plot manager update: Conditional {num} is not a valid number for use. Values must be greater than 0 and less than 2 billion.");
                                    Analytics.TrackEvent(@"Bad plot manager function", new Dictionary<string, string>() {
                                        { @"FunctionName", $@"F{currentFuncNum}" },
                                        { @"DLCName", pmuDLCName }
                                    });
                                    sb = null;
                                    return false;
                                }
                                else if (num.ToString().Length != currentFuncNum.Length)
                                {
                                    Log.Error($@"Skipping plot manager update: Conditional {currentFuncNum} is not a valid number for use. Values must not contain leading zeros");
                                    Analytics.TrackEvent(@"Bad plot manager function", new Dictionary<string, string>() {
                                        { @"FunctionName", $@"F{currentFuncNum}" },
                                        { @"DLCName", pmuDLCName }
                                    });
                                    sb = null;
                                    return false;
                                }
                            }
                            else
                            {
                                Log.Error($@"Skipping plot manager update: Conditional {currentFuncNum} is not a valid number for use. Values must be greater than 0 and less than 2 billion.");
                                Analytics.TrackEvent(@"Bad plot manager function", new Dictionary<string, string>() {
                                    { @"FunctionName", $@"F{currentFuncNum}" },
                                    { @"DLCName", pmuDLCName }
                                });
                                sb = null;
                                return false;
                            }
                        }
                        else
                        {
                            sb?.AppendLine(line);
                        }
                    }

                    // Add final, if any was found
                    if (sb != null)
                    {
                        funcMap[currentFuncNum] = sb.ToString();
                    }
                }
            }

            var pmPath = GetPlotManagerPath(target);
            var vpm = Utilities.ExtractInternalFileToStream($@"MassEffectModManagerCore.modmanager.plotmanager.{target.Game}.PlotManager.{(target.Game == MEGame.ME1 ? @"u" : @"pcc")}"); // do not localize
            if (funcMap.Any())
            {
                var plotManager = MEPackageHandler.OpenMEPackageFromStream(vpm, $@"PlotManager.{(target.Game == MEGame.ME1 ? @"u" : @"pcc")}"); // do not localize
                Stopwatch sw = Stopwatch.StartNew();
                var fl = new FileLib(plotManager);
                bool initialized = fl.Initialize(new RelativePackageCache() { RootPath = M3Directories.GetBioGamePath(target) }).Result;
                if (!initialized)
                {
                    throw new Exception(@"FileLib for PlotManagerUpdate could not initialize!");
                }
                sw.Stop();
                Debug.WriteLine($@"Took {sw.ElapsedMilliseconds}ms to load filelib");

                bool relinkChain = false;
                foreach (var v in funcMap)
                {
                    var pmKey = $@"BioAutoConditionals.F{v.Key}";
                    var exp = plotManager.FindExport(pmKey);
                    if (exp == null)
                    {
                        // Adding a new conditional
                        var expToClone = plotManager.Exports.FirstOrDefault(x => x.ClassName == @"Function");
                        exp = EntryCloner.CloneEntry(expToClone);
                        // Reduces trash
                        UFunction uf = ObjectBinary.From<UFunction>(exp);
                        uf.Children = 0;
                        exp.WriteBinary(uf);
                        relinkChain = true;
                        CLog.Information($@"Generated new conditional entry: {exp.UIndex} {pmKey}", Settings.LogModInstallation);
                    }
                    else
                    {
                        CLog.Information($@"Updating conditional entry: {pmKey}", Settings.LogModInstallation);
                    }

                    (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(exp, v.Value, fl);
                    if (log.AllErrors.Any())
                    {
                        Log.Error($@"Error compiling function {exp.InstancedFullPath}:");
                        foreach (var l in log.AllErrors)
                        {
                            Log.Error(l.Message);
                        }

                        throw new Exception(M3L.GetString(M3L.string_interp_errorCompilingFunctionReason, exp, string.Join('\n', log.AllErrors.Select(x => x.Message))));
                        return false;
                    }
                }

                if (relinkChain)
                {
                    UClass uc = ObjectBinary.From<UClass>(plotManager.FindExport(@"BioAutoConditionals"));
                    uc.UpdateChildrenChain();
                    uc.UpdateLocalFunctions();
                    uc.Export.WriteBinary(uc);
                }

                if (plotManager.IsModified)
                {
                    plotManager.Save(pmPath, true);
                    // Update local file DB
                    var bgfe = new BasegameFileIdentificationService.BasegameCloudDBFile(pmPath.Substring(target.TargetPath.Length + 1), (int)new FileInfo(pmPath).Length, target.Game, M3L.GetString(M3L.string_interp_plotManagerSyncForX,string.Join(@", ", combinedNames)), Utilities.CalculateMD5(pmPath));
                    BasegameFileIdentificationService.AddLocalBasegameIdentificationEntries(new List<BasegameFileIdentificationService.BasegameCloudDBFile>(new[] { bgfe }));
                }
            }
            else
            {
                // Just write out vanilla.
                vpm.WriteToFile(pmPath);
            }


            return true;
        }

        private static string GetPlotManagerPath(GameTarget target)
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
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"PlotManagerUpdate");
            nbw.DoWork += (a, b) =>
            {
                RunPlotManagerUpdate(PlotManagerUpdateTarget);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }
    }
}
