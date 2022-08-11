using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;

namespace ME3TweaksModManager.modmanager.helpers
{
    public static class GameLauncher
    {
        /// <summary>
        /// Launches the game. This call is blocking as it may wait for Steam to run, so it should be run on a background thread.
        /// </summary>
        /// <param name="target"></param>
        public static void LaunchGame(GameTargetWPF target)
        {
            target.ReloadGameTarget(false);

            // Update LODs for target
            if (target.SupportsLODUpdates() && (Settings.AutoUpdateLODs4K || Settings.AutoUpdateLODs2K))
            {
                target.UpdateLODs(Settings.AutoUpdateLODs2K);
            }


            List<string> commandLineArgs = new();
            var exe = M3Directories.GetExecutablePath(target);
            var exeDir = M3Directories.GetExecutableDirectory(target);
            var environmentVars = new Dictionary<string, string>();
            if (target.GameSource != null)
            {

                // IS GAME STEAM BASED?
                if (target.GameSource.Contains(@"Steam"))
                {
                    var steamInstallPath = M3Utilities.GetRegistrySettingString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", @"InstallPath");
                    if (steamInstallPath != null && Directory.Exists(steamInstallPath))
                    {
                        environmentVars[@"SteamPath"] = steamInstallPath;

                        // Ensure steam is running and ready
                        var steamExe = Path.Combine(steamInstallPath, @"steam.exe");
                        EnsureSteamRunning(steamExe); // Can block up for some time!
                    }

                    int gameId = 0;
                    switch (target.Game)
                    {
                        case MEGame.ME1:
                            gameId = 17460;
                            break;
                        case MEGame.ME2:
                            gameId = 24980;
                            break;
                        case MEGame.ME3:
                            gameId = 1238020;
                            break;
                        case MEGame.LE1:
                        case MEGame.LE2:
                        case MEGame.LE3:
                            gameId = 1328670;
                            break;
                    }

                    environmentVars[@"SteamAppId"] = gameId.ToString();
                    environmentVars[@"SteamGameId"] = gameId.ToString();
                    environmentVars[@"SteamOverlayGameId"] = gameId.ToString();

                    // Make steam_appid.txt next to exe. It can help launch game if steam.exe is already running
                    var steamappidfile = Path.Combine(exeDir, @"steam_appid.txt");
                    if (!File.Exists(steamappidfile))
                    {
                        try
                        {
                            File.WriteAllText(steamappidfile, gameId.ToString());
                        }
                        catch (Exception e)
                        {
                            M3Log.Error($@"Could not install steam_appid.txt: {e.Message}");
                        }
                    }
                }
                else if (target.GameSource.Contains(@"Origin") && target.RegistryActive && target.Game < MEGame.ME3 && Settings.LaunchGamesThroughOrigin) // Must be registry active or origin will run the wrong game.
                {
                    // ME2 seems to have lots of problems directly running due to it's licensing system
                    // We should try to run it through Origin to avoid this problem

                    var parFile = Path.Combine(exeDir, Path.GetFileNameWithoutExtension(exe) + @".par");
                    if (target.Game == MEGame.ME2 && File.Exists(parFile))
                    {
                        var fInfo = new FileInfo(exe);
                        if (fInfo.Length < 5 * FileSize.MebiByte)
                        {
                            // Does this executable need swapped? MassEffect2.exe does not seem to reliably run through Origin and just exits early for some reason

                        }
                        var parContents = PARTools.DecodePAR(File.ReadAllBytes(parFile));
                        var contentIds = parContents[@"Base"].GetValue(@"ContentId")?.Value;

                        if (!string.IsNullOrWhiteSpace(contentIds))
                        {
                            exe = $@"origin://launchgame/{contentIds}";
                        }
                    }
                }
            }

            if (Settings.SkipLELauncher && target.Game.IsLEGame() && !MUtilities.IsGameRunning(MEGame.LELauncher))
            {
                var launcherPath = Path.Combine(target.TargetPath, @"..", @"Launcher");
                var launcherTarget = new GameTargetWPF(MEGame.LELauncher, launcherPath, false);
                var failedValidationReason = launcherTarget.ValidateTarget();
                if (failedValidationReason == null)
                {
                    // Ensure bypass is installed
                    launcherTarget.InstallBinkBypass(false); // If bink fails to install, whatever. Launcher may be running.
                }
                commandLineArgs.Add($@"-game"); // Autoboot dll
                commandLineArgs.Add((target.Game.ToGameNum() - 3).ToString());
                commandLineArgs.Add(@"-autoterminate");
            }

#if DEBUG
            if (target.Game.IsLEGame() && !target.Supported)
            {
                commandLineArgs.Add(@"-NoHomeDir");
            }
#endif
            M3Utilities.RunProcess(exe, commandLineArgs, false, true, false, false, environmentVars);
            Thread.Sleep(3500); // Keep task alive for a bit
        }

        /// <summary>
        /// Ensures steam is running and the user is logged in.
        /// </summary>
        /// <param name="steamExe"></param>
        private static bool EnsureSteamRunning(string steamExe)
        {
            int numTicksSteamLoggedIn = 0;
            int numRetries = 10;
            int timeBetweenRetries = 2000; // 2 seconds
            bool startingUpSteam = false;
            while (numRetries > 0)
            {
                var steamInfo = getRunningSteamInfo();
                if (steamInfo.steamProcessId > 0 && steamInfo.steamUserId > 0)
                {
                    // Steam is running, user is logged in, we are good to go
                    if (!startingUpSteam || numTicksSteamLoggedIn > 1)
                    {
                        M3Log.Information(@"Steam running and user is logged in, continuing game launch");
                        return true;
                    }
                    else
                    {
                        M3Log.Information($@"Waiting for steam to finish initializing... ({numRetries} retries left)");
                        numTicksSteamLoggedIn++;
                    }

                }
                else if (steamInfo.steamProcessId > 0 && steamInfo.steamUserId <= 0)
                {
                    // Steam is running but the user is not yet logged in
                    M3Log.Information($@"Steam running, but not yet logged in, will retry running game ({numRetries} retries left)");
                }
                else if (steamInfo.steamProcessId <= 0 && !startingUpSteam)
                {
                    // Steam is not running
                    // We need to run steam or it's going to throw the application error message.
                    M3Log.Information($@"Steam not running. Launching now.");
                    startingUpSteam = true;
                    M3Utilities.RunProcess(steamExe);
                }
                else if (startingUpSteam)
                {
                    M3Log.Information($@"Waiting for steam process to startup ({numRetries} retries left)");
                }
                Thread.Sleep(timeBetweenRetries);
                numRetries--;
            }

            M3Log.Error(@"Steam could not be launched + logged into within the retry period. The game executable may throw application error message when it's launched. Running steam games requires steam to be running");
            return false;
        }

        private static (int steamProcessId, int steamUserId) getRunningSteamInfo()
        {
            var currentSteamPid = M3Utilities.GetRegistrySettingInt(@"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess", @"pid"); // Set when the steam client has started up
            var currentSteamUser = M3Utilities.GetRegistrySettingInt(@"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess", @"ActiveUser"); // Set when the user is logged in. Cannot launch until this is set

            return (currentSteamPid, currentSteamUser);
        }
    }
}
