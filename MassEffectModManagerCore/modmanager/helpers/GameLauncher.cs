using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using MassEffectModManagerCore.modmanager.objects;
using ME3ExplorerCore.Packages;
using Serilog;

namespace MassEffectModManagerCore.modmanager.helpers
{
    public static class GameLauncher
    {
        /// <summary>
        /// Launches the game. This call is blocking as it may wait for Steam to run, so it should be run on a background thread.
        /// </summary>
        /// <param name="target"></param>
        public static void LaunchGame(GameTarget target)
        {
            // Update LODs for target
            if (Settings.AutoUpdateLODs4K || Settings.AutoUpdateLODs2K)
            {
                target.UpdateLODs(Settings.AutoUpdateLODs2K);
            }

            var exe = M3Directories.GetExecutablePath(target);
            var exeDir = M3Directories.GetExecutableDirectory(target);
            var environmentVars = new Dictionary<string, string>();
            if (target.GameSource != null && target.GameSource.Contains(@"Steam"))
            {
                var steamInstallPath = Utilities.GetRegistrySettingString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", @"InstallPath");
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
                        Log.Error($@"Could not install steam_appid.txt: {e.Message}");
                    }
                }
            }

            Utilities.RunProcess(exe, (string)null, false, true, false, false, environmentVars);
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
                        Log.Information(@"Steam running and user is logged in, continuing game launch");
                        return true;
                    }
                    else
                    {
                        Log.Information($@"Waiting for steam to finish initializing... ({numRetries} retries left)");
                        numTicksSteamLoggedIn++;
                    }

                }
                else if (steamInfo.steamProcessId > 0 && steamInfo.steamUserId <= 0)
                {
                    // Steam is running but the user is not yet logged in
                    Log.Information($@"Steam running, but not yet logged in, will retry running game ({numRetries} retries left)");
                }
                else if (steamInfo.steamProcessId <= 0 && !startingUpSteam)
                {
                    // Steam is not running
                    // We need to run steam or it's going to throw the application error message.
                    Log.Information($@"Steam not running. Launching now.");
                    startingUpSteam = true;
                    Utilities.RunProcess(steamExe);
                } else if (startingUpSteam)
                {
                    Log.Information($@"Waiting for steam process to startup ({numRetries} retries left)");
                }
                Thread.Sleep(timeBetweenRetries);
                numRetries--;
            }

            Log.Error(@"Steam could not be launched + logged into within the retry period. The game executable may throw application error message when it's launched. Running steam games requires steam to be running");
            return false;
        }

        private static (int steamProcessId, int steamUserId) getRunningSteamInfo()
        {
            var currentSteamPid = Utilities.GetRegistrySettingInt(@"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess", @"pid"); // Set when the steam client has started up
            var currentSteamUser = Utilities.GetRegistrySettingInt(@"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess", @"ActiveUser"); // Set when the user is logged in. Cannot launch until this is set

            return (currentSteamPid, currentSteamUser);
        }
    }
}
