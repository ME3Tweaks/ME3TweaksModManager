using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols;

namespace ME3TweaksModManager.modmanager.loaders
{
    /// <summary>
    /// Class that contains logic for checking for updates to mods.
    /// </summary>
    public class ModUpdater
    {
        public MainWindow mainWindow { get; set; }

        public static ModUpdater Instance { get; private set; }
        private ModUpdater() { }
        internal void CheckAllModsForUpdates()
        {
            var updatableMods = M3LoadedMods.Instance.VisibleFilteredMods.Where(x => x.IsUpdatable).ToList();
            if (updatableMods.Count > 0)
            {
                CheckModsForUpdates(updatableMods);
            }
        }

        internal void CheckModsForUpdates(List<Mod> updatableMods, bool restoreMode = false)
        {
            M3Log.Information($@"Checking {updatableMods.Count} mods for updates. Turn on mod update logging to view which mods");
            if (Settings.LogModUpdater)
            {
                foreach (var m in updatableMods)
                {
                    M3Log.Information($@" >> Checking for updates to {m.ModName} {m.ParsedModVersion}");
                }
            }

            BackgroundTask bgTask = BackgroundTaskEngine.SubmitBackgroundJob(@"ModCheckForUpdates", M3L.GetString(M3L.string_checkingModsForUpdates), M3L.GetString(M3L.string_modUpdateCheckCompleted));
            void updateCheckProgressCallback(string newStr)
            {
                BackgroundTaskEngine.SubmitBackgroundTaskUpdate(bgTask, newStr);
            }

            var updateManifestModInfos = OnlineContent.CheckForModUpdates(updatableMods, restoreMode, updateCheckProgressCallback);
            if (updateManifestModInfos != null)
            {
                //Calculate CLASSIC Updates
                var updates = updateManifestModInfos.Where(x => x.updatecode > 0 && (x.applicableUpdates.Count > 0 || x.filesToDelete.Count > 0)).ToList();
                foreach (var v in updates)
                {
                    M3Log.Information($@"Classic mod out of date: {v.mod.ModName} {v.mod.ParsedModVersion}, server version: {v.LocalizedServerVersionString}");
                }

                //Calculate MODMAKER Updates
                foreach (var mm in updatableMods.Where(x => x.ModModMakerID > 0))
                {
                    var matchingServerMod = updateManifestModInfos.FirstOrDefault(x => x is OnlineContent.ModMakerModUpdateInfo mmui && mmui.ModMakerId == mm.ModModMakerID);
                    if (matchingServerMod != null)
                    {
                        var serverVer = Version.Parse(matchingServerMod.versionstr + @".0"); //can't have single digit version
                        if (serverVer > mm.ParsedModVersion || restoreMode)
                        {
                            if (!restoreMode)
                            {
                                M3Log.Information($@"ModMaker mod out of date: {mm.ModName} {mm.ParsedModVersion}, server version: {serverVer}");
                            }
                            else
                            {
                                M3Log.Information($@"Restore mode: Show ModMaker mod {mm.ModName} as out of date. Server version: {serverVer}");
                            }
                            matchingServerMod.mod = mm;
                            updates.Add(matchingServerMod);
                            matchingServerMod.SetLocalizedInfo();
                        }
                    }
                }

                //Calculate NEXUSMOD Updates
                foreach (var mm in updatableMods.Where(x => x.NexusModID > 0 && x.ModClassicUpdateCode == 0)) //check zero as Mgamerz's mods will list me3tweaks with a nexus code still for integrations
                {
                    var matchingUpdateInfoForMod = updateManifestModInfos.OfType<OnlineContent.NexusModUpdateInfo>().FirstOrDefault(x => x.NexusModsId == mm.NexusModID
                                                                                                                                   && M3Utilities.GetGameFromNumber(x.GameId) == mm.Game
                                                                                                                                   && updates.All(y => !y.mod.Equals(x.mod)));
                    if (matchingUpdateInfoForMod != null)
                    {
                        if (Version.TryParse(matchingUpdateInfoForMod.versionstr, out var serverVer))
                        {
                            if (serverVer > mm.ParsedModVersion)
                            {
                                // We need to make a clone in the event a mod uses duplicate code, such as Project Variety
                                OnlineContent.NexusModUpdateInfo clonedInfo = new OnlineContent.NexusModUpdateInfo(matchingUpdateInfoForMod) { mod = mm };
                                updates.Add(clonedInfo);
                                clonedInfo.SetLocalizedInfo();
                                M3Log.Information($@"NexusMods mod out of date: {mm.ModName} {mm.ParsedModVersion}, server version: {serverVer}");

                            }
                        }
                        else
                        {
                            M3Log.Error($@"Cannot parse nexusmods version of mod, skipping update check for {mm.ModName}. Server version string is { matchingUpdateInfoForMod.versionstr}");
                        }
                    }
                }

                updates = updates.Distinct().ToList();
                if (updates.Count > 0)
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        var modUpdatesNotificationDialog = new ModUpdateInformationPanel(updates);
                        modUpdatesNotificationDialog.Close += (sender, args) =>
                        {
                            mainWindow.ReleaseBusyControl();
                        };
                        mainWindow.ShowBusyControl(modUpdatesNotificationDialog);
                    });
                }
            }
            else
            {
                bgTask.FinishedUIText = M3L.GetString(M3L.string_errorCheckingForModUpdates);
            }

            BackgroundTaskEngine.SubmitJobCompletion(bgTask);
        }

        public static void InitializeModUpdater(MainWindow mainWindow)
        {
            Instance = new ModUpdater() { mainWindow = mainWindow };
        }

    }
}
