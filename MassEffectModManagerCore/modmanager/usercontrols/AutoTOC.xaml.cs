using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.objects.mod;
using MassEffectModManagerCore.ui;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// In-Window content container for AutoTOC. Most of this class was ported from Mod Manager Command Line Tools.
    /// </summary>
    public partial class AutoTOC : MMBusyPanelBase
    {
        private const string SFAR_SUBPATH = @"CookedPCConsole\Default.sfar";

        private enum AutoTOCMode
        {
            MODE_GAMEWIDE,
            MODE_MOD
        }

        //private AutoTOCMode mode;
        //private Mod modModeMod;
        private GameTarget gameWideModeTarget;

        public int Percent { get; private set; }
        public string ActionText { get; private set; }

        public AutoTOC(GameTarget target)
        {
            DataContext = this;
            this.gameWideModeTarget = target ?? throw new Exception(@"Null target specified for AutoTOC");
            InitializeComponent();
        }

        public AutoTOC(Mod mod)
        {
            DataContext = this;
            if (mod.Game != MEGame.ME3) throw new Exception(@"AutoTOC cannot be run on mods not designed for Mass Effect 3.");
            //this.modModeMod = mod;
            InitializeComponent();

        }

        private void RunModAutoTOC()
        {
            //Implement mod-only autotoc, for deployments
            // TODO actually do this
        }

        public static bool RunTOCOnGameTarget(GameTarget target, Action<int> percentDoneCallback = null)
        {
            Log.Information(@"Autotocing game: " + target.TargetPath);

            if (target.Game.IsLEGame())
            {
                TOCCreator.CreateTOCForGame(target.Game, percentDoneCallback, target.TargetPath);
                return true;
            }

            //get toc target folders, ensuring we clean up the inputs a bit.
            string baseDir = Path.GetFullPath(Path.Combine(target.TargetPath, @"BIOGame"));
            string dlcDirRoot = M3Directories.GetDLCPath(target);
            if (!Directory.Exists(dlcDirRoot))
            {
                Log.Error(@"Specified game directory does not appear to be a Mass Effect 3 root game directory (DLC folder missing).");
                return false;
            }

            var tocTargets = (new DirectoryInfo(dlcDirRoot)).GetDirectories().Select(x => x.FullName).Where(x => Path.GetFileName(x).StartsWith(@"DLC_", StringComparison.OrdinalIgnoreCase)).ToList();
            tocTargets.Add(baseDir);
            tocTargets.Add(Path.Combine(target.TargetPath, @"BIOGame\Patches\PCConsole\Patch_001.sfar"));

            //Debug.WriteLine("Found TOC Targets:");
            //tocTargets.ForEach(x => Debug.WriteLine(x));
            //Debug.WriteLine("=====Generating TOC Files=====");
            int done = 0;

            foreach (var tocTarget in tocTargets)
            {
                string sfar = Path.Combine(tocTarget, SFAR_SUBPATH);
                if (tocTarget.EndsWith(@".sfar"))
                {
                    //TestPatch
                    var watch = Stopwatch.StartNew();
                    DLCPackage dlc = new DLCPackage(tocTarget);
                    var tocResult = dlc.UpdateTOCbin();
                    watch.Stop();
                    if (tocResult == DLCPackage.DLCTOCUpdateResult.RESULT_UPDATE_NOT_NECESSARY)
                    {
                        Log.Information($@"TOC is already up to date in {tocTarget}");
                    }
                    else if (tocResult == DLCPackage.DLCTOCUpdateResult.RESULT_UPDATED)
                    {
                        var elapsedMs = watch.ElapsedMilliseconds;
                        Log.Information($@"{tocTarget} - Ran SFAR TOC, took {elapsedMs}ms");
                    }
                }
                else if (ME3Directory.OfficialDLCNames.ContainsKey(Path.GetFileName(tocTarget)))
                {
                    //Official DLC
                    if (File.Exists(sfar))
                    {
                        if (new FileInfo(sfar).Length == 32) //DLC is unpacked for sure
                        {
                            CreateUnpackedTOC(tocTarget);
                        }
                        else
                        {
                            //AutoTOC it - SFAR is not unpacked
                            var watch = System.Diagnostics.Stopwatch.StartNew();

                            DLCPackage dlc = new DLCPackage(sfar);
                            var tocResult = dlc.UpdateTOCbin();
                            watch.Stop();
                            if (tocResult == DLCPackage.DLCTOCUpdateResult.RESULT_ERROR_NO_ENTRIES)
                            {
                                Log.Information($@"No DLC entries in SFAR... Suspicious. Creating empty TOC for {tocTarget}");
                                CreateUnpackedTOC(tocTarget);
                            }
                            else if (tocResult == DLCPackage.DLCTOCUpdateResult.RESULT_UPDATE_NOT_NECESSARY)
                            {
                                Log.Information($@"TOC is already up to date in {tocTarget}");
                            }
                            else if (tocResult == DLCPackage.DLCTOCUpdateResult.RESULT_UPDATED)
                            {
                                var elapsedMs = watch.ElapsedMilliseconds;
                                Log.Information($@"{Path.GetFileName(tocTarget)} - Ran SFAR TOC, took {elapsedMs}ms");
                            }
                        }
                    }

                }
                else
                {
                    //TOC it unpacked style
                    // Console.WriteLine(foldername + ", - UNPACKED TOC");
                    CreateUnpackedTOC(tocTarget);
                }

                done++;
                percentDoneCallback?.Invoke((int)Math.Floor(done * 100.0 / tocTargets.Count));
            }
            return true;
        }

        public static void CreateUnpackedTOC(string dlcDirectory)
        {
            Log.Information(@"Creating unpacked toc for " + dlcDirectory);
            //#if DEBUG
            //            if (dlcDirectory.Contains(@"DLC_CON_END") || dlcDirectory.Contains(@"DLC_EXP_Pack002"))
            //            {
            //                Debugger.Break();
            //                throw new Exception(@"ASSERT ERROR: CREATING UNPACKED TOC FOR OFFICIAL DLC!");
            //            }
            //#endif
            var watch = System.Diagnostics.Stopwatch.StartNew();
            MemoryStream ms = TOCCreator.CreateTOCForDirectory(dlcDirectory);
            if (ms != null)
            {
                string tocPath = Path.Combine(dlcDirectory, @"PCConsoleTOC.bin");
                File.WriteAllBytes(tocPath, ms.ToArray());
                ms.Close();
                watch.Stop();
                var elapsedMs = watch.ElapsedMilliseconds;
                Log.Information($@"{Path.GetFileName(dlcDirectory)} - {dlcDirectory} Ran Unpacked TOC, took {elapsedMs}ms");
            }
            else
            {
                Log.Warning(@"Did not create TOC for " + dlcDirectory);
                watch.Stop();
            }
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"AutoTOC");
            nbw.DoWork += (a, b) =>
            {
                //if (mode == AutoTOCMode.MODE_GAMEWIDE)
                {
                    RunTOCOnGameTarget(gameWideModeTarget, x => Percent = x);
                }
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
