using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TestDirectoryGenerator
{


    /// <summary>
    /// Helper class for copying a directory with progress
    /// Copied and modified from ALOT Installer
    /// </summary>
    public static class CopyDir
    {

        public static void CreateBlankCopy(DirectoryInfo source, DirectoryInfo target, bool useGuids)
        {
            Directory.CreateDirectory(target.FullName);
            Console.WriteLine("Copying directory " + source.Name);
            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                string displayName = fi.Name;
                string path = Path.Combine(target.FullName, fi.Name);
                //if (path.ToLower().EndsWith(".sfar") || path.ToLower().EndsWith(".tfc"))
                //{
                //    long length = new System.IO.FileInfo(fi.FullName).Length;
                //    displayName += " (" + ByteSize.FromBytes(length) + ")";
                //}

                try
                {
                    if (fi.Name == "moddesc.ini")
                    {
                        //Copy moddesc.ini files as we don't want them blank.
                        File.Copy(fi.FullName, Path.Combine(target.FullName, fi.Name));
                    }
                    else
                    if (!useGuids)
                    {
                        File.Create(Path.Combine(target.FullName, fi.Name)).Close();
                    }
                    else
                    {
                        File.WriteAllText(Path.Combine(target.FullName, fi.Name), Guid.NewGuid().ToString());
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error creating blank file: " + fi + " -> " + Path.Combine(target.FullName, fi.Name) + ": " + e.Message);
                    throw e;
                }


                // Log.Information(@"Copying {0}\{1}", target.FullName, fi.Name);

            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CreateBlankCopy(diSourceSubDir, nextTargetSubDir, useGuids);
            }

        }
    }
}
