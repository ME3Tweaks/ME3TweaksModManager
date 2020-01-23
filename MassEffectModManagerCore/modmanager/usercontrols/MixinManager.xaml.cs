using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Linq;
using MassEffectModManagerCore.GameDirectories;
using Serilog;
using System.Threading.Tasks;
using System.Globalization;
using IniParser.Model;
using ME3Explorer.Packages;
using MassEffectModManagerCore.modmanager.helpers;
using System.Threading;
using MassEffectModManagerCore.modmanager.windows;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for MixinManager.xaml
    /// </summary>
    public partial class MixinManager : MMBusyPanelBase
    {
        public ObservableCollectionExtended<Mixin> AvailableOfficialMixins { get; set; } = new ObservableCollectionExtended<Mixin>();
        public Mixin SelectedMixin { get; set; }
        public bool OperationInProgress { get; set; }
        public long ProgressBarValue { get; set; }
        public long ProgressBarMax { get; set; } = 100; //default
        public string BottomLeftMessage { get; set; } = "Select Mixins to compile";
        public bool AtLeastOneMixinSelected => AvailableOfficialMixins.Any(x => x.UISelectedForUse);
        public MixinManager()
        {
            DataContext = this;
            MixinHandler.LoadME3TweaksPackage();
            AvailableOfficialMixins.ReplaceAll(MixinHandler.ME3TweaksPackageMixins.OrderBy(x => x.PatchName));

            var backupPath = Utilities.GetGameBackupPath(Mod.MEGame.ME3);
            if (backupPath != null)
            {
                var dlcPath = MEDirectories.DLCPath(backupPath, Mod.MEGame.ME3);
                var headerTranslation = ModJob.GetHeadersToDLCNamesMap(Mod.MEGame.ME3);
                foreach (var mixin in AvailableOfficialMixins)
                {
                    mixin.UIStatusChanging += MixinUIStatusChanging;
                    if (mixin.TargetModule == ModJob.JobHeader.TESTPATCH)
                    {
                        string biogame = MEDirectories.BioGamePath(backupPath);
                        var sfar = Path.Combine(biogame, "Patches", "PCConsole", "Patch_001.sfar");
                        if (File.Exists(sfar))
                        {
                            mixin.CanBeUsed = true;
                        }
                    }
                    else if (mixin.TargetModule != ModJob.JobHeader.BASEGAME)
                    {
                        //DLC
                        var resolvedPath = Path.Combine(dlcPath, headerTranslation[mixin.TargetModule]);
                        if (Directory.Exists(resolvedPath))
                        {
                            mixin.CanBeUsed = true;
                        }
                    }
                    else
                    {
                        //BASEGAME
                        mixin.CanBeUsed = true;
                    }
                }
            }
            else
            {
                BottomLeftMessage = "No game backup of ME3 is available. Mixins cannot be used without a backup.";
            }

            ResetMixinsUIState();
            LoadCommands();
            InitializeComponent();
        }

        private void MixinUIStatusChanging(object sender, EventArgs e)
        {
            TriggerPropertyChangedFor(nameof(AtLeastOneMixinSelected));
        }

        private void ResetMixinsUIState()
        {
            foreach (var m in AvailableOfficialMixins)
            {
                m.UISelectedForUse = true; //DEBUG ONLY
            }
        }

        public ICommand CloseCommand { get; set; }
        public ICommand ToggleSelectedMixinCommand { get; set; }
        public ICommand CompileAsNewModCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
            ToggleSelectedMixinCommand = new GenericCommand(ToggleSelectedMixin, MixinIsSelected);
            CompileAsNewModCommand = new GenericCommand(CompileAsNewMod, CanCompileAsNewMod);
        }

        private bool CanCompileAsNewMod() => SelectedMixin != null && !OperationInProgress;

        private void CompileAsNewMod()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("MixinManager CompileAsNewModThread");
            List<string> failedApplications = new List<string>();
            var modname = "MixinTest";
            var modpath = Path.Combine(Utilities.GetME3ModsDirectory(), modname);
            if (Directory.Exists(modpath))
            {
                Utilities.DeleteFilesAndFoldersRecursively(modpath);
            }
            nbw.DoWork += (a, b) =>
            {
                BottomLeftMessage = "Compiling Mixins...";
                OperationInProgress = true;
                //DEBUG STUFF
                int MaxModmakerCores = 1;

                var mixins = AvailableOfficialMixins.Where(x => x.UISelectedForUse).ToList();
                MixinHandler.LoadPatchDataForMixins(mixins); //before dynamic
                var compilingListsPerModule = MixinHandler.GetMixinApplicationList(mixins);
                ProgressBarMax = mixins.Count();
                ProgressBarValue = 0;
                int numdone = 0;
                void completedSingleApplicationCallback()
                {
                    var val = Interlocked.Increment(ref numdone);
                    ProgressBarValue = val;
                }

                void failedApplicationCallback(string str)
                {
                    failedApplications.Add(str);
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
                            var dlcFolderName = ModMakerCompiler.ModmakerChunkNameToDLCFoldername(mapping.Key.ToString());
                            var outdir = Path.Combine(modpath, ModMakerCompiler.HeaderToDefaultFoldername(mapping.Key), @"CookedPCConsole");
                            Directory.CreateDirectory(outdir);
                            if (mapping.Key == ModJob.JobHeader.BASEGAME)
                            {
                                //basegame
                                foreach (var file in mapping.Value)
                                {
                                    try
                                    {
                                        using var packageAsStream =
                                            VanillaDatabaseService.FetchBasegameFile(Mod.MEGame.ME3,
                                                Path.GetFileName(file.Key));
                                        packageAsStream.WriteToFile(@"C:\users\dev\desktop\compressed.pcc");
                                        using var decompressedStream = MEPackage.GetDecompressedPackageStream(packageAsStream);
                                        decompressedStream.WriteToFile(@"C:\users\dev\desktop\decompressed.pcc");

                                        using var finalStream = MixinHandler.ApplyMixins(decompressedStream, file.Value,
                                            completedSingleApplicationCallback, failedApplicationCallback);
                                        CLog.Information("Compressing package to mod directory: " + file.Key, Settings.LogModMakerCompiler);
                                        finalStream.Position = 0;
                                        var package = MEPackageHandler.OpenMEPackage(finalStream);
                                        var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));
                                        package.save(outfile, true);
                                        //finalStream.WriteToFile(outfile);
                                        //File.WriteAllBytes(outfile, finalStream.ToArray());
                                    } catch (Exception e)
                                    {
                                        Log.Error($"Error in mixin application for file {mapping.Value}: {e.Message}");
                                    }
                                }
                            }
                            else
                            {
                                //dlc
                                var dlcPackage = VanillaDatabaseService.FetchVanillaSFAR(dlcFolderName); //do not have to open file multiple times.
                                foreach (var file in mapping.Value)
                                {
                                    try
                                    {
                                        using var packageAsStream =
                                            VanillaDatabaseService.FetchFileFromVanillaSFAR(dlcFolderName, file.Key, forcedDLC: dlcPackage);
                                        using var decompressedStream = MEPackage.GetDecompressedPackageStream(packageAsStream);
                                        using var finalStream = MixinHandler.ApplyMixins(decompressedStream, file.Value, completedSingleApplicationCallback, failedApplicationCallback);
                                        CLog.Information("Compressing package to mod directory: " + file.Key, Settings.LogModMakerCompiler);
                                        finalStream.Position = 0;
                                        var package = MEPackageHandler.OpenMEPackage(finalStream);
                                        var outfile = Path.Combine(outdir, Path.GetFileName(file.Key));
                                        package.save(outfile, true);
                                    } catch (Exception e)
                                    {
                                        Log.Error($"Error in mixin application for file {mapping.Value}: {e.Message}");
                                    }
                                    //finalStream.WriteToFile(outfile);
                                }
                            }
                        });

                MixinHandler.FreeME3TweaksPatchData();

                //Generate moddesc
                IniData ini = new IniData();
                ini[@"ModManager"][@"cmmver"] = App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture); //prevent commas
                ini[@"ModInfo"][@"game"] = @"ME3";
                ini[@"ModInfo"][@"modname"] = modname;
                ini[@"ModInfo"][@"moddev"] = App.AppVersionHR;
                ini[@"ModInfo"][@"moddesc"] = "Compiled from the following mixins:";
                ini[@"ModInfo"][@"modver"] = "1.0";

                generateRepaceFilesMapping(ini, modpath);
                File.WriteAllText(Path.Combine(modpath, @"moddesc.ini"), ini.ToString());
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                OperationInProgress = false;
                ClearMixinHandler();
                if (failedApplications.Count > 0)
                {
                    var ld = new ListDialog(failedApplications, "Failed to apply all Mixins", "The following Mixins failed to apply.", mainwindow);
                    ld.ShowDialog();
                }

                ClosePanel();
                OnClosing(new DataEventArgs(modpath)); //update to new mod path

            };
            nbw.RunWorkerAsync();
        }

        private void generateRepaceFilesMapping(IniData ini, string modpath)
        {
            var dirs = Directory.GetDirectories(modpath);
            foreach (var dir in dirs)
            {
                //automap
                var dirname = Path.GetFileName(dir);
                var headername = ModMakerCompiler.defaultFoldernameToHeader(dirname).ToString();
                ini[headername]["moddir"] = dirname;
                if (dirname != "BALANCE_CHANGES")
                {
                    ini[headername]["newfiles"] = "CookedPCConsole";

                    string inGameDestdir;
                    if (dirname == "BASEGAME")
                    {
                        inGameDestdir = @"BIOGame/CookedPCConsole";
                    }
                    else
                    {
                        //DLC
                        inGameDestdir = $@"BIOGame/DLC/{ModMakerCompiler.ModmakerChunkNameToDLCFoldername(dirname)}/CookedPCConsole";
                    }

                    ini[headername]["replacefiles"] = inGameDestdir;
                    ini[headername]["gamedirectorystructure"] = "true";
                }
                else
                {
                    ini[headername]["newfiles"] = "ServerCoalesced.bin"; //BALANCE_CHANGES
                }
            }
        }

        private void ToggleSelectedMixin()
        {
            SelectedMixin.UISelectedForUse = !SelectedMixin.UISelectedForUse;
        }

        private bool MixinIsSelected() => SelectedMixin != null;
        private bool CanClosePanel() => true;

        private void ClosePanel()
        {
            ClearMixinHandler();
            OnClosing(DataEventArgs.Empty);
        }

        private void ClearMixinHandler()
        {
            foreach (var mixin in AvailableOfficialMixins)
            {
                mixin.UIStatusChanging -= MixinUIStatusChanging;
            }
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            // throw new NotImplementedException();
        }

        public override void OnPanelVisible()
        {
            // throw new NotImplementedException();
        }

        public void OnSelectedMixinChanged()
        {

        }


        //private void MixinList_ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (e.AddedItems.Count > 0)
        //    {
        //        SelectedMixin = (Mixin)e.AddedItems[0];
        //    }
        //    else
        //    {
        //        SelectedMixin = null;
        //    }
        //}
    }
}
