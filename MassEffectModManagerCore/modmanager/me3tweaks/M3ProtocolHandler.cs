using Flurl.Util;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.NativeMods.Interfaces;
using ME3TweaksCore.Services;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.save;
using ME3TweaksModManager.modmanager.usercontrols;
using ME3TweaksModManager.modmanager.windows.input;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;

namespace ME3TweaksModManager.modmanager.me3tweaks
{
    /// <summary>
    /// Class for handling me3tweaksmodmanager:// links
    /// </summary>
    public class M3ProtocolHandler
    {
        public static void SetupProtocolHandler()
        {
            // Register app
            M3Log.Information(@"Registering this application as the me3tweaksmodmanager:// handler");
            using var subkey =
                Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Classes\me3tweaksmodmanager\shell\open\command");
            subkey.SetValue("", $"\"{App.ExecutableLocation}\" --m3link \"%1\""); // do not localize

            // Register protocol
            var protocolKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Classes\me3tweaksmodmanager", true);
            protocolKey.SetValue(@"URL Protocol", @"");
            protocolKey.SetValue(@"", @"URL:ME3Tweaks Mod Manager Protocol");
        }

        public static void HandleLink(string m3link, MainWindow window)
        {
            var link = M3Link.Parse(m3link);
            if (link == null) return;

            if (link.Command == M3Link.UPLOAD_LOG_COMMAND)
            {
                GameTarget uploadTarget = null;
                if (link.QueryParams != null && link.QueryParams.TryGetValue(@"game", out var gameStr) && Enum.TryParse<MEGame>(gameStr, out var game))
                {
                    uploadTarget = window.InstallationTargets.FirstOrDefault(x => x.Game == game);
                }
                if (!window.HasAnyQueuedPanelsOfType(typeof(LogUploaderPanel)))
                {
                    M3Log.Information(@"M3ProtocolHandler: Showing log uploader");
                    window.ShowLogUploadPanel(uploadTarget);
                }
                else
                {
                    M3Log.Information(@"Log upload panel is already open or queued, not opening another instance.");
                }
                return;
            }

            if (link.Command == M3Link.LMP_LAUNCH_COMMAND)
            {
                HandleLMPLaunch(window);
                return;
            }

            if (link.Command == M3Link.LMP_JOIN_COMMAND)
            {
                HandleLMPJoin(link, window);
                return;
            }

            if (link.Command == M3Link.IMPORT_SAVE_COMMAND)
            {
                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"SaveImport", M3L.GetString(M3L.string_importingSaveFileFromME3Tweaks), M3L.GetString(M3L.string_importedSaveFileFromME3Tweaks));
                Task.Run(() =>
                {
                    var saveinfo = HttpUtility.ParseQueryString(link.Data);
                    var hash = saveinfo[@"hash"];
                    string gameStr = saveinfo[@"game"];
                    MEGame? game = null;
                    if (gameStr != null)
                    {
                        game = Enum<MEGame>.Parse(gameStr);
                    }

                    M3Log.Information($@"M3ProtocolHandler: Downloading ME3Tweaks diagnostic save: {hash}");

                    var storageLink = $@"https://me3tweaks.com/modmanager/logservice/saves/{hash}.pcsav";
                    var saveName = saveinfo[@"name"];

                    var download = MOnlineContent.DownloadToMemory(storageLink, hash: hash);
                    if (download.errorMessage == null)
                    {
                        var loadedSave = SaveFileLoader.LoadSaveFile(download.result);
                        var sf = JsonConvert.SerializeObject(loadedSave);
                        Debug.WriteLine(sf);
                        // Some day we might want to set it to have a specific proxy_firstname so it gets sorted out into
                        // a proper career. but that's a lot of work and I don't really want to deal with save editing...

                        var baseSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", SaveSelectorUI.GetSaveSubDir(game ?? loadedSave.Game));
                        var careerPath = Path.Combine(baseSavePath, GetDiagnosticCareerName(game ?? loadedSave.Game));
                        Directory.CreateDirectory(careerPath);

                        var finalSavePath = Path.Combine(careerPath, saveName);
                        M3Log.Information($@"Installing save file to {finalSavePath} ME3Tweaks diagnostic save: {hash}");
                        download.result.WriteToFile(finalSavePath);

                        if (saveinfo[@"openintse"] is string str && bool.TryParse(str, out var openInSTE) && openInSTE)
                        {
                            M3Log.Information(@"Opening save in TSE");
                            TrilogySaveEditorHelper.OpenTSE(window, finalSavePath);
                        }
                    }
                    else
                    {
                        task.FinishedUIText = $@"Error importing save: {download.errorMessage}";
                        M3Log.Error($@"Error downloading save: {download.errorMessage}");
                    }
                }).ContinueWithOnUIThread(x =>
                {
                    if (x.Exception != null)
                    {
                        task.FinishedUIText = M3L.GetString(M3L.string_interp_errorImportingSaveFileX, x.Exception.Message);
                    }
                    BackgroundTaskEngine.SubmitJobCompletion(task);
                });

                return;
            }
        }

        /// <summary>
        /// Discord runs me3tweaksmodmanager://lmplaunch when a Discord join for LMP is accepted while the game is
        /// closed. The join itself reaches the game through the Discord Integration ASI once it starts, so this
        /// only makes sure that ASI is installed and boots the current LMP target.
        /// </summary>
        private static void HandleLMPLaunch(MainWindow window)
        {
            if (MRunningGameInfo.IsGameRunning(MEGame.LMP, true))
            {
                M3Log.Information(@"M3ProtocolHandler: LMP is already running; Discord delivers the join to it");
                return;
            }

            var target = window.GetCurrentTarget(MEGame.LMP);
            if (target == null)
            {
                M3Log.Error(@"M3ProtocolHandler: cannot start LMP for a Discord join, there is no LMP target");
                return;
            }

            var gameName = target.Game.ToGameName();
            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"LMPDiscordLaunch",
                M3L.GetString(M3L.string_interp_preparingForDiscordJoin, gameName), M3L.GetString(M3L.string_interp_preparedForDiscordJoin, gameName));
            Task.Run(async () =>
            {
                // Without the ASI the game still starts; the player can then join with the invite code.
                if (!await EnsureLMPDiscordIntegration(target))
                    M3Log.Warning(@"M3ProtocolHandler: Discord Integration could not be installed; LMP starts without it and will not receive the Discord join");
            }).ContinueWithOnUIThread(x =>
            {
                if (x.Exception != null)
                    M3Log.Error($@"M3ProtocolHandler: error preparing LMP for a Discord join: {x.Exception.FlattenException()}");
                BackgroundTaskEngine.SubmitJobCompletion(task);
                window.InternalStartGame(target);
            });
        }

        /// <summary>
        /// Installs or updates the LMP Discord Integration ASI and its Discord SDK runtime. A build the manifest does
        /// not know (a developer build) is left in place: installing the manifest version beside it would load two copies.
        /// </summary>
        private static async Task<bool> EnsureLMPDiscordIntegration(GameTargetWPF target)
        {
            const int groupId = (int)ASIModUpdateGroupID.LMP_DiscordIntegration;
            bool installed = ASIModIDs.GetInstalledASIModIds(M3Directories.GetExecutableDirectory(target)).Any(x => x.id == groupId);
            bool known = target.GetInstalledASIs().OfType<IKnownInstalledASIMod>().Any(x => x.AssociatedManifestItem.OwningMod.UpdateGroupId == groupId);
            if (installed && !known)
            {
                M3Log.Information(@"M3ProtocolHandler: an unlisted build of Discord Integration is installed; leaving it in place");
                return true;
            }

            return await ASIManager.InstallASIToTargetByGroupID(ASIModUpdateGroupID.LMP_DiscordIntegration, @"Discord Integration", target, includeHiddenASIs: true);
        }

        /// <summary>
        /// The matchmaker an lmpjoin link without a server parameter means. MilanHost's LMP_LOBBY_DEFAULT_MATCHMAKER_URL.
        /// </summary>
        private const string DEFAULT_LMP_MATCHMAKER_URL = @"https://vps.me3tweaks.com";

        /// <summary>
        /// Handles the join link MilanHost copies to the clipboard when the host presses Invite Friends. The invite code goes
        /// to the game through MilanHost's control pipe, after starting the current LMP target if LMP is not running; the game
        /// then joins once its multiplayer menu is open.
        /// </summary>
        private static void HandleLMPJoin(M3Link link, MainWindow window)
        {
            string code = null;
            string server = null;
            link.QueryParams?.TryGetValue(@"code", out code);
            link.QueryParams?.TryGetValue(@"server", out server);
            // Links leave out the default server. Name it anyway, so the join does not go to whichever server the game is set to.
            if (string.IsNullOrEmpty(server))
                server = DEFAULT_LMP_MATCHMAKER_URL;
            if (!IsValidLMPJoin(code, server))
            {
                M3Log.Error($@"M3ProtocolHandler: invalid LMP join link: {link.Link}");
                M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_invalidJoinLink), M3L.GetString(M3L.string_title_joinMultiplayerLobby), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // A link can come from anywhere, and its server decides which game the player connects to.
            var gameName = MEGame.LMP.ToGameName();
            if (M3L.ShowDialog(window, M3L.GetString(M3L.string_interp_dialog_joinMultiplayerLobby, gameName, code, server),
                    M3L.GetString(M3L.string_title_joinMultiplayerLobby), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            GameTargetWPF target = null;
            bool running = MRunningGameInfo.IsGameRunning(MEGame.LMP, true);
            if (!running)
            {
                target = window.GetCurrentTarget(MEGame.LMP);
                if (target == null)
                {
                    M3Log.Error(@"M3ProtocolHandler: cannot start LMP for a join link, there is no LMP target");
                    return;
                }
            }

            M3Log.Information($@"M3ProtocolHandler: joining LMP lobby {code} on {server}");
            var task = BackgroundTaskEngine.SubmitBackgroundJob(@"LMPJoin",
                M3L.GetString(M3L.string_interp_joiningMultiplayerLobby, gameName, code), M3L.GetString(M3L.string_interp_joinedMultiplayerLobby, gameName, code));
            if (target != null)
                window.InternalStartGame(target);

            // A game started through Steam or the EA app can take a while to reach MilanHost's attach.
            var timeout = running ? TimeSpan.FromSeconds(15) : TimeSpan.FromMinutes(5);
            Task.Run(() => SendLMPJoin(server, code, timeout)).ContinueWithOnUIThread(x =>
            {
                if (x.Exception != null)
                    M3Log.Error($@"M3ProtocolHandler: error sending the LMP join: {x.Exception.FlattenException()}");
                if (x.Exception != null || !x.Result)
                    task.FinishedUIText = M3L.GetString(M3L.string_interp_couldNotJoinMultiplayerLobby, gameName, code);
                BackgroundTaskEngine.SubmitJobCompletion(task);
            });
        }

        /// <summary>
        /// The limits MilanHost's control pipe enforces.
        /// </summary>
        private static bool IsValidLMPJoin(string code, string server)
        {
            if (string.IsNullOrEmpty(code) || code.Length > 64 || code.Any(c => c <= 0x20 || c >= 0x7F))
                return false;
            return !string.IsNullOrEmpty(server) && server.Length < 256 && Uri.TryCreate(server, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Sends the join to the first running LMP whose MilanHost control pipe (\\.\pipe\MilanHost.&lt;pid&gt;) answers,
        /// waiting up to <paramref name="timeout"/> for one to open it.
        /// </summary>
        private static bool SendLMPJoin(string server, string code, TimeSpan timeout)
        {
            var processNames = MEDirectories.ExecutableNames(MEGame.LMP).Select(Path.GetFileNameWithoutExtension).ToList();
            var request = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { @"matchmakerUrl", server },
                { @"inviteCode", code },
            }));

            var deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                foreach (var process in processNames.SelectMany(Process.GetProcessesByName))
                {
                    using (process)
                    {
                        try
                        {
                            using var pipe = new NamedPipeClientStream(@".", $@"MilanHost.{process.Id}", PipeDirection.InOut);
                            pipe.Connect(1000);
                            pipe.ReadMode = PipeTransmissionMode.Message;
                            pipe.Write(request);
                            pipe.Flush();

                            using var reply = new MemoryStream();
                            var buffer = new byte[4096];
                            do
                            {
                                int read = pipe.Read(buffer);
                                if (read == 0)
                                    break;
                                reply.Write(buffer, 0, read);
                            } while (!pipe.IsMessageComplete);

                            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(Encoding.UTF8.GetString(reply.ToArray()));
                            if (result != null && result.TryGetValue(@"ok", out var ok) && ok is true)
                            {
                                M3Log.Information($@"M3ProtocolHandler: LMP (process {process.Id}) accepted the join");
                                return true;
                            }

                            M3Log.Error($@"M3ProtocolHandler: LMP (process {process.Id}) refused the join: {result?.GetValueOrDefault(@"error")}");
                            return false;
                        }
                        catch (TimeoutException)
                        {
                            // MilanHost has not opened its pipe yet, or this process has no MilanHost.
                        }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
                        {
                            M3Log.Warning($@"M3ProtocolHandler: could not send the join to LMP (process {process.Id}): {e.Message}");
                        }
                    }
                }

                if (DateTime.UtcNow >= deadline)
                {
                    M3Log.Error(@"M3ProtocolHandler: no running LMP accepted the join before the timeout");
                    return false;
                }
                Thread.Sleep(2000);
            }
        }

        private static string GetDiagnosticCareerName(MEGame game)
        {
            string baseName = @"ModManagerDiagSaves";

            switch (game)
            {
                case MEGame.ME1: // ME1 is not supported for this feature
                case MEGame.LE1:
                    return baseName;
                case MEGame.ME2:
                    return baseName + @"_03_Diag_070123";
                case MEGame.ME3:
                case MEGame.LE2:
                case MEGame.LE3:
                    return baseName + @"_03_Diag_070123_0000000";
            }

            throw new Exception(@"Unsupported game for this feature!");
        }
    }

    /// <summary>
    /// Object representation of an me3tweaksmodmanager:// link
    /// </summary>
    public class M3Link
    {
        public static M3Link Parse(string link)
        {
            if (!link.StartsWith(@"me3tweaksmodmanager://")) return null; // not an me3tweaksmodmanager:// link!

            var m3pl = new M3Link() { Link = link };
            var linkContents = link.Substring(22); // remove 'me3tweaksmodmanager://'
            var queryPos = linkContents.IndexOf('?');
            m3pl.Command = (queryPos > 1 ? linkContents.Substring(0, queryPos) : linkContents).ToUpper().Trim('/'); // Some browsers add this on the end
            m3pl.Data = queryPos > 0 ? linkContents.Substring(queryPos + 1) : null;

            // 12/6/2025 - Support parsing link parameters
            var uri = new Uri(link);
            var query = HttpUtility.ParseQueryString(uri.Query);
            m3pl.QueryParams = query.AllKeys.ToDictionary(k => k, k => query[k]);

            query.ToKeyValuePairs().ToDictionary();

            return m3pl;
        }

        public string Link { get; set; }

        public const string UPLOAD_LOG_COMMAND = @"UPLOADLOG";
        public const string IMPORT_SAVE_COMMAND = @"IMPORTSAVE";
        /// <summary>
        /// Starts LMP for a join accepted in Discord. The Discord Integration ASI registers this link as LMP's Discord launch command.
        /// </summary>
        public const string LMP_LAUNCH_COMMAND = @"LMPLAUNCH";
        /// <summary>
        /// Joins an LMP lobby: lmpjoin?code=&lt;invite code&gt;&amp;server=&lt;matchmaker URL&gt;, with no server for the default one.
        /// me3tweaks.com/modmanager/protocol/lmpjoin opens these; MilanHost copies links to that page from Invite Friends.
        /// </summary>
        public const string LMP_JOIN_COMMAND = @"LMPJOIN";


        // Not used currently
        /// <summary>
        /// Version of link feature
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// The command to issue
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Data for the command to execute
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Dictionary of parameters on the protocol link. This is the parsed version of Data.
        /// </summary>
        public Dictionary<string, string> QueryParams { get; private set; }
    }
}
