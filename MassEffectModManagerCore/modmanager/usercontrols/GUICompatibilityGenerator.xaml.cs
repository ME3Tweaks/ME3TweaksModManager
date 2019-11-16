using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ME3Explorer.Packages;
using static MassEffectModManagerCore.modmanager.Mod;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using SevenZip;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for GUICompatibilityGenerator.xaml
    /// </summary>
    public partial class GUICompatibilityGenerator : MMBusyPanelBase
    {
        private GameTarget target;
        public GUICompatibilityGenerator(GameTarget target)
        {
            if (target.Game != MEGame.ME3) throw new Exception("Cannot generate compatibility mods for " + target.Game);
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
            StartGuiCompatibilityScanner();
        }

        private static readonly string[] DLCUIModFolderNames =
        {
            "DLC_CON_XBX",
            "DLC_MOD_UIScaling",
            "DLC_MOD_UIScaling_Shared"
        };

        public enum GUICompatibilityThreadResult
        {
            NO_UI_MODS_INSTALLED,
            NOT_REQUIRED,
            REQUIRED,
            INVALID_UI_MOD_CONFIG,
            NO_UI_LIBRARY
        }

        private Dictionary<string, List<string>> getFileSupercedances()
        {
            Mod.MEGame game = target.Game;
            //make dictionary from basegame files
            var fileListMapping = new CaseInsensitiveDictionary<List<string>>();
            var directories = MELoadedFiles.GetEnabledDLC(target).OrderBy(dir => MELoadedFiles.GetMountPriority(dir, target.Game));
            foreach (string directory in directories)
            {
                var dlc = Path.GetFileName(directory);
                if (MEDirectories.OfficialDLC(target.Game).Contains(dlc)) continue; //skip
                foreach (string filePath in MELoadedFiles.GetCookedFiles(target.Game, directory, false))
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName != null && fileName.RepresentsPackageFilePath())
                    {
                        if (fileListMapping.TryGetValue(fileName, out var supercedingList))
                        {
                            supercedingList.Insert(0, dlc);
                        }
                        else
                        {
                            fileListMapping[fileName] = new List<string>(new[] { dlc });
                        }
                    }
                }
            }

            return fileListMapping;
        }


        private static readonly string M3_UILIBRARY_ROOT = "https://me3tweaks.com/modmanager/tools/uilibrary/m3/";

        /// <summary>
        /// Gets the path to the GUI library specified by the DLC name. Returns null if the library is not found and could not be downloaded.
        /// If Download is specified this call should run on a background thread only.
        /// </summary>
        /// <param name="dlcname">DLC mod to lookup library for</param>
        /// <param name="download">Download library if missing. If this is false and library is missing the value returned will be null</param>
        /// <returns>Path to library, null if it does not exist.</returns>
        public static string GetUILibraryPath(string dlcname, bool download)
        {
            string libraryFolder = Path.Combine(Utilities.GetAppDataFolder(), "UIModLibrary");
            string libraryPath = Path.Combine(libraryFolder, dlcname + ".zip");
            if (File.Exists(libraryPath)) return libraryPath;

            if (!Directory.Exists(libraryFolder) && !download) return null;
            if (!File.Exists(libraryPath) && !download) return null;

            if (download)
            {
                Directory.CreateDirectory(libraryFolder);
                Log.Information("Downloading UI library for " + dlcname);
                var downloaded = OnlineContent.DownloadToMemory(M3_UILIBRARY_ROOT + dlcname + ".zip");
                if (downloaded.errorMessage == null)
                {
                    File.WriteAllBytes(libraryPath, downloaded.result.ToArray());
                    Log.Information("Downloaded UI library for " + dlcname);
                    return libraryPath;
                }
                else
                {
                    Log.Error("Error downloading UI library: " + downloaded.errorMessage);
                    return null;
                }
            }
            return null;
        }

        private void StartGuiCompatibilityScanner()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("GUICompatibilityScanner");
            bw.DoWork += (a, b) =>
            {
                var installedDLCMods = VanillaDatabaseService.GetInstalledDLCMods(target);
                var numTotalDLCMods = installedDLCMods.Count;
                var uiModInstalled = installedDLCMods.Intersect(DLCUIModFolderNames).Any();
                var dlcRoot = MEDirectories.DLCPath(target);
                if (uiModInstalled)
                {


                    var nonUIinstalledDLCMods = installedDLCMods.Except(DLCUIModFolderNames).ToList();

                    if (nonUIinstalledDLCMods.Count < numTotalDLCMods && nonUIinstalledDLCMods.Count > 0)
                    {
                        //Get UI library
                        bool xbxLibrary = installedDLCMods.Contains("DLC_CON_XBX");
                        bool uiscalinglibrary = installedDLCMods.Contains("DLC_CON_UIScaling");
                        if (!xbxLibrary && !uiscalinglibrary) uiscalinglibrary = installedDLCMods.Contains("DLC_CON_UIScaling_Shared");
                        if (xbxLibrary && uiscalinglibrary)
                        {
                            //can't have both! Not supported.
                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                Xceed.Wpf.Toolkit.MessageBox.Show(window, "Cannot generate a compatibility pack: Both Interface Scaling Mod and SP Controller support are installed. These mods are not compatible with each other. You must use one or the other.", "Invalid configuration", MessageBoxButton.OK, MessageBoxImage.Error);
                                OnClosing(DataEventArgs.Empty);
                            });
                            b.Result = GUICompatibilityThreadResult.INVALID_UI_MOD_CONFIG;
                            return;
                        }

                        var uiLibraryPath = GetUILibraryPath(xbxLibrary ? "DLC_CON_XBX" : "DLC_CON_UIScaling", true);
                        if (uiLibraryPath == null)
                        {
                            Application.Current.Dispatcher.Invoke(delegate
                            {
                                Xceed.Wpf.Toolkit.MessageBox.Show(window, "Cannot generate a compatibility pack: The required UI library could not be downloaded from ME3Tweaks. See the log for more information.", "Could not acquire UI library", MessageBoxButton.OK, MessageBoxImage.Error);
                                OnClosing(DataEventArgs.Empty);
                            });
                            b.Result = GUICompatibilityThreadResult.NO_UI_LIBRARY;
                            return;
                        }

                        //Open UI library
                        SevenZipExtractor libraryArchive = new SevenZipExtractor(uiLibraryPath);
                        List<string> libraryGUIs = libraryArchive.ArchiveFileData.Where(x => !x.IsDirectory).Select(x => x.FileName.Substring(Path.GetFileNameWithoutExtension(uiLibraryPath).Length + 1)).Select(x => x.Substring(0, x.Length - 4)).ToList(); //remove / on end too

                        //We have UI mod(s) installed and at least one other DLC mod.
                        var supercedanceList = getFileSupercedances();
                        //Find GUIs

                        List<string> filesToBePatched = new List<string>();
                        foreach (var pair in supercedanceList)
                        {
                            var firstNonUIModDlc = pair.Value.FirstOrDefault(x => !DLCUIModFolderNames.Contains(x));
                            if (firstNonUIModDlc != null)
                            {
                                //Scan file.
                                var packagefile = Path.Combine(dlcRoot, firstNonUIModDlc, target.Game == MEGame.ME3 ? "CookedPCConsole" : "CookedPC", pair.Key);
                                if (!File.Exists(packagefile)) throw new Exception("Package file for inspecting GUIs in was not found: " + packagefile);
                                Log.Information("Scanning file for GFXMovieInfo exports: " + packagefile);
                                var package = MEPackageHandler.OpenMEPackage(packagefile);
                                var guiExports = package.Exports.Where(x => !x.IsDefaultObject && x.ClassName == "GFxMovieInfo").ToList();
                                if (guiExports.Count > 0)
                                {
                                    //potential item needing replacement
                                    //Check GUI library to see if we have anything.
                                    foreach (var export in guiExports)
                                    {
                                        if (libraryGUIs.Contains(export.GetFullPath, StringComparer.InvariantCultureIgnoreCase))
                                        {
                                            //match
                                            filesToBePatched.Add(packagefile);
                                            Log.Information(firstNonUIModDlc + " " + pair.Key + " has GUI export that is in UI library, marking for patching. Trigger: " + export.GetFullPath);
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (filesToBePatched.Count > 0)
                        {
                            Log.Information("A GUI compatibility patch is required for this game configuration");
                            b.Result = GUICompatibilityThreadResult.REQUIRED;
                            return;
                        }
                    }
                    Log.Information("A GUI compatibility patch is not required for this game configuration");
                    b.Result = GUICompatibilityThreadResult.NOT_REQUIRED;
                }
                else
                {
                    Log.Information("No UI mods are installed - no GUI compatibility pack required");
                    b.Result = GUICompatibilityThreadResult.NO_UI_MODS_INSTALLED;
                }
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Result is GUICompatibilityThreadResult gctr)
                {

                }
                else
                {
                    throw new Exception("GUI Compatibility generator thread did not return a result!");
                }
            };
            bw.RunWorkerAsync();
        }
    }
}
