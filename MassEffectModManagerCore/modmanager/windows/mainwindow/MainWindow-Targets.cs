using LegendaryExplorerCore;
using ME3TweaksCore.ME3Tweaks.M3Merge;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksCoreWPF.Targets;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ME3TweaksModManager.modmanager;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.helpers;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3TweaksModManager.modmanager.localizations;
using LegendaryExplorerCore.GameFilesystem;
using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;

namespace ME3TweaksModManager
{
    /// <summary>
    /// Partial class containing game target loading and population logic
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// List of all loaded targets, even ones for different generations
        /// </summary>
        private List<GameTargetWPF> InternalLoadedTargets { get; } = new();

        public GameTargetWPF SelectedGameTarget { get; set; }

        /// <summary>
        /// Lock on this object if you want to ensure targets are not repopulating when code is run.
        /// </summary>
        internal static object targetRepopulationSyncObj = new();

        private bool RepopulatingTargets;

        private void PopulateTargets(GameTargetWPF selectedTarget = null)
        {
            // We lock this code behind object to ensure it finishes running before something else tries to use targets. If a panel tries to access targets list, it could be empty, and that's a problem.
            lock (targetRepopulationSyncObj)
            {
                RepopulatingTargets = true;
                InstallationTargets.ClearEx();
                SelectedGameTarget = null;
                MEDirectories.ReloadGamePaths(true); //this is redundant on the first boot but whatever.
                M3Log.Information(@"Populating game targets");
                var targets = new List<GameTargetWPF>();
                bool foundMe1Active = false;
                bool foundMe2Active = false;
                if (ME3Directory.DefaultGamePath != null && Directory.Exists(ME3Directory.DefaultGamePath))
                {
                    var target = new GameTargetWPF(MEGame.ME3, ME3Directory.DefaultGamePath, true);
                    var failureReason = target.ValidateTarget();
                    if (failureReason == null)
                    {
                        M3Log.Information(@"Current boot target for ME3: " + target.TargetPath);
                        targets.Add(target);
                        M3Utilities.AddCachedTarget(target);
                    }
                    else
                    {
                        M3Log.Error(@"Current boot target for ME3 is invalid: " + failureReason);
                    }
                }

                if (ME2Directory.DefaultGamePath != null && Directory.Exists(ME2Directory.DefaultGamePath))
                {
                    var target = new GameTargetWPF(MEGame.ME2, ME2Directory.DefaultGamePath, true);
                    var failureReason = target.ValidateTarget();
                    if (failureReason == null)
                    {
                        M3Log.Information(@"Current boot target for ME2: " + target.TargetPath);
                        targets.Add(target);
                        M3Utilities.AddCachedTarget(target);
                        foundMe2Active = true;
                    }
                    else
                    {
                        M3Log.Error(@"Current boot target for ME2 is invalid: " + failureReason);
                    }
                }

                if (ME1Directory.DefaultGamePath != null && Directory.Exists(ME1Directory.DefaultGamePath))
                {
                    var target = new GameTargetWPF(MEGame.ME1, ME1Directory.DefaultGamePath, true);
                    var failureReason = target.ValidateTarget();
                    if (failureReason == null)
                    {
                        M3Log.Information(@"Current boot target for ME1: " + target.TargetPath);
                        targets.Add(target);
                        M3Utilities.AddCachedTarget(target);
                        foundMe1Active = true;
                    }
                    else
                    {
                        M3Log.Error(@"Current boot target for ME1 is invalid: " + failureReason);
                    }
                }

                if (!string.IsNullOrWhiteSpace(LegendaryExplorerCoreLibSettings.Instance?.LEDirectory) &&
                    Directory.Exists(LegendaryExplorerCoreLibSettings.Instance.LEDirectory))
                {
                    // Load LE targets
                    void loadLETarget(MEGame game, string defaultPath)
                    {
                        var target = new GameTargetWPF(game, defaultPath, true);
                        var failureReason = target.ValidateTarget();
                        if (failureReason == null)
                        {
                            M3Log.Information($@"Current boot target for {game}: {target.TargetPath}");
                            targets.Add(target);
                        }
                        else
                        {
                            M3Log.Error($@"Current boot target for {game} at {target.TargetPath} is invalid: " +
                                        failureReason);
                        }
                    }

                    loadLETarget(MEGame.LELauncher, LEDirectory.LauncherPath);
                    loadLETarget(MEGame.LE1, LE1Directory.DefaultGamePath);
                    loadLETarget(MEGame.LE2, LE2Directory.DefaultGamePath);
                    loadLETarget(MEGame.LE3, LE3Directory.DefaultGamePath);
                }

                // Read steam locations
                void addSteamTarget(string targetPath, bool foundActiveAlready, MEGame game)
                {
                    if (!string.IsNullOrWhiteSpace(targetPath)
                        && Directory.Exists(targetPath)
                        && !targets.Any(x =>
                            x.TargetPath.Equals(targetPath, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        var target = new GameTargetWPF(game, targetPath, !foundActiveAlready);
                        var failureReason = target.ValidateTarget();
                        if (failureReason == null)
                        {
                            M3Log.Information($@"Found Steam game for {game}: " + target.TargetPath);
                            // Todo: Figure out how to insert at correct index
                            targets.Add(target);
                            M3Utilities.AddCachedTarget(target);
                        }
                        else
                        {
                            M3Log.Error($@"Steam version of {game} at {targetPath} is invalid: {failureReason}");
                        }
                    }
                }

                // ME1
                addSteamTarget(M3Utilities.GetRegistrySettingString(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 17460",
                    @"InstallLocation"), foundMe1Active, MEGame.ME1);

                // ME2
                addSteamTarget(M3Utilities.GetRegistrySettingString(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 24980",
                    @"InstallLocation"), foundMe2Active, MEGame.ME2);

                // ME3
                addSteamTarget(M3Utilities.GetRegistrySettingString(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1238020",
                    @"InstallLocation"), foundMe2Active, MEGame.ME3);

                // Legendary Edition
                var legendarySteamLoc = M3Utilities.GetRegistrySettingString(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1328670",
                    @"InstallLocation");
                if (!string.IsNullOrWhiteSpace(legendarySteamLoc))
                {
                    addSteamTarget(Path.Combine(legendarySteamLoc, @"Game", @"Launcher"), false, MEGame.LELauncher);
                    addSteamTarget(Path.Combine(legendarySteamLoc, @"Game", @"ME1"), false, MEGame.LE1);
                    addSteamTarget(Path.Combine(legendarySteamLoc, @"Game", @"ME2"), false, MEGame.LE2);
                    addSteamTarget(Path.Combine(legendarySteamLoc, @"Game", @"ME3"), false, MEGame.LE3);
                }

                M3Log.Information(@"Loading cached targets");
                targets.AddRange(M3Utilities.GetCachedTargets(MEGame.ME3, targets));
                targets.AddRange(M3Utilities.GetCachedTargets(MEGame.ME2, targets));
                targets.AddRange(M3Utilities.GetCachedTargets(MEGame.ME1, targets));

                // Load LE cached targets
                targets.AddRange(M3Utilities.GetCachedTargets(MEGame.LE3, targets));
                targets.AddRange(M3Utilities.GetCachedTargets(MEGame.LE2, targets));
                targets.AddRange(M3Utilities.GetCachedTargets(MEGame.LE1, targets));
                targets.AddRange(M3Utilities.GetCachedTargets(MEGame.LELauncher, targets));

                OrderAndSetTargets(targets, selectedTarget);
            }
        }

        private void OrderAndSetTargets(List<GameTargetWPF> targets, GameTargetWPF selectedTarget = null)
        {
            // ORDER THE TARGETS
            //targets = targets.Where(x => x.Game.IsEnabledGeneration()).Distinct().ToList();
            var finalList = new List<GameTargetWPF>();

            //LE
            var aTarget = targets.FirstOrDefault(x => x.Game == MEGame.LE3 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.LE2 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.LE1 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.LELauncher && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);

            // OT
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.ME3 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.ME2 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);
            aTarget = targets.FirstOrDefault(x => x.Game == MEGame.ME1 && x.RegistryActive);
            if (aTarget != null) finalList.Add(aTarget);

            if (targets.Count > finalList.Count)
            {
                finalList.Add(new GameTargetWPF(MEGame.Unknown,
                    $@"==================={M3L.GetString(M3L.string_otherSavedTargets)}===================", false,
                    true)
                { Selectable = false });
            }

            finalList.AddRange(targets.Where(x => x.Game == MEGame.LE3 && !x.RegistryActive));
            finalList.AddRange(targets.Where(x => x.Game == MEGame.LE2 && !x.RegistryActive));
            finalList.AddRange(targets.Where(x => x.Game == MEGame.LE1 && !x.RegistryActive));
            finalList.AddRange(targets.Where(x => x.Game == MEGame.LELauncher && !x.RegistryActive));

            finalList.AddRange(targets.Where(x => x.Game == MEGame.ME3 && !x.RegistryActive));
            finalList.AddRange(targets.Where(x => x.Game == MEGame.ME2 && !x.RegistryActive));
            finalList.AddRange(targets.Where(x => x.Game == MEGame.ME1 && !x.RegistryActive));

            if (!InternalLoadedTargets.Any())
            {
                InternalLoadedTargets.ReplaceAll(finalList.Where(x => !x.IsCustomOption));
            }

            finalList = finalList.Where(x => x.IsCustomOption || x.Game.IsEnabledGeneration()).ToList();
            if (finalList.LastOrDefaultOut(out var lastTarget) && lastTarget.IsCustomOption)
            {
                // Trim last custom option
                finalList.Remove(lastTarget);
            }

            InstallationTargets.ReplaceAll(finalList.Where(x => x.IsCustomOption || x.Game.IsEnabledGeneration()));

            if (selectedTarget != null &&
                finalList.FirstOrDefaultOut(x => x.TargetPath == selectedTarget.TargetPath, out var selTarget))
            {
                SelectedGameTarget = selTarget;
            }
            else if (!string.IsNullOrWhiteSpace(Settings.LastSelectedTarget) && InstallationTargets.FirstOrDefaultOut(
                         x => !x.IsCustomOption && x.TargetPath.Equals(Settings.LastSelectedTarget),
                         out var matchingTarget))
            {
                SelectedGameTarget = matchingTarget;
            }
            else
            {
                if (InstallationTargets.Count > 0)
                {
                    var firstSelectableTarget = InstallationTargets.FirstOrDefault(x => x.Selectable);
                    if (firstSelectableTarget != null)
                    {
                        SelectedGameTarget = firstSelectableTarget;
                    }
                }
            }

            UpdateMenuTargets();
            //BackupService.SetInstallStatuses(InstallationTargets);
            RepopulatingTargets = false;
        }

        /// <summary>
        /// Gets current target that matches the game. If selected target does not match, the first one in the list used (active). THIS CAN RETURN A NULL OBJECT!
        /// </summary>
        /// <param name="game">Game to find target for</param>
        /// <returns>Game matching target. If none is found, this return null.</returns>
        internal GameTargetWPF GetCurrentTarget(MEGame game)
        {
            if (SelectedGameTarget != null)
            {
                if (SelectedGameTarget.Game == game) return SelectedGameTarget;
            }

            return InstallationTargets.FirstOrDefault(x => x.Game == game);
        }

        public void OnSelectedGameTargetChanged()
        {
            if (!RepopulatingTargets && SelectedGameTarget != null)
            {
                //Settings.Save();
                if (!SelectedGameTarget.RegistryActive)
                {
                    UpdateBinkStatus(SelectedGameTarget.Game);
                    try
                    {
                        var hresult = UpdateBootTarget(SelectedGameTarget);
                        if (hresult == -3) return; //do nothing.
                        if (hresult == 0)
                        {
                            //rescan
                            PopulateTargets(SelectedGameTarget);
                            SelectedGameTarget.UpdateLODs(Settings.AutoUpdateLODs2K);
                        }

                        TelemetryInterposer.TrackEvent(@"Changed to non-active target", new Dictionary<string, string>()
                        {
                            { @"New target", SelectedGameTarget.Game.ToString() },
                        });
                    }
                    catch (Win32Exception ex)
                    {
                        M3Log.Warning(
                            @"Win32 exception occurred updating boot target. User maybe pressed no to the UAC dialog?: " +
                            ex.Message);
                    }
                }

                Settings.LastSelectedTarget = SelectedGameTarget?.TargetPath;
                UpdateSelectedLaunchOption();
            }
        }
        
        public void UpdateMenuTargets()
        {
            // Populate the list of available games, for menus
            MenuAvailableGames.ReplaceAll(InstallationTargets.Where(x => x.Game.IsMEGame()).Select(x => x.Game).Distinct().OrderBy(x => x));
        }

        /// <summary>
        /// Updates boot target and returns the HRESULT of the update command for registry.
        /// Returns -3 if no registry update was performed.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        private int UpdateBootTarget(GameTargetWPF target)
        {
            string exe = @"reg";
            var args = new List<string>();
            string regPath = null;
            switch (target.Game)
            {
                case MEGame.ME1:
                    {
                        var existingPath = ME1Directory.DefaultGamePath;
                        if (existingPath != null)
                        {
                            regPath = @"HKLM\SOFTWARE\Wow6432Node\BioWare\Mass Effect";
                        }
                    }
                    break;
                case MEGame.ME2:
                    {
                        var existingPath = ME2Directory.DefaultGamePath;
                        if (existingPath != null)
                        {
                            regPath = @"HKLM\SOFTWARE\Wow6432Node\BioWare\Mass Effect 2";
                        }
                    }

                    break;
                case MEGame.ME3:
                    {
                        var existingPath = ME3Directory.DefaultGamePath;
                        if (existingPath != null)
                        {
                            regPath = @"HKLM\SOFTWARE\Wow6432Node\BioWare\Mass Effect 3";
                        }
                    }
                    break;
            }

            if (regPath != null)
            {
                //is set in registry
                args.Add(@"add");
                args.Add(regPath);
                args.Add(@"/v");
                args.Add(target.Game == MEGame.ME3 ? @"Install Dir" : @"Path");
                args.Add(@"/t");
                args.Add(@"REG_SZ");
                args.Add(@"/d");
                args.Add($"{target.TargetPath.TrimEnd('\\')}\\\\"); // do not localize
                                                                    // ^ Strip ending slash. Then append it to make sure there is ending slash. Reg will interpret final \ as an escape, so we do \\ (as documented on ss64)
                args.Add(@"/f");

                return M3Utilities.RunProcess(exe, args, waitForProcess: true, requireAdmin: true);
            }

            return -3;
        }

        private void AddTarget()
        {
            M3Log.Information(@"User is adding new modding target");
            var ofd = new OpenFileDialog();
            ofd.Title = M3L.GetString(M3L.string_selectGameExecutable);
            string filter =
                $@"{M3L.GetString(M3L.string_gameExecutable)}|MassEffect.exe;MassEffect2.exe;MassEffect3.exe;MassEffectLauncher.exe;MassEffect1.exe"; //only partially localizable.
            ofd.Filter = filter;
            if (ofd.ShowDialog() == true)
            {
                MEGame gameSelected = MEGame.Unknown;
                var filename = Path.GetFileName(ofd.FileName);
                M3Log.Information($@"Validating user chosen exe: {filename}");
                if (filename.Equals(@"MassEffect3.exe", StringComparison.InvariantCultureIgnoreCase))
                    gameSelected = MEGame.ME3;
                if (filename.Equals(@"MassEffect2.exe", StringComparison.InvariantCultureIgnoreCase))
                    gameSelected = MEGame.ME2;

                if (gameSelected != MEGame.Unknown)
                {
                    // Check for LE versions
                    var version = FileVersionInfo.GetVersionInfo(ofd.FileName);
                    if (version.FileMajorPart >= 2)
                    {
                        // LE1 can't be selected this way as it has unique exe name.
                        if (gameSelected == MEGame.ME2) gameSelected = MEGame.LE2;
                        if (gameSelected == MEGame.ME3) gameSelected = MEGame.LE3;
                    }
                }
                else
                {
                    // Has unique name
                    if (filename.Equals(@"MassEffect.exe", StringComparison.InvariantCultureIgnoreCase))
                        gameSelected = MEGame.ME1;
                    if (filename.Equals(@"MassEffect1.exe", StringComparison.InvariantCultureIgnoreCase))
                        gameSelected = MEGame.LE1;

                    if (filename.Equals(@"MassEffectLauncher.exe"))
                    {
                        var version = FileVersionInfo.GetVersionInfo(ofd.FileName);
                        if (version.FileMajorPart >= 2)
                        {
                            gameSelected = MEGame.LELauncher;
                        }
                    }
                }

                if (gameSelected != MEGame.Unknown)
                {
                    string result = Path.GetDirectoryName(ofd.FileName);
                    if (gameSelected != MEGame.LELauncher)
                    {
                        // game root path for ME1/ME2
                        result = Path.GetDirectoryName(result);
                    }

                    if (gameSelected.IsLEGame() || gameSelected == MEGame.ME3)
                        result = Path.GetDirectoryName(result); //up one more because of win32/win64 directory.

                    var pendingTarget = new GameTargetWPF(gameSelected, result, false);
                    string failureReason = pendingTarget.ValidateTarget();

                    if (failureReason == null)
                    {
                        TelemetryInterposer.TrackEvent(@"Attempted to add game target", new Dictionary<string, string>()
                        {
                            { @"Game", pendingTarget.Game.ToString() },
                            { @"Result", @"Success" },
                            { @"Supported", pendingTarget.Supported.ToString() }
                        });

                        M3Utilities.AddCachedTarget(pendingTarget);
                        PopulateTargets(pendingTarget);
                    }
                    else
                    {
                        TelemetryInterposer.TrackEvent(@"Attempted to add game target", new Dictionary<string, string>()
                        {
                            { @"Game", pendingTarget.Game.ToString() },
                            { @"Result", @"Failed, " + failureReason },
                            { @"Supported", pendingTarget.Supported.ToString() }
                        });
                        M3Log.Error(@"Could not add target: " + failureReason);
                        M3L.ShowDialog(this,
                            M3L.GetString(M3L.string_interp_dialogUnableToAddGameTarget, failureReason),
                            M3L.GetString(M3L.string_errorAddingTarget), MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    M3Log.Error($@"Unsupported/unknown game: {ofd.FileName}");
                }
            }

            else
            {
                M3Log.Information(@"User aborted adding new target");
            }
        }

    }
}
