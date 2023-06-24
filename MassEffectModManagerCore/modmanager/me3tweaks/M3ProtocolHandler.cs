using ME3TweaksModManager.modmanager.objects;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Services;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.save;
using ME3TweaksModManager.modmanager.windows.input;

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
            protocolKey.SetValue("", @"URL:ME3Tweaks Mod Manager Protocol");
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
                var task = BackgroundTaskEngine.SubmitBackgroundJob(@"SaveImport", "Importing save file from ME3Tweaks",
                    "Imported save file from ME3Tweaks");
                Task.Run(() =>
                {
                    var saveinfo = HttpUtility.ParseQueryString(link.Data);
                    var hash = saveinfo[@"hash"];
                    M3Log.Information($@"Downloading ME3Tweaks diagnostic save: {hash}");

                    var storageLink = $"https://me3tweaks.com/modmanager/logservice/saves/{hash}.pcsav";
                    var saveName = saveinfo[@"name"];

                    var download = MOnlineContent.DownloadToMemory(storageLink, hash: hash);
                    if (download.errorMessage == null)
                    {
                        var loadedSave = SaveFileLoader.LoadSaveFile(download.result);
                       
                        // Some day we might want to set it to have a specific proxy_firstname so it gets sorted out into
                        // a proper career. but that's a lot of work and I don't really want to deal with save editing...

                        var baseSavePath =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare",
                                SaveSelectorUI.GetSaveSubDir(loadedSave.Game));
                        var careerPath = Path.Combine(baseSavePath, @"ModManagerDiagSaves");
                        Directory.CreateDirectory(careerPath);

                        var finalSavePath = Path.Combine(careerPath, saveName);
                        M3Log.Information($@"Installing save file to {finalSavePath} ME3Tweaks diagnostic save: {hash}");
                        download.result.WriteToFile(finalSavePath);
                    }
                    else
                    {
                        task.FinishedUIText = $@"Error importing save: {download.errorMessage}";
                        M3Log.Error($@"Error downloading save: {download.errorMessage}");
                    }
                }).ContinueWithOnUIThread(x =>
                {
                    {

                        BackgroundTaskEngine.SubmitJobCompletion(task);
                    }
                });

                return;
            }
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
            m3pl.Data = queryPos > 0 ? linkContents.Substring(queryPos+1) : null;
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
