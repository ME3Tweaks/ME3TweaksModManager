using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliWrap.EventStream;
using CliWrap;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.usercontrols;
using RoboSharp.Results;

namespace ME3TweaksModManager.modmanager.save
{
    internal class HeadmorphInstaller
    {
        public static async Task<bool> InstallHeadmorph(string morphFilename, string destSavename,
            BackgroundTask installingTask)
        {
            void DownloadProgress(long downloaded, long total)
            {
                Debug.WriteLine($@"Downloading TSE {downloaded}/{total}");
            }

            var tseAvailable = await TrilogySaveEditorCLIUpdater.UpdateTSECLI(DownloadProgress);
            if (!tseAvailable)
            {
                return false;
            }

            var tseCliToolFolder = ExternalToolLauncher.GetToolStoragePath(ExternalToolLauncher.TRILOGYSAVEEDITOR_CMD);
            var tseCliToolPath = Path.Combine(tseCliToolFolder,
                ExternalToolLauncher.ToolNameToExeName(ExternalToolLauncher.TRILOGYSAVEEDITOR_CMD));

            await RunTSECLIImportHeadmorph(tseCliToolPath, morphFilename, destSavename);

            return true;
        }

        private static async Task<int> RunTSECLIImportHeadmorph(string tseCliFilePath, string headMorphFile,
            string existingSave)
        {
            int exitCode = -1;
            Version v = new Version(0, 0);

            string newSaveName = Path.Combine(Directory.GetParent(existingSave).FullName, GetNewSaveName(existingSave));
            M3Log.Information($"Installing headmorph {headMorphFile} into save {existingSave}, result will be saved to {newSaveName}");
            var cmd = Cli.Wrap(tseCliFilePath)
                .WithArguments(
                    $"import-head-morph --input \"{headMorphFile}\" --output \"{newSaveName}\"  \"{existingSave}\"")
                .WithValidation(CommandResultValidation.None); // do not localize
            await foreach (var cmdEvent in cmd.ListenAsync())

            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        break;
                    case StandardOutputCommandEvent stdOut:
#if DEBUG
                        Debug.WriteLine(stdOut.Text);
#endif
                        break;
                    case StandardErrorCommandEvent stdErr:
                        Debug.WriteLine(@"STDERR " + stdErr.Text);
                        M3Log.Fatal($@"{stdErr.Text}");
                        break;
                    case ExitedCommandEvent exited:
                        exitCode = exited.ExitCode;
                        break;
                }
            }

            return exitCode;
        }

        private static string GetNewSaveName(string existingSave)
        {
            var saveDir = Directory.GetParent(existingSave).FullName;
            var fname = Path.GetFileNameWithoutExtension(existingSave);
            var ext = Path.GetExtension(existingSave);
            if (fname.StartsWith(@"Save-"))
            {
                // Numbered save
                var saveNum = int.Parse(fname.Substring(5));
                string numberedPrefix = @"Save-";

                while (saveNum < 9999)
                {
                    string testFullName = Path.Combine(saveDir, $@"{numberedPrefix}{saveNum}{ext}");
                    if (!File.Exists(testFullName))
                        return testFullName;
                    saveNum++; // Try again
                }

            }
         
            
            return existingSave; // Overwrite - might be dangerous?
        }
    }
}
