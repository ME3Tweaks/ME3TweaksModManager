using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.launcher;
using Pathoschild.FluentNexus.Models;

namespace ME3TweaksModManager.modmanager.helpers
{
    public static class GameLauncher
    {
        private const string AUTOBOOT_KEY_NAME = @"LEAutobootArgs"; // DO NOT CHANGE - USED FOR AUTOBOOT IN BINK DLL


        public static void LaunchGame(GameTargetWPF target, LaunchOptionsPackage LaunchPackage)
        {
            if (!target.Game.IsLEGame()) return;

            string args = @"";

            if (Settings.SkipLELauncher) // Autoboot
            {
                args += $@" -game {LaunchPackage.Game.ToMEMGameNum()} -autoterminate";

                // Custom option is the vanilla launch - do not add any extra params
                if (!LaunchPackage.IsCustomOption)
                {
                    args += $@" -Subtitles {LaunchPackage.SubtitleSize} ";
                    if (LaunchPackage.Game == MEGame.LE3)
                    {
                        args += $@"-language={LaunchPackage.ChosenLanguage} ";
                    }
                    else
                    {
                        args += $@"-OVERRIDELANGUAGE={LaunchPackage.ChosenLanguage}";
                    }

                    if (LaunchPackage.EnableMinidumps)
                    {
                        args += @" -enableminidumps";
                    }

                    if (LaunchPackage.AutoResumeSave)
                    {
                        args += @" -RESUME";
                    }

                    if (LaunchPackage.NoForceFeedback)
                    {
                        args += @" -NOFORCEFEEDBACK";
                    }

                    // Custom options
                    args += @" " + LaunchPackage.CustomExtraArgs;
                }
            }

            LaunchGame(target, args);

            App.SubmitAnalyticTelemetryEvent("LE Game Launch", new Dictionary<string, string>()
            {
                {@"Game", target.Game.ToString()},
                {@"LaunchConfig", (!LaunchPackage.IsCustomOption).ToString()},
            });
        }

        /// <summary>
        /// Launches the game. This call is blocking as it may wait for Steam to run, so it should be run on a background thread.
        /// </summary>
        /// <param name="target"></param>
        public static void LaunchGame(GameTargetWPF target, string presuppliedArguments = null)
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
            }

            if (Settings.SkipLELauncher && target.Game.IsLEGame() && !MUtilities.IsGameRunning(MEGame.LELauncher))
            {
                var launcherPath = Path.Combine(target.TargetPath, @"..", @"Launcher");
                var launcherTarget = new GameTargetWPF(MEGame.LELauncher, launcherPath, false);
                var failedValidationReason = launcherTarget.ValidateTarget();
                if (failedValidationReason == null)
                {
                    // Ensure bypass is installed
#if DEBUG
                    launcherTarget.InstallBinkBypass(false); // If bink fails to install, whatever. Launcher may be running.
#else
                    // In release mode we will always install bink bypass
                    launcherTarget.InstallBinkBypass(false); // If bink fails to install, whatever. Launcher may be running.
#endif
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

            if (presuppliedArguments != null)
            {
                // We were passed in arguments to use
                WriteLEAutobootValue(presuppliedArguments);
                RunGame(target, exe, presuppliedArguments, null, environmentVars);
            }
            else
            {
                // We use the generated command line arguments as none were passed in
                if (target.Game.IsLEGame())
                {
                    WriteLEAutobootValue(string.Join(@" ", commandLineArgs));
                }
                RunGame(target, exe, null, commandLineArgs, environmentVars);
                //M3Utilities.RunProcess(exe, commandLineArgs, false, true, false, false, environmentVars);
            }

            Thread.Sleep(3500); // Keep task alive for a bit
        }

        private static void RunGame(GameTargetWPF target, string exe, string commandLineArgsString, List<string> commandLineArgsList, Dictionary<string, string> environmentVars)
        {
            // If the game source is steam and it's LE, we can use Link2EA as they all require EA app to run.
            // Technically this can also be done for ME3 but I'm not going to bother changing launch code for it
            if (target.GameSource != null && target.GameSource.Contains(@"Steam") && (target.Game == MEGame.ME3 || target.Game.IsLEGame() || target.Game == MEGame.LELauncher))
            {
                // Experimental: Use Link2EA to boot without EA sign in
                var link2EA = RegistryHandler.GetRegistryString(@"HKEY_CLASSES_ROOT\link2ea\shell\open\command", "");
                if (link2EA != null)
                {
                    // We found Link2EA
                    var splitStr = link2EA.Split('"')
                        .Select((element, index) => index % 2 == 0  // If even index
                            ? element.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)  // Split the item
                            : new string[] { element })  // Keep the entire item
                        .SelectMany(element => element).ToList();
                    exe = splitStr[0];

                    var theme = target.Game == MEGame.ME3 ? @"me3" : @"met";
                    var gameId = target.Game == MEGame.ME3 ? 1238020 : 1328670; // Game IDs
                    commandLineArgsString = $@"link2ea://launchgame/{gameId}?platform=steam&theme={theme}"; // The id of what to run.
                }
            }

            if (commandLineArgsString != null)
            {
                M3Utilities.RunProcess(exe, commandLineArgsString, false, true, false, false, environmentVars);
            }
            else if (commandLineArgsList != null)
            {
                M3Utilities.RunProcess(exe, commandLineArgsList, false, true, false, false, environmentVars);
            }
            else
            {
                M3Utilities.RunProcess(exe, @"", false, true, false, false, environmentVars);
            }
        }

        private static void WriteLEAutobootValue(string bootArgs)
        {
            // a space must precede the arguments - I'm too lazy to fix the terrible cmdline C++ code I wrote 
            MSharedSettings.WriteSettingString(AUTOBOOT_KEY_NAME, bootArgs);
            M3Log.Information($@"Wrote autoboot command line: '{bootArgs}'");
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
