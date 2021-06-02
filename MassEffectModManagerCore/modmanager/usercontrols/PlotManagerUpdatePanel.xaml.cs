using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Documents;
using System.Windows.Input;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
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
            Log.Information(@"Updating PlotManager for game: {target.TargetPath}");
            var supercedances = M3Directories.GetFileSupercedances(target, new[] { @".pmu" });
            Dictionary<string, string> funcMap = new();
            if (supercedances.TryGetValue(@"PlotManagerUpdate.pmu", out var supercedanes))
            {
                StringBuilder sb = null;
                string currentFunc = null;
                foreach (var pmuDLCName in supercedanes)
                {
                    var text = File.ReadAllLines(Path.Combine(M3Directories.GetDLCPath(target), pmuDLCName, target.Game.CookedDirName(), @"PlotManagerUpdate.pmu"));
                    foreach (var line in text)
                    {
                        if (line.StartsWith(@"public function bool F"))
                        {
                            if (sb != null)
                            {
                                funcMap[currentFunc] = sb.ToString();
                                currentFunc = null;
                            }

                            sb = new StringBuilder();
                            sb.AppendLine(line);

                            // Method name
                            currentFunc = line.Substring(21);
                            currentFunc = currentFunc.Substring(0, currentFunc.IndexOf('('));
                        }
                        else if (sb != null)
                        {
                            sb.AppendLine(line);
                        }
                    }

                    // Add final, if any was found
                    if (sb != null)
                    {
                        funcMap[currentFunc] = sb.ToString();
                    }
                }
            }

            // They are all named .pcc for ease. ME1 doesn't use this extension but loader doesn't care
            var vpm = Utilities.ExtractInternalFileToStream($@"MassEffectModManagerCore.modmanager.plotmanager.{target.Game}.PlotManager.pcc");

            if (funcMap.Any())
            {

                var plotManager = MEPackageHandler.OpenMEPackageFromStream(vpm, @"PlotManager.pcc");
                Stopwatch sw = Stopwatch.StartNew();
                var fl = new FileLib(plotManager);
                bool initialized = fl.Initialize(new PackageCache()).Result;
                if (!initialized)
                {
                    throw new Exception(@"FileLib for PlotManagerUpdate could not initialize!");
                }
                sw.Stop();
                Debug.WriteLine($@"Took {sw.ElapsedMilliseconds}ms to load filelib");
                foreach (var v in funcMap)
                {
                    var exp = plotManager.FindExport($@"BioAutoConditionals.{v.Key}");
                    if (exp == null)
                    {
                        // ADD ITEM HERE
                        Debug.WriteLine(@"NOT IMPLEMENTED!");
                        continue;
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
