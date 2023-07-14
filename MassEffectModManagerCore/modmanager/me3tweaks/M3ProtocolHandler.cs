using ME3TweaksModManager.modmanager.objects;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.save;
using ME3TweaksModManager.modmanager.windows.input;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.me3tweaks
{
    /// <summary>
    /// Class for handling m3:// links
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
                window.ShowLogUploadPanel();
                return;
            }

            if (link.Command == M3Link.IMPORT_SAVE_COMMAND)
            {
                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"SaveImport", M3L.GetString(M3L.string_importingSaveFileFromME3Tweaks),
                    M3L.GetString(M3L.string_importedSaveFileFromME3Tweaks));
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

                    M3Log.Information($@"Downloading ME3Tweaks diagnostic save: {hash}");

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
            return m3pl;
        }

        public string Link { get; set; }

        public const string UPLOAD_LOG_COMMAND = @"UPLOADLOG";
        public const string IMPORT_SAVE_COMMAND = @"IMPORTSAVE";


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
    }
}
