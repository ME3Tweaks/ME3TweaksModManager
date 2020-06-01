using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using ME3Explorer.Packages;
using static MassEffectModManagerCore.modmanager.Mod;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.windows;
using MassEffectModManagerCore.ui;
using ME3Explorer.Unreal;
using Microsoft.AppCenter.Analytics;
using SevenZip;
using static MassEffectModManagerCore.modmanager.windows.StarterKitGeneratorWindow;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using MassEffectModManagerCore.modmanager.localizations;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for GUICompatibilityGenerator.xaml
    /// </summary>
    public partial class GUICompatibilityGenerator : MMBusyPanelBase
    {
        private GameTarget target;
        public int Percent { get; set; } = 0;

        public string ActionString { get; set; } = M3L.GetString(M3L.string_preparingToScanGame);

        public string ActionSubstring { get; set; } = M3L.GetString(M3L.string_pleaseWait);

        //CEM (not sure why I had this). Will enable if required
        //private string[] doNotPatchFiles = { @"BioD_CitCas.pcc" };
        public GUICompatibilityGenerator(GameTarget target)
        {
            Log.Information(@"Opening GUI compatibility generator");
            if (target.Game != MEGame.ME3) throw new Exception($@"Cannot generate compatibility mods for {target.Game}");
            DataContext = this;
            this.target = target;
            InitializeComponent();
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void OnPanelVisible()
        {
            var installedDLCMods = VanillaDatabaseService.GetInstalledDLCMods(target);
            var uiModInstalled = installedDLCMods.Intersect(DLCUIModFolderNames).Any();
            if (uiModInstalled)
            {
                var result = M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogWhatGuiCompatPackIsFor), M3L.GetString(M3L.string_confirmGeneration), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    OnClosing(DataEventArgs.Empty);
                    return;
                }
                Log.Information(@"Starting GUI compatibility scanner");
                StartGuiCompatibilityScanner();
            }
            else
            {
                Log.Information(@"No UI mods are installed. Cannot run GUI Compat Generator");
                M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogNoUiModsAreInstalled), M3L.GetString(M3L.string_noUiModsAreInstalled), MessageBoxButton.OK, MessageBoxImage.Error);
                OnClosing(DataEventArgs.Empty);
            }
        }

        public static readonly string[] DLCUIModFolderNames =
        {
            @"DLC_CON_XBX",
            @"DLC_CON_UIScaling",
            // ME3 Ultrawide someday?
            @"DLC_CON_UIScaling_Shared"
        };

        private static readonly string[] DLCUIModFolderNamesIncludingPatch =
        {
            @"DLC_CON_XBX",
            @"DLC_CON_UIScaling",
            @"DLC_CON_UIScaling_Shared",
            //  ME3 Ultrawide someday?
            @"DLC_MOD_" + UI_MOD_NAME
        };

        public enum GUICompatibilityThreadResult
        {
            NO_UI_MODS_INSTALLED,
            NOT_REQUIRED,
            REQUIRED,
            INVALID_UI_MOD_CONFIG,
            NO_UI_LIBRARY,
            GENERATED_PACK
        }



        private int getPercent(long done, long total)
        {
            if (total == 0) return 0;
            if (done == total) return 100;
            return (int)(done * 100.0 / total);
        }


        private static readonly string M3_UILIBRARY_ROOT = @"https://me3tweaks.com/modmanager/tools/uilibrary/m3/";
        public const string UI_MOD_NAME = @"GUICompatibilityPack";

        /// <summary>
        /// Gets the path to the GUI library specified by the DLC name. Returns null if the library is not found and could not be downloaded.
        /// If Download is specified this call should run on a background thread only.
        /// </summary>
        /// <param name="dlcname">DLC mod to lookup library for</param>
        /// <param name="download">Download library if missing. If this is false and library is missing the value returned will be null</param>
        /// <returns>Path to library, null if it does not exist.</returns>
        public static string GetUILibraryPath(string dlcname, bool download, Action<long, long> progressCallback = null)
        {
            string libraryFolder = Path.Combine(Utilities.GetAppDataFolder(), @"UIModLibrary");
            string libraryPath = Path.Combine(libraryFolder, dlcname + @".zip");
            if (File.Exists(libraryPath)) return libraryPath;

            if (!Directory.Exists(libraryFolder) && !download) return null;
            if (!File.Exists(libraryPath) && !download) return null;

            if (download)
            {
                Directory.CreateDirectory(libraryFolder);
                Log.Information(@"Downloading UI library for " + dlcname);
                var downloaded = OnlineContent.DownloadToMemory(M3_UILIBRARY_ROOT + dlcname + @".zip", progressCallback);
                if (downloaded.errorMessage == null)
                {
                    File.WriteAllBytes(libraryPath, downloaded.result.ToArray());
                    Log.Information(@"Downloaded UI library for " + dlcname);
                    return libraryPath;
                }
                else
                {
                    Log.Error(@"Error downloading UI library: " + downloaded.errorMessage);
                    return null;
                }
            }

            return null;
        }

        private void StartGuiCompatibilityScanner()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"GUICompatibilityScanner");
            bw.DoWork += (a, b) =>
            {
                Percent = 0;
                ActionString = M3L.GetString(M3L.string_preparingCompatGenerator);
                ActionSubstring = M3L.GetString(M3L.string_pleaseWait);
                var installedDLCMods = VanillaDatabaseService.GetInstalledDLCMods(target);
                var numTotalDLCMods = installedDLCMods.Count;
                var uiModInstalled = installedDLCMods.Intersect(DLCUIModFolderNames).Any();
                var dlcRoot = MEDirectories.DLCPath(target);
                if (uiModInstalled)
                {
                    var nonUIinstalledDLCMods = installedDLCMods.Except(DLCUIModFolderNamesIncludingPatch).ToList();

                    if (nonUIinstalledDLCMods.Count < numTotalDLCMods && nonUIinstalledDLCMods.Count > 0)
                    {
                        //Get UI library
                        bool xbxLibrary = installedDLCMods.Contains(@"DLC_CON_XBX");
                        bool uiscalinglibrary = installedDLCMods.Contains(@"DLC_CON_UIScaling");
                        if (!xbxLibrary && !uiscalinglibrary) uiscalinglibrary = installedDLCMods.Contains(@"DLC_CON_UIScaling_Shared");
                        if (xbxLibrary && uiscalinglibrary)
                        {
                            //can't have both! Not supported.
                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                Log.Error(@"Cannot make compat pack: Both ISM and SP Controller are installed, this is not supported.");
                                M3L.ShowDialog(window, M3L.GetString(M3L.string_dialogCannotGenerateCompatPackInvalidConfig), M3L.GetString(M3L.string_invalidConfiguration), MessageBoxButton.OK, MessageBoxImage.Error);
                                OnClosing(DataEventArgs.Empty);
                            });
                            b.Result = GUICompatibilityThreadResult.INVALID_UI_MOD_CONFIG;
                            return;
                        }

                        void progressCallback(long done, long total)
                        {
                            ActionString = M3L.GetString(M3L.string_downloadingUiLibrary);
                            ActionSubstring = xbxLibrary ? @"DLC_CON_XBX" : @"DLC_CON_UIScaling";
                            Percent = getPercent(done, total);
                        }

                        var uiLibraryPath = GetUILibraryPath(xbxLibrary ? @"DLC_CON_XBX" : @"DLC_CON_UIScaling", true, progressCallback);
                        if (uiLibraryPath == null)
                        {
                            Log.Error(@"Required UI library could not be downloaded.");
                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                M3L.ShowDialog(window, M3L.GetString(M3L.string_cannotGeneratorCompatPackCouldNotDownload), M3L.GetString(M3L.string_couldNotAcquireUiLibrary), MessageBoxButton.OK, MessageBoxImage.Error);
                                OnClosing(DataEventArgs.Empty);
                            });
                            b.Result = GUICompatibilityThreadResult.NO_UI_LIBRARY;
                            return;
                        }

                        //Open UI library
                        SevenZipExtractor libraryArchive = new SevenZipExtractor(uiLibraryPath);
                        List<string> libraryGUIs = libraryArchive.ArchiveFileData.Where(x => !x.IsDirectory).Select(x => x.FileName.Substring(Path.GetFileNameWithoutExtension(uiLibraryPath).Length + 1)).Select(x => x.Substring(0, x.Length - 4)).ToList(); //remove / on end too

                        //We have UI mod(s) installed and at least one other DLC mod.
                        var supercedanceList = MEDirectories.GetFileSupercedances(target).Where(x => x.Value.Any(x => !DLCUIModFolderNamesIncludingPatch.Contains(x))).ToDictionary(p => p.Key, p => p.Value);

                        //Find GUIs

                        ConcurrentDictionary<string, string> filesToBePatched = new ConcurrentDictionary<string, string>(); //Dictionary because there is no ConcurrentList. Keys and values are idenitcal.
                        ActionString = M3L.GetString(M3L.string_scanningForGuiExports);
                        ActionSubstring = M3L.GetString(M3L.string_pleaseWait);
                        Percent = 0;
                        int done = 0;
                        string singlesuffix = M3L.GetString(M3L.string_singularFile);
                        string pluralsuffix = M3L.GetString(M3L.string_pluralFiles);
                        Parallel.ForEach(supercedanceList, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, (pair) =>
                        {
                            var firstNonUIModDlc = pair.Value.FirstOrDefault(x => !DLCUIModFolderNamesIncludingPatch.Contains(x));

                            if (firstNonUIModDlc != null)
                            {
                                //Scan file.
                                var packagefile = Path.Combine(dlcRoot, firstNonUIModDlc, target.Game == MEGame.ME3 ? @"CookedPCConsole" : @"CookedPC", pair.Key);
                                Log.Information(@"Scanning file for GFXMovieInfo exports: " + packagefile);
                                if (!File.Exists(packagefile)) throw new Exception($@"Package file for inspecting GUIs in was not found: {packagefile}");
                                var package = MEPackageHandler.OpenMEPackage(packagefile);
                                var guiExports = package.Exports.Where(x => !x.IsDefaultObject && x.ClassName == @"GFxMovieInfo").ToList();
                                if (guiExports.Count > 0)
                                {
                                    //potential item needing replacement
                                    //Check GUI library to see if we have anything.
                                    foreach (var export in guiExports)
                                    {
                                        if (libraryGUIs.Contains(export.GetFullPath, StringComparer.InvariantCultureIgnoreCase))
                                        {
                                            //match
                                            filesToBePatched[packagefile] = packagefile;
                                            ActionSubstring = M3L.GetString(M3L.string_interp_XFilesNeedToBePatched, filesToBePatched.Count.ToString());
                                            Log.Information($@"{firstNonUIModDlc} {pair.Key} has GUI export that is in UI library, marking for patching. Trigger: {export.GetFullPath}");
                                            break;
                                        }
                                    }
                                }
                            }
                            Interlocked.Increment(ref done);
                            Percent = getPercent(done, supercedanceList.Count);
                        });

                        if (filesToBePatched.Count > 0)
                        {
                            Log.Information(@"A GUI compatibility patch is required for this game configuration");
                            b.Result = GUICompatibilityThreadResult.REQUIRED;
                            var generatedMod = GenerateCompatibilityPackForFiles(nonUIinstalledDLCMods, filesToBePatched.Keys.ToList(), libraryArchive);
                            b.Result = GUICompatibilityThreadResult.GENERATED_PACK;
                            Application.Current.Dispatcher.Invoke(delegate { ((MainWindow)window).LoadMods(generatedMod); }); //reload to this mod
                            return;
                        }
                    }

                    Log.Information(@"A GUI compatibility patch is not required for this game configuration");
                    b.Result = GUICompatibilityThreadResult.NOT_REQUIRED;
                }
                else
                {
                    Log.Information(@"No UI mods are installed - no GUI compatibility pack required");
                    b.Result = GUICompatibilityThreadResult.NO_UI_MODS_INSTALLED;
                }
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Result is GUICompatibilityThreadResult gctr)
                {
                    Analytics.TrackEvent(@"Generated a UI compatibility pack", new Dictionary<string, string>() { { @"Result", gctr.ToString() } });
                    OnClosing(DataEventArgs.Empty);
                    if (gctr == GUICompatibilityThreadResult.NOT_REQUIRED)
                    {
                        M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_dialogNoCompatPackRequired), M3L.GetString(M3L.string_noCompatPackRequired), MessageBoxButton.OK);
                    }
                }
                else
                {
                    throw new Exception(@"GUI Compatibility generator thread did not return a result! Please report this to ME3Tweaks");
                }
            };
            bw.RunWorkerAsync();
        }

        private object modGeneratedSignaler = new object();

        private Mod GenerateCompatibilityPackForFiles(List<string> dlcModList, List<string> filesToBePatched, SevenZipExtractor uiArchive)
        {
            dlcModList = dlcModList.Select(x =>
            {
                var tpmi = ThirdPartyServices.GetThirdPartyModInfo(x, MEGame.ME3);
                if (tpmi == null) return x;
                return tpmi.modname;
            }).ToList();
            string dlcs = string.Join("\n - ", dlcModList); // do not localize

            StarterKitOptions sko = new StarterKitOptions
            {
                ModName = M3L.GetString(M3L.string_guiCompatibilityPack),
                ModDescription = M3L.GetString(M3L.string_interp_compatPackModDescription, dlcs, DateTime.Now.ToString()),
                ModDeveloper = App.AppVersionHR,
                ModDLCFolderName = UI_MOD_NAME,
                ModGame = MEGame.ME3,
                ModInternalName = @"UI Mod Compatibility Pack",
                ModInternalTLKID = 1420400890,
                ModMountFlag = EMountFileFlag.ME3_SPOnly_NoSaveFileDependency,
                ModMountPriority = 31050,
                ModURL = null,
                ModModuleNumber = 0
            };

            Mod generatedMod = null;

            void modGenerated(Mod mod)
            {
                generatedMod = mod;
                lock (modGeneratedSignaler)
                {
                    Monitor.Pulse(modGeneratedSignaler);
                }
            }

            void skUicallback(string text)
            {
                ActionSubstring = text;
            }

            StarterKitGeneratorWindow.CreateStarterKitMod(sko, skUicallback, modGenerated);
            lock (modGeneratedSignaler)
            {
                Monitor.Wait(modGeneratedSignaler);
            }

            //Mod has been generated.

            string outputPath = Path.Combine(generatedMod.ModPath, @"DLC_MOD_" + UI_MOD_NAME, @"CookedPCConsole");
            ActionString = M3L.GetString(M3L.string_preparingUiLibrary);
            ActionSubstring = M3L.GetString(M3L.string_decompressingData);

            int done = 0;

            CaseInsensitiveDictionary<byte[]> uiLibraryData = new CaseInsensitiveDictionary<byte[]>();
            var filesToDecompress = uiArchive.ArchiveFileData.Where(x => x.FileName.EndsWith(@".swf")).ToList();
            foreach (var f in filesToDecompress)
            {
                MemoryStream decompressedStream = new MemoryStream();
                uiArchive.ExtractFile(f.Index, decompressedStream);
                string fname = f.FileName.Substring(f.FileName.IndexOf('\\') + 1);
                fname = fname.Substring(0, fname.IndexOf(@".swf", StringComparison.InvariantCultureIgnoreCase));

                uiLibraryData[fname] = decompressedStream.ToArray();
                done++;
                Percent = getPercent(done, filesToDecompress.Count);
            }

            ActionString = M3L.GetString(M3L.string_patchingFiles);
            Percent = 0;
            done = 0;
            string singlesuffix = M3L.GetString(M3L.string_singularFile);
            string pluralsuffix = M3L.GetString(M3L.string_pluralFiles);

            // Logging
            Log.Information(@"The following files will be promoted in the compatibility pack:");
            foreach (var file in filesToBePatched)
            {
                Log.Information(@" - " + file);
            }

            foreach (var file in filesToBePatched)
            {
                Log.Information(@"Patching file: " + file);
                ActionSubstring = Path.GetFileName(file);
                var package = MEPackageHandler.OpenMEPackage(file);
                if (package == null)
                {
                    Log.Error(@"package object is null!!!");
                }
                var guiExports = package.Exports.Where(x => !x.IsDefaultObject && x.ClassName == @"GFxMovieInfo").ToList();
                if (guiExports.Count > 0)
                {
                    //potential item needing replacement
                    //Check GUI library to see if we have anything.
                    foreach (var export in guiExports)
                    {
                        if (uiLibraryData.TryGetValue(export.GetFullPath, out var newData))
                        {
                            Log.Information(@" >> Patching export " + export.GetFullPath);
                            //Patching this export.
                            var exportProperties = export.GetProperties();
                            var rawData = exportProperties.GetProp<ArrayProperty<ByteProperty>>(@"RawData");
                            if (rawData == null)
                            {
                                Log.Error("Rawdata is null!!");
                            }
                            rawData.Clear();
                            rawData.AddRange(newData.Select(x => new ByteProperty(x))); //This will be terribly slow. Need to port over new ME3Exp binary data handler
                            export.WriteProperties(exportProperties);
                        }
                        else
                        {
                            Debug.WriteLine(@"Not patching gui export, file not in library: " + export.GetFullPath);
                        }
                    }

                    var outpath = Path.Combine(outputPath, Path.GetFileName(package.FilePath));
                    if (package.IsModified)
                    {
                        Log.Information(@"Saving patched package to " + outpath);
                        package.save(outpath, true);
                        done++;
                        ActionSubstring = M3L.GetString(M3L.string_interp_patchedXY, done.ToString(), done == 1 ? singlesuffix : pluralsuffix);
                    }
                    else
                    {
                        done++;
                        Log.Information(@"File was patched but data did not change! " + outpath);
                    }

                    Percent = getPercent(done, filesToBePatched.Count);
                }
                else
                {
                    throw new Exception($@"Error: {Path.GetFileName(file)} doesn't contain GUI exports! This shouldn't have been possible.");
                }
            }

            return generatedMod;
        }
    }
}