using ME3TweaksCoreWPF.Targets;
using LegendaryExplorerCore.Gammtek.Extensions;
using System.ComponentModel;
using System.Windows;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.localizations;
using System.Threading.Tasks;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.telemetry;
using LegendaryExplorerCore.Helpers;
using ME3TweaksModManager.modmanager.windows.dialog;

namespace ME3TweaksModManager
{
    public partial class MainWindow : Window
    {
        private void StartGame()
        {
            InternalStartGame(SelectedGameTarget);
        }

        internal void InternalStartGame(GameTargetWPF target, string customArguments = null, bool? skipLauncher = null, bool? autoboot = null)
        {
            var game = target.Game.ToGameName();
            BackgroundTask gameLaunch = BackgroundTaskEngine.SubmitBackgroundJob(@"GameLaunch",
                M3L.GetString(M3L.string_interp_launching, game), M3L.GetString(M3L.string_interp_launched, game));

            try
            {
                Task.Run(() =>
                {
                    if (target.Game.IsLEGame())
                    {
                        // Validate that the launch option matches the target's game
                        if (SelectedLaunchOption != null && SelectedLaunchOption.Game != target.Game)
                        {
                            M3Log.Warning($@"Launch option game ({SelectedLaunchOption.Game}) does not match target game ({target.Game}). Using default launch option for {target.Game}.");
                            SelectedLaunchOption = M3LoadedMods.GetDefaultLaunchOptionsPackage(target.Game);
                        }
                        GameLauncher.LaunchGame(target, SelectedLaunchOption, skipLauncher, autoboot);
                    }
                    else
                    {
                        GameLauncher.LaunchGame(target, customArguments);
                    }
                })
                    .ContinueWith(x =>
                    {
                        if (x.Exception != null)
                        {
                            M3Log.Error($@"There was an error launching the game: {x.Exception.FlattenException()}");
                        }

                        BackgroundTaskEngine.SubmitJobCompletion(gameLaunch);
                    });
            }
            catch (Exception e)
            {
                BackgroundTaskEngine.SubmitJobCompletion(gameLaunch); // This ensures message is cleared out of queue
                if (e is Win32Exception w32e)
                {
                    if (w32e.NativeErrorCode == 1223)
                    {
                        //Admin canceled.
                        return; //we don't care.
                    }
                }

                M3Log.Error(@"Error launching game: " + e.Message);
            }

            M3Telemetry.SubmitScreenResolutionInfo(target);
        }

        private bool CanStartGame()
        {
            //Todo: Check if this is origin game and if target will boot
            return SelectedGameTarget != null && SelectedGameTarget.Selectable /*&& SelectedGameTarget.RegistryActive*/;
        }

        private void SelectSpecificSaveForBoot()
        {
            // Select save to install to
            GameLauncher.SetAutoresumeSave(this, SelectedGameTarget, autoresumeSaveChanged: StartGameWithResume);
        }

        private void StartGameWithResume()
        {
            InternalStartGame(SelectedGameTarget, skipLauncher: true, autoboot: true);
        }

        private void OpenLaunchOptionSelector()
        {
            if (SelectedGameTarget?.Game.IsLEGame() ?? false) // Nice and hard to read
            {
                LaunchOptionSelectorDialog losd = new LaunchOptionSelectorDialog(this, SelectedGameTarget.Game);
                losd.ShowDialog();
                UpdateSelectedLaunchOption();
            }
        }

        private void UpdateSelectedLaunchOption()
        {
            if (SelectedGameTarget == null)
                return;

            if (M3LoadedMods.Instance == null || !SelectedGameTarget.Game.IsLEGame())
            {
                // Set default option.
                SelectedLaunchOption = M3LoadedMods.GetDefaultLaunchOptionsPackage(SelectedGameTarget.Game);
                return;
            }

            Guid guidToMatch = SelectedGameTarget.Game switch
            {
                MEGame.LE1 => Settings.SelectedLE1LaunchOption,
                MEGame.LE2 => Settings.SelectedLE2LaunchOption,
                MEGame.LE3 => Settings.SelectedLE3LaunchOption,
                _ => Guid.Empty,
            };

            var option = M3LoadedMods.Instance.AllLaunchOptions.FirstOrDefault(x => x.Game == SelectedGameTarget.Game && x.PackageGuid == guidToMatch);
            if (option != null)
            {
                SelectedLaunchOption = option;
            }
            else
            {
                SelectedLaunchOption = M3LoadedMods.GetDefaultLaunchOptionsPackage(SelectedGameTarget.Game);
            }
        }
    }
}
