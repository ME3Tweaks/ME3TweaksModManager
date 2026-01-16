using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.headmorph;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.windows;
using ME3TweaksModManager.modmanager.windows.input;
using System.Windows.Input;

namespace ME3TweaksModManager
{
    /// <summary>
    /// Partial class for MainWindow - Headmorph installation and management
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Command to begin installing a standard headmorph file
        /// </summary>
        public ICommand InstallHeadmorphCommand { get; set; }

        /// <summary>
        /// Command to begin installing an M3-packaged headmorph
        /// </summary>
        public ICommand ApplyM3HeadmorphCommand { get; set; }

        /// <summary>
        /// Initializes headmorph-related commands
        /// </summary>
        private void LoadHeadmorphCommands()
        {
            InstallHeadmorphCommand = new GenericCommand(BeginInstallingHeadmorph, CanInstallHeadmorph);
            ApplyM3HeadmorphCommand = new GenericCommand(BeginInstallingM3Headmorph, CanInstallM3Headmorph);
        }

        /// <summary>
        /// Checks if a headmorph can be installed to the current target
        /// </summary>
        /// <returns>True if headmorph installation is possible</returns>
        private bool CanInstallHeadmorph()
        {
            return SelectedGameTarget != null && SelectedGameTarget.Game.IsMEGame() && SelectedGameTarget.Game != MEGame.ME1;
        }

        /// <summary>
        /// Checks if an M3 headmorph can be installed from the selected mod
        /// </summary>
        /// <returns>True if M3 headmorph installation is possible</returns>
        private bool CanInstallM3Headmorph()
        {
            if (!CanInstallHeadmorph()) return false;
            if (SelectedMod == null) return false;
            var headmorphJob = SelectedMod.GetJob(ModJob.JobHeader.HEADMORPHS);
            if (headmorphJob == null || !headmorphJob.HeadMorphFiles.Any()) return false;
            return true;
        }

        /// <summary>
        /// Begins the process of installing a standard headmorph file
        /// </summary>
        private void BeginInstallingHeadmorph()
        {
            if (!CanInstallHeadmorph()) return;

            // Select headmorph file
            string filter = @"*.ron";
            if (SelectedGameTarget.Game.IsGame2())
                filter += @";*.me2headmorph";
            if (SelectedGameTarget.Game.IsGame3())
                filter += @";*.me3headmorph";

            Microsoft.Win32.OpenFileDialog m = new Microsoft.Win32.OpenFileDialog
            {
                Title = M3L.GetString(M3L.string_selectHeadmorphFile),
                Filter = M3L.GetString(M3L.string_headmorphFiles) + $@"|{filter}"
            };
            var result = m.ShowDialog(this);
            if (result != true)
                return;

            InstallHeadmorphToTarget(m.FileName, SelectedGameTarget);
        }

        /// <summary>
        /// Begins the process of installing an M3-packaged headmorph from the selected mod
        /// </summary>
        private void BeginInstallingM3Headmorph()
        {
            if (!CanInstallM3Headmorph()) return;

            // Show dialog
            var selectorDialog = new HeadmorphSelectorDialog(this, SelectedMod);
            if (selectorDialog.ShowDialog() == true && selectorDialog.SelectedHeadmorph != null)
            {
                var morph = selectorDialog.SelectedHeadmorph;
                if (morph.RequiredDLC.Any())
                {
                    // We must check DLC first
                    var installedDLC = SelectedGameTarget.GetMetaMappedInstalledDLC();
                    foreach (var dlc in morph.RequiredDLC)
                    {
                        var modNameStr =
                            TPMIService.GetThirdPartyModInfo(dlc.DLCFolderName.Key, SelectedGameTarget.Game)?.modname ??
                            dlc.DLCFolderName;
                        if (installedDLC.TryGetValue(dlc.DLCFolderName.Key, out MetaCMM metaCmm))
                        {
                            if (dlc.MinVersion != null)
                            {
                                // No version info found
                                if (metaCmm == null)
                                {
                                    // DLC installed but not by mod manager
                                    M3Log.Error(
                                        $@"Required DLC {dlc.DLCFolderName} is installed but Mod Manager could not read the version information; the mod may not have been installed by Mod Manager. We cannot verify this requirement is met; thus we are rejecting the install");
                                    M3L.ShowDialog(this,
                                        M3L.GetString(M3L.string_interp_headmorphRequiresDLCCouldNotDetermine, modNameStr, dlc.MinVersion, modNameStr),
                                        M3L.GetString(M3L.string_prerequesiteNotMet), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                                    return;
                                }

                                // We could not parse the version
                                if (!Version.TryParse(metaCmm.Version, out var modVersion))
                                {
                                    M3Log.Error(
                                        $@"Required DLC {dlc.DLCFolderName} is installed but could not parse its version: {metaCmm.Version}. We cannot verify this requirement is met; thus we are rejecting the install");
                                    M3L.ShowDialog(this,
                                        M3L.GetString(M3L.string_interp_headmorphRequiresDLCBadVersionString, modNameStr, dlc.MinVersion, metaCmm.Version),
                                        M3L.GetString(M3L.string_prerequesiteNotMet), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                                    return;
                                }

                                // We do not meet the version
                                if (modVersion < dlc.MinVersion)
                                {
                                    M3Log.Error(
                                        $@"Required DLC {dlc.DLCFolderName} is installed but does not meet the minimum version requirement. Installed version: {modVersion}, required version: {dlc.MinVersion}");
                                    M3L.ShowDialog(this,
                                        M3L.GetString(M3L.string_interp_headmorphRequiresDLCMinimumReqNotMet, modNameStr, dlc.MinVersion, modVersion, dlc.MinVersion),
                                        M3L.GetString(M3L.string_prerequesiteNotMet), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                                    return;
                                }
                            }
                        }
                        else
                        {
                            M3Log.Error($@"Required DLC for headmorph is not installed in game: {dlc.DLCFolderName}{(dlc.MinVersion != null ? @" with minimum version " + dlc.MinVersion : null)}");
                            M3L.ShowDialog(this,
                                M3L.GetString(M3L.string_interp_headmorphRequiresDLCPrereqNotMet, modNameStr),
                                M3L.GetString(M3L.string_prerequesiteNotMet), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                            return;
                        }
                    }
                }

                var headmorphFilepath = Path.Combine(SelectedMod.ModPath, Mod.HEADMORPHS_FOLDER_NAME,
                        selectorDialog.SelectedHeadmorph.FileName);
                if (File.Exists(headmorphFilepath))
                {
                    InstallHeadmorphToTarget(headmorphFilepath, SelectedGameTarget, morph.Title);
                }
                else
                {
                    M3Log.Error($@"BUG FOUND? Headmorph file doesn't exist that was chosen: {headmorphFilepath}");
                }
            }
        }

        /// <summary>
        /// Installs a headmorph file to the specified game target
        /// </summary>
        /// <param name="mFileName">Path to the headmorph file</param>
        /// <param name="selectedGameTarget">Target game to install to</param>
        /// <param name="titleSuffix">Optional title suffix for the save selector UI</param>
        private void InstallHeadmorphToTarget(string mFileName, GameTarget selectedGameTarget, string titleSuffix = null)
        {
            // Select save to install to
            SaveSelectorUI ssui = new SaveSelectorUI(this, selectedGameTarget, titleSuffix ?? Path.GetFileName(mFileName));
            ssui.Show();
            ssui.Closed += (sender, args) =>
            {
                if (ssui.SaveWasSelected && ssui.SelectedSaveFile != null)
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        M3Log.Information($@"Installing headmorph {mFileName} to {ssui.SelectedSaveFile.SaveFilePath}");
                        var task = BackgroundTaskEngine.SubmitBackgroundJob(@"HeadmorphInstall", M3L.GetString(M3L.string_installingHeadmorph), M3L.GetString(M3L.string_installedHeadmorphToSave));
                        var installed = HeadmorphInstaller.InstallHeadmorph(mFileName, ssui.SelectedSaveFile.SaveFilePath, task).Result;
                        if (!installed)
                        {
                            task.FinishedUIText = M3L.GetString(M3L.string_failedToInstallHeadmorph);
                        }
                        BackgroundTaskEngine.SubmitJobCompletion(task);
                    });
                }
            };
        }
    }
}
