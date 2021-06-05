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
            if (supercedances.TryGetValue(@"PlotManagerUpdate.pmu", out var supercedanes))
            {
                StringBuilder sb = null;
                string currentFuncNum = null;
                foreach (var pmuDLCName in supercedanes)
                {
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
                            currentFuncNum = line.Substring(21);
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
                                }
                                else if (num.ToString().Length != currentFuncNum.Length)
                                {
                                    Log.Error($@"Skipping plot manager update: Conditional {currentFuncNum} is not a valid number for use. Values must not contain leading zeros");
                                    Analytics.TrackEvent(@"Bad plot manager function", new Dictionary<string, string>() {
                                        { @"FunctionName", $@"F{currentFuncNum}" },
                                        { @"DLCName", pmuDLCName }
                                    });
                                    sb = null;
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

            var vpm = Utilities.ExtractInternalFileToStream($@"MassEffectModManagerCore.modmanager.plotmanager.{target.Game}.PlotManager.{(target.Game == MEGame.ME1 ? @"u" : @"pcc")}");

            if (funcMap.Any())
            {

                var plotManager = MEPackageHandler.OpenMEPackageFromStream(vpm, $@"PlotManager.{(target.Game == MEGame.ME1 ? @"u" : @"pcc")}");
                Stopwatch sw = Stopwatch.StartNew();
                var fl = new FileLib(plotManager);
                bool initialized = fl.Initialize(new PackageCache()).Result;
                if (!initialized)
                {
                    throw new Exception(@"FileLib for PlotManagerUpdate could not initialize!");
                }
                sw.Stop();
                Debug.WriteLine($@"Took {sw.ElapsedMilliseconds}ms to load filelib");

                bool relinkChain = false;
                foreach (var v in funcMap)
                {
                    var exp = plotManager.FindExport($@"BioAutoConditionals.{v.Key}");
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
                    }

                    (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(exp, v.Value, fl);
                    if (log.AllErrors.Any())
                    {
                        Log.Error($@"Error compiling function {exp.InstancedFullPath}:");
                        foreach (var l in log.AllErrors)
                        {
                            Log.Error(l.Message);
                        }

                        // Is this right? [0]?
                        throw new Exception($"Error compiling function {exp}: {log.AllErrors[0].Message}");
                        return false;
                    }
                }

                if (relinkChain)
                {
                    UClass uc = ObjectBinary.From<UClass>(plotManager.FindExport("BioAutoConditionals"));
                    uc.UpdateChildrenChain();
                    uc.Export.WriteBinary(uc);
                }
                plotManager.Save(GetPlotManagerPath(target), true);
            }
            else
            {
                // Just write out vanilla.
                vpm.WriteToFile(GetPlotManagerPath(target));
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
