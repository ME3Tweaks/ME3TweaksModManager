using System;
using System.IO;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod.merge;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    public partial class Mod
    {
        public IMergeMod LoadMergeMod(string fullPath)
        {
            Stream sourceStream;
            if (IsInArchive)
            {
                if (!CheckNonSolidArchiveFile(fullPath))
                    return null;

                // Load from archive
                sourceStream = new MemoryStream();
                Archive.ExtractFile(fullPath, sourceStream);
                sourceStream.Position = 0;
            }
            else
            {
                // Load from disk
                sourceStream = File.OpenRead(fullPath);
            }

            IMergeMod mm = null;
            try
            {
                // if modpath = "" there is no starting /
                int extraLen = ModPath.Length > 0 ? 2 : 1;
                mm = MergeModLoader.LoadMergeMod(sourceStream, fullPath.Substring(ModPath.Length + extraLen + Mod.MergeModFolderName.Length), IsInArchive);
            }
            catch (Exception e)
            {
                M3Log.Error($@"Exception loading merge mod {fullPath}: {e.Message}");
                M3Log.Error(e.StackTrace);
                LoadFailedReason = $@"An error occurred reading referenced merge mod '{fullPath}': {e.Message}. See the logs for more information.";
                return null;
            }
            finally
            {
                if (!IsInArchive)
                {
                    // Do not keep stream open
                    sourceStream.Dispose();
                }
            }

            if (mm.Game != Game)
            {
                M3Log.Error($@"Merge mod {mm.MergeModFilename} lists applicable game as {mm.Game}, but the mod loading this merge mod is for {Game}. The mod and merge mod target games must match.");
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_modparsing_mergeModGameIdMismatch, mm.MergeModFilename, mm.Game, Game);
                return null;
            }

            return mm;
        }
    }
}
