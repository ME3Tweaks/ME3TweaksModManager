using System.Threading.Tasks;
using CliWrap.EventStream;
using CliWrap;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.usercontrols;

namespace ME3TweaksModManager.modmanager.headmorph
{
    internal class HeadmorphInstaller
    {
        public static async Task<bool> InstallHeadmorph(string morphFilename, string destSavename, BackgroundTask installingTask)
        {
            void DownloadProgress(long downloaded, long total)
            {
                M3Log.Information($@"Downloading TSECLI {downloaded}/{total} bytes");
            }

            var tseAvailable = await TrilogySaveEditorCLIUpdater.UpdateTSECLI(DownloadProgress);
            if (!tseAvailable)
            {
                M3Log.Error(@"TSECLI is not available. Cannot install headmorphs");
                return false;
            }

            var tseCliToolFolder = ExternalToolLauncher.GetToolStoragePath(ExternalToolLauncher.TRILOGYSAVEEDITOR_CMD);
            var tseCliToolPath = Path.Combine(tseCliToolFolder, ExternalToolLauncher.ToolNameToExeName(ExternalToolLauncher.TRILOGYSAVEEDITOR_CMD));

            var exitCode = await RunTSECLIImportHeadmorph(tseCliToolPath, morphFilename, destSavename);

            return exitCode == 0;
        }

        private static async Task<int> RunTSECLIImportHeadmorph(string tseCliFilePath, string headMorphFile,
            string existingSave)
        {
            int exitCode = -1;
            string newSaveName = Path.Combine(Directory.GetParent(existingSave).FullName, GetNewSaveName(existingSave));
            M3Log.Information($@"Installing headmorph {headMorphFile} into save {existingSave}, result will be saved to {newSaveName}");
            var cmd = Cli.Wrap(tseCliFilePath)
                .WithArguments($"import-head-morph --input \"{headMorphFile}\" --output \"{newSaveName}\"  \"{existingSave}\"") // do not localize
                .WithValidation(CommandResultValidation.None); // do not localize
            await foreach (var cmdEvent in cmd.ListenAsync())

            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        M3Log.Information($@"TSECLI: Process started with id {started.ProcessId}");
                        break;
                    case StandardOutputCommandEvent stdOut:
                        M3Log.Information($@"TSECLI: {stdOut.Text}");
                        break;
                    case StandardErrorCommandEvent stdErr:
                        M3Log.Error($@"TSECLI: {stdErr.Text}");
                        break;
                    case ExitedCommandEvent exited:
                        M3Log.Information($@"TSECLI: Process exited with code {exited.ExitCode}");
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
