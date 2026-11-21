using System.Windows;
using LegendaryExplorerCore.GameFilesystem;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>
    /// Writes converted profiles to LMP's MP_Profile and imports LEMP saves.
    /// </summary>
    internal static class LmpProfileImport
    {
        /// <summary>
        /// Writes to a temp file, reads it back, then swaps it in.
        /// </summary>
        /// <returns>The written MP_Profile path</returns>
        public static string WriteProfile(byte[] profileBytes)
        {
            var destination = LMPDirectory.LocalProfilePath;
            Directory.CreateDirectory(LMPDirectory.SaveFolderPath);
            var temp = Path.Combine(LMPDirectory.SaveFolderPath, $"MP_Profile.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(temp, profileBytes);
                Me3MpProfileConverter.ValidateFile(temp);
                if (File.Exists(destination))
                {
                    // Backup profile then install
                    var backup = destination + $".backup-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}";
                    File.Replace(temp, destination, backup);
                }
                else
                {
                    // Move into position
                    File.Move(temp, destination);
                }
            }
            finally
            {
                // Nuke the temp file
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            return destination;
        }

        /// <summary>
        /// Reads and converts a LEMP save. Throws if it can't be converted.
        /// </summary>
        public static byte[] ConvertLempSave(string lempSavePath)
        {
            var info = new FileInfo(lempSavePath);
            if (info.Length > 8 * 1024 * 1024)
            {
                throw new InvalidDataException("The file is too large to be a LEMP multiplayer save.");
            }
            return LempMpProfileConverter.Convert(File.ReadAllText(lempSavePath));
        }

        /// <summary>
        /// Asks for a LEMP save, then imports it. Must run on the UI thread
        /// </summary>
        public static void PromptAndImportLempSave(Window window)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a LEMP profile file",
                Filter = "LEMP multiplayer profile|*.txt|All files|*.*",
                CheckFileExists = true
            };
            if (ofd.ShowDialog(window) == true)
            {
                ImportLempSave(window, ofd.FileName);
            }
        }

        /// <summary>
        /// Converts a LEMP save into LMP's MP_Profile. Asks first if one already exists. Must run on the UI thread
        /// </summary>
        public static void ImportLempSave(Window window, string lempSavePath)
        {
            try
            {
                var profileBytes = ConvertLempSave(lempSavePath);
                if (File.Exists(LMPDirectory.LocalProfilePath))
                {
                    var abort = M3L.ShowDialog(window, $"An LMP multiplayer profile already exists. Overwrite it with the LEMP save below? A backup will be kept beside it.\n\n{lempSavePath}",
                        "Import LEMP multiplayer save", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes;
                    if (abort)
                    {
                        // User canceled
                        return;
                    }
                }

                var destination = WriteProfile(profileBytes);
                M3Log.Information($"Imported LEMP multiplayer save {lempSavePath} to {destination}");
                M3L.ShowDialog(window, $"LEMP multiplayer save converted successfully to:\n\n{destination}", "Profile import complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception e)
            {
                M3Log.Exception(e, "Failed to import LEMP multiplayer save");
                M3L.ShowDialog(window, $"The LEMP multiplayer save could not be converted: {e.Message}", "Profile import failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
