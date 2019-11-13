using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

namespace MassEffectModManagerCore.modmanager.helpers
{
    /// <summary>
    /// Helper class for copying a directory with progress
    /// Copied and modified from ALOT Installer
    /// </summary>
    public static class CopyDir
    {

        public static int CopyAll_ProgressBar(DirectoryInfo source, DirectoryInfo target, Action<int> totalItemsToCopyCallback = null, Action fileCopiedCallback = null, Func<string, bool> aboutToCopyCallback = null, int total = -1, int done = 0, string[] ignoredExtensions = null)
        {
            if (total == -1)
            {
                //calculate number of files
                total = Directory.GetFiles(source.FullName, "*.*", SearchOption.AllDirectories).Length;
                totalItemsToCopyCallback?.Invoke(total);
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
                var shouldCopy = aboutToCopyCallback?.Invoke(fi.FullName);
                if (shouldCopy.HasValue && shouldCopy.Value)
                {
                    try
                    {
                        fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error copying file: " + fi + " -> " + Path.Combine(target.FullName, fi.Name) + ": " + e.Message);
                        throw e;
                    }
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
                numdone = CopyAll_ProgressBar(diSourceSubDir, nextTargetSubDir, totalItemsToCopyCallback, fileCopiedCallback, aboutToCopyCallback, total, numdone);
            }
            return numdone;
        }

        public static void CopyFiles_ProgressBar(Dictionary<string, string> fileMapping, Action<string> fileCopiedCallback = null)
        {
            foreach (var singleMapping in fileMapping)
            {
                var source = singleMapping.Key;
                var dest = singleMapping.Value;
                Directory.CreateDirectory(Directory.GetParent(dest).FullName);
                if (File.Exists(dest))
                {
                    FileAttributes attributes = File.GetAttributes(dest);

                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        // Make the file RW
                        attributes = attributes & ~FileAttributes.ReadOnly;
                        File.SetAttributes(dest, attributes);
                    }
                }

                File.Copy(source, dest, true);
                fileCopiedCallback?.Invoke(dest);
            }
        }
    }
}