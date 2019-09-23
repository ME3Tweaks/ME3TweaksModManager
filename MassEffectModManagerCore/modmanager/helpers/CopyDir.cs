using Serilog;
using System;
using System.IO;

namespace MassEffectModManager.modmanager.helpers
{
    /// <summary>
    /// Helper class for copying a directory with progress
    /// Copied and modified from ALOT Installer
    /// </summary>
    public static class CopyDir
    {

        public static int CopyAll_ProgressBar(DirectoryInfo source, DirectoryInfo target, Action fileCopiedCallback = null, int total = -1, int done = 0, string[] ignoredExtensions = null)
        {
            if (total == -1)
            {
                //calculate number of files
                total = Directory.GetFiles(source.FullName, "*.*", SearchOption.AllDirectories).Length;
            }

            int numdone = done;
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                if (ignoredExtensions != null)
                {
                    bool skip = false;
                    foreach (string str in ignoredExtensions)
                    {
                        if (fi.Name.ToLower().EndsWith(str))
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip)
                    {
                        numdone++;
                        fileCopiedCallback?.Invoke();
                        continue;
                    }
                }
                string displayName = fi.Name;
                string path = Path.Combine(target.FullName, fi.Name);
                //if (path.ToLower().EndsWith(".sfar") || path.ToLower().EndsWith(".tfc"))
                //{
                //    long length = new System.IO.FileInfo(fi.FullName).Length;
                //    displayName += " (" + ByteSize.FromBytes(length) + ")";
                //}
                try
                {
                    fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
                }
                catch (Exception e)
                {
                    Log.Error("Error copying file: " + fi + " -> " + Path.Combine(target.FullName, fi.Name) + ": " + e.Message);
                    throw e;
                }
                // Log.Information(@"Copying {0}\{1}", target.FullName, fi.Name);
                numdone++;
                fileCopiedCallback?.Invoke();
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                numdone = CopyAll_ProgressBar(diSourceSubDir, nextTargetSubDir, fileCopiedCallback, total, numdone);
            }
            return numdone;
        }
    }
}