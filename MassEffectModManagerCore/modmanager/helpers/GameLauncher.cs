using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using LegendaryExplorerCore;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using MassEffectModManagerCore.modmanager.objects;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.me3tweaks;
using Serilog;

namespace MassEffectModManagerCore.modmanager.helpers
{
    public static class GameLauncher
    {
        // May 17 update
        private const string VanillaLESWFLauncherMD5 = @"ab2559b90696f262ef76a152eff4deb9";

        /// <summary>
        /// Launches the game. This call is blocking as it may wait for Steam to run, so it should be run on a background thread.
        /// </summary>
        /// <param name="target"></param>
        public static void LaunchGame(GameTarget target)
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
                            Log.Error($@"Could not install steam_appid.txt: {e.Message}");
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

            if (Settings.SkipLELauncher && target.Game.IsLEGame())
            {
                commandLineArgs.Add($@"-game"); // Autoboot dll
                commandLineArgs.Add((target.Game.ToGameNum() - 3).ToString());
                commandLineArgs.Add(@"-autoterminate");
                /*
                var sourceFile = Path.Combine(Utilities.GetLELaunchToolsGameBootDir(), @"VanillaLauncherUI.swf");
                var destFile = Path.Combine(LEDirectory.GetLauncherPath(), @"Content", @"LauncherUI.swf");

                bool correctSource = false;
                if (!File.Exists(sourceFile) && Utilities.CalculateMD5(destFile) == VanillaLESWFLauncherMD5)
                {
                    File.Copy(destFile, sourceFile, true);
                    correctSource = true;
                }

                if (correctSource || (File.Exists(sourceFile) && Utilities.CalculateMD5(sourceFile) == VanillaLESWFLauncherMD5))
                {
                    // JPatch it
                    Log.Information($@"JPatching LauncherUI.swf to autoboot {target.Game}");
                    using var outs = File.Open(destFile, FileMode.Create, FileAccess.ReadWrite);
                    using var ins = File.OpenRead(sourceFile);
                    JPatch.ApplyJPatch(ins, Utilities.ExtractInternalFileToStream($@"MassEffectModManagerCore.modmanager.lelauncherbypass.To{target.Game}.jsf"), outs);
                    Log.Information($@"JPatched LauncherUI.swf to autoboot {target.Game}");
                }
                else
                {
                    Log.Warning(@"LauncherUI.swf has wrong hash, not JPatching to autoboot");
                }*/

                var destFile = Path.Combine(LEDirectory.GetLauncherPath(), @"Content", @"BWLogo.bik");
                if (File.Exists(destFile) && new FileInfo(destFile).Length > 500)
                {
                    // > 500 bytes
                    var blackFrame = Utilities.ExtractInternalFileToStream($@"MassEffectModManagerCore.modmanager.lelauncherbypass.singleblackframe.bik");
                    blackFrame.WriteToFile(destFile);
                    Log.Information(@"Installed single black frame for BWLogo.bik");
                }
            }

            Utilities.RunProcess(exe, commandLineArgs, false, true, false, false, environmentVars);
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
                }
                else if (startingUpSteam)
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
