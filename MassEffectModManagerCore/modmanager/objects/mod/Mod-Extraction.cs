using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using IniParser.Parser;
using MassEffectModManager;
using ME3Explorer.Packages;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore.modmanager
{
    public partial class Mod
    {
        public bool SelectedForImport { get; set; } = true; //Default check on

        public void ExtractFromArchive(string archivePath, bool compressPackages, Action<string> updateTextCallback = null, Action<ProgressEventArgs> extractingCallback = null)
        {
            if (!IsInArchive) throw new Exception("Cannot extract a mod that is not part of an archive.");
            var modDirectory = Utilities.GetModDirectoryForGame(Game);
            var sanitizedPath = Path.Combine(modDirectory, Utilities.SanitizePath(ModName));
            if (Directory.Exists(sanitizedPath))
            {
                //Will delete on import
                //Todo: Delete directory/s
            }

            Directory.CreateDirectory(sanitizedPath);


            using (var archiveFile = new SevenZipExtractor(archivePath))
            {
                var fileIndicesToExtract = new List<int>();
                foreach (var info in archiveFile.ArchiveFileData)
                {
                    bool fileAdded = false;
                    //moddesc.ini
                    if (info.FileName == ModDescPath)
                    {
                        Debug.WriteLine("Add file to extraction list: " + info.FileName);
                        fileIndicesToExtract.Add(info.Index);
                        continue;
                    }

                    //Check each job
                    foreach (ModJob job in InstallationJobs)
                    {
                        if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                        {
                            #region Extract Custom DLC
                            foreach (var localCustomDLCFolder in job.CustomDLCFolderMapping.Keys)
                            {
                                if (info.FileName.StartsWith(FilesystemInterposer.PathCombine(IsInArchive, ModPath, localCustomDLCFolder)))
                                {
                                    Debug.WriteLine("Add file to extraction list: " + info.FileName);
                                    fileIndicesToExtract.Add(info.Index);
                                    fileAdded = true;
                                    break;
                                }
                            }

                            if (fileAdded) break;

                            //Alternate files
                            foreach (var alt in job.AlternateFiles)
                            {
                                if (alt.AltFile != null && info.FileName.Equals(FilesystemInterposer.PathCombine(IsInArchive, ModPath, alt.AltFile), StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Debug.WriteLine("Add alternate file to extraction list: " + info.FileName);
                                    fileIndicesToExtract.Add(info.Index);
                                    fileAdded = true;
                                    break;
                                }
                            }

                            if (fileAdded) break;

                            //Alternate DLC
                            foreach (var alt in job.AlternateDLCs)
                            {
                                if (info.FileName.StartsWith(FilesystemInterposer.PathCombine(IsInArchive, ModPath, alt.AlternateDLCFolder), StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Debug.WriteLine("Add alternate dlc file to extraction list: " + info.FileName);
                                    fileIndicesToExtract.Add(info.Index);
                                    fileAdded = true;
                                    break;
                                }
                            }

                            if (fileAdded) break;

                            #endregion
                        }
                        else
                        {
                            #region Official headers

                            foreach (var inSubDirFile in job.FilesToInstall.Values)
                            {
                                var inArchivePath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, job.JobDirectory, inSubDirFile); //keep relative if unpacked mod, otherwise use full in-archive path for extraction
                                if (info.FileName.Equals(inArchivePath, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Debug.WriteLine("Add file to extraction list: " + info.FileName);
                                    fileIndicesToExtract.Add(info.Index);
                                    fileAdded = true;
                                    break;
                                }
                            }

                            if (fileAdded) break;
                            //Alternate files
                            foreach (var alt in job.AlternateFiles)
                            {
                                if (alt.AltFile != null && info.FileName.Equals(FilesystemInterposer.PathCombine(IsInArchive, ModPath, alt.AltFile), StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Debug.WriteLine("Add alternate file to extraction list: " + info.FileName);
                                    fileIndicesToExtract.Add(info.Index);
                                    fileAdded = true;
                                    break;
                                }
                            }

                            if (fileAdded) break;

                            #endregion
                        }
                    }
                }

                archiveFile.Extracting += (sender, args) => { extractingCallback?.Invoke(args); };

                string outputFilePathMapping(string entryPath)
                {
                    //Archive path might start with a \. Substring may return value that start with a \
                    var subModPath = entryPath /*.TrimStart('\\')*/.Substring(ModPath.Length).TrimStart('\\');
                    var path = Path.Combine(sanitizedPath, subModPath);
                    //Debug.WriteLine("remapping output: " + entryPath + " -> " + path);
                    return path;
                }

                archiveFile.ExtractFiles(sanitizedPath, outputFilePathMapping, fileIndicesToExtract.ToArray());
                ModPath = sanitizedPath;
                if (IsVirtualized)
                {
                    var parser = new IniDataParser().Parse(VirtualizedIniText);
                    parser["ModInfo"]["modver"] = ModVersionString; //In event relay service resolved this
                    File.WriteAllText(Path.Combine(ModPath, "moddesc.ini"), parser.ToString());
                }

                int packagesCompressed = 0;
                if (compressPackages)
                {
                    var packages = Utilities.GetPackagesInDirectory(ModPath, true);
                    extractingCallback?.Invoke(new ProgressEventArgs((byte)(packagesCompressed * 100.0 / packages.Count), 0));
                    foreach (var package in packages)
                    {
                        updateTextCallback?.Invoke($"Compressing {Path.GetFileName(package)}");
                        Log.Information("Compressing package: " + package);
                        var p = MEPackageHandler.OpenMEPackage(package);
                        p.save(true);

                        packagesCompressed++;
                        extractingCallback?.Invoke(new ProgressEventArgs((byte)(packagesCompressed * 100.0 / packages.Count), 0));
                    }
                }
            }
        }
    }
}
