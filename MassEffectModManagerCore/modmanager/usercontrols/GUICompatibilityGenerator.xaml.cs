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
            REQUIRED
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


        private void StartGuiCompatibilityScanner()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("GUICompatibilityScanner");
            bw.DoWork += (a, b) =>
            {
                //Ensure UI libraries



                var installedDLCMods = VanillaDatabaseService.GetInstalledDLCMods(target);
                var numMods = installedDLCMods.Count;
                var uiModInstalled = installedDLCMods.Intersect(DLCUIModFolderNames).Any();
                var dlcRoot = MEDirectories.DLCPath(target);
                if (uiModInstalled)
                {
                    installedDLCMods = installedDLCMods.Except(DLCUIModFolderNames).ToList();

                    if (installedDLCMods.Count < numMods && installedDLCMods.Count > 0)
                    {
                        //We have UI mod(s) installed and at least one other DLC mod.
                        var supercedanceList = getFileSupercedances();
                        //Find GUIs

                        foreach (var pair in supercedanceList)
                        {
                            var firstNonUIModFile = pair.Value.FirstOrDefault(x => !DLCUIModFolderNames.Contains(x));
                            if (firstNonUIModFile != null)
                            {
                                //Scan file.
                                var packagefile = Path.Combine(dlcRoot, pair.Key, target.Game == MEGame.ME3 ? "CookedPCConsole" : "CookedPC", firstNonUIModFile);
                                if (!File.Exists(packagefile)) throw new Exception("Package file for inspecting GUIs in was not found!");
                                var package = MEPackageHandler.OpenMEPackage(packagefile);
                                var guiExports = package.Exports.Where(x => !x.IsDefaultObject && x.ClassName == "GFxMovieInfo").ToList();
                                if (guiExports.Count > 0)
                                {
                                    //potential item needing replacement
                                    //Check GUI library to see if we have anything.
                                }
                            }

                            for (int i = 0; i < pair.Value.Count; i++)
                            {

                            }
                        }
                    }
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
