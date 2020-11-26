using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using IniParser;
using IniParser.Model;
using IniParser.Parser;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.Classes;
using Microsoft.AppCenter.Crashes;
using Serilog;
using SevenZip;
using SevenZip.EventArguments;

namespace MassEffectModManagerCore.modmanager
{
    public partial class Mod
    {
        public bool SelectedForImport { get; set; } = true; //Default check on

        private BlockingCollection<string> compressionQueue;
        private object compressionCompletedSignaler = new object();

        /// <summary>
        /// Attempts to retreive the amount of data that will be decompressed. Sine not all archive formats list this information, this method is not 100% reliable.
        /// </summary>
        /// <returns></returns>
        private long GetRequiredSpaceForExtraction()
        {
            if (GetJob(ModJob.JobHeader.ME2_RCWMOD) != null) { return new FileInfo(ArchivePath).Length; }
            if (Archive == null) return 0; //Nothing we can look at
            var itemsToExtract = new List<ArchiveFileInfo>();
            var referencedFiles = GetAllRelativeReferences(!IsVirtualized, Archive);
            //unsure if this is required?? doesn't work for MEHEM EXE
            //referencedFiles = referencedFiles.Select(x => FilesystemInterposer.PathCombine(IsInArchive, ModPath, x)).ToList(); //remap to in-archive paths so they match entry paths
            foreach (var info in Archive.ArchiveFileData)
            {
                if (!info.IsDirectory && (ModPath == "" || info.FileName.Contains(ModPath)))
                {
                    var relativedName = info.FileName.Substring(ModPath.Length).TrimStart('\\');
                    if (referencedFiles.Contains(relativedName))
                    {
                        //Log.Information(@"Adding file to extraction list: " + info.FileName);
                        itemsToExtract.Add(info);
                    }
                    else
                    {
                        Log.Information(@"File being skipped, not referenced by mod: " + relativedName);
                    }
                }
            }

            long requiredSize = 0;
            foreach (var item in itemsToExtract)
            {
                requiredSize += (long)item.Size;
                //Debug.WriteLine($@"Add to size: {item.Size} {item.FileName}");
            }


            return requiredSize;
        }

        /// <summary>
        /// Extracts the mod from the archive. The caller should handle exception that may be thrown.
        /// </summary>
        /// <param name="archivePath"></param>
        /// <param name="outputFolderPath"></param>
        /// <param name="compressPackages"></param>
        /// <param name="updateTextCallback"></param>
        /// <param name="extractingCallback"></param>
        /// <param name="compressedPackageCallback"></param>
        /// <param name="testRun"></param>
        public void ExtractFromArchive(string archivePath, string outputFolderPath, bool compressPackages,
            Action<string> updateTextCallback = null, Action<DetailedProgressEventArgs> extractingCallback = null, Action<string, int, int> compressedPackageCallback = null,
            bool testRun = false)
        {
            if (!IsInArchive) throw new Exception(@"Cannot extract a mod that is not part of an archive.");
            if (!File.Exists(archivePath))
            {
                throw new Exception(M3L.GetString(M3L.string_interp_theArchiveFileArchivePathIsNoLongerAvailable, archivePath));
            }
            compressPackages &= Game >= MEGame.ME2;
            var isExe = archivePath.EndsWith(@".exe", StringComparison.InvariantCultureIgnoreCase);

            var archiveFile = isExe ? new SevenZipExtractor(archivePath, InArchiveFormat.Nsis) : new SevenZipExtractor(archivePath);
            using (archiveFile)
            {
                var fileIndicesToExtract = new List<int>();
                var filePathsToExtractTESTONLY = new List<string>();
                var referencedFiles = GetAllRelativeReferences(!IsVirtualized, archiveFile);
                if (isExe)
                {
                    //remap to mod root. Not entirely sure if this needs to be done for sub mods?
                    referencedFiles = referencedFiles.Select(x => FilesystemInterposer.PathCombine(IsInArchive, ModPath, x)).ToList(); //remap to in-archive paths so they match entry paths
                }
                foreach (var info in archiveFile.ArchiveFileData)
                {
                    if (!info.IsDirectory && (ModPath == "" || info.FileName.Contains(ModPath)))
                    {
                        var relativedName = isExe ? info.FileName : info.FileName.Substring(ModPath.Length).TrimStart('\\');
                        if (referencedFiles.Contains(relativedName))
                        {
                            Log.Information(@"Adding file to extraction list: " + info.FileName);
                            fileIndicesToExtract.Add(info.Index);
                            filePathsToExtractTESTONLY.Add(relativedName);
                        }
                    }
                }
                #region old
                /*
            bool fileAdded = false;
            //moddesc.ini
            if (info.FileName == ModDescPath)
            {
                //Debug.WriteLine("Add file to extraction list: " + info.FileName);
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
                            //Debug.WriteLine("Add file to extraction list: " + info.FileName);
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
                            //Debug.WriteLine("Add alternate file to extraction list: " + info.FileName);
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
                            //Debug.WriteLine("Add alternate dlc file to extraction list: " + info.FileName);
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
                            //Debug.WriteLine("Add file to extraction list: " + info.FileName);
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
                            //Debug.WriteLine("Add alternate file to extraction list: " + info.FileName);
                            fileIndicesToExtract.Add(info.Index);
                            fileAdded = true;
                            break;
                        }
                    }

                    if (fileAdded) break;

                    #endregion
                }
            }
        }*/
                #endregion
                archiveFile.Progressing += (sender, args) => { extractingCallback?.Invoke(args); };
                string outputFilePathMapping(ArchiveFileInfo entryInfo)
                {
                    Log.Information(@"Mapping extraction target for " + entryInfo.FileName);

                    string entryPath = entryInfo.FileName;
                    if (ExeExtractionTransform != null && ExeExtractionTransform.PatchRedirects.Any(x => x.index == entryInfo.Index))
                    {
                        Log.Information(@"Extracting vpatch file at index " + entryInfo.Index);
                        return Path.Combine(Utilities.GetVPatchRedirectsFolder(), ExeExtractionTransform.PatchRedirects.First(x => x.index == entryInfo.Index).outfile);
                    }

                    if (ExeExtractionTransform != null && ExeExtractionTransform.NoExtractIndexes.Any(x => x == entryInfo.Index))
                    {
                        Log.Information(@"Extracting file to trash (not used): " + entryPath);
                        return Path.Combine(Utilities.GetTempPath(), @"Trash", @"trashfile");
                    }

                    if (ExeExtractionTransform != null && ExeExtractionTransform.AlternateRedirects.Any(x => x.index == entryInfo.Index))
                    {
                        var outfile = ExeExtractionTransform.AlternateRedirects.First(x => x.index == entryInfo.Index).outfile;
                        Log.Information($@"Extracting file with redirection: {entryPath} -> {outfile}");
                        return Path.Combine(outputFolderPath, outfile);
                    }

                    //Archive path might start with a \. Substring may return value that start with a \
                    var subModPath = entryPath /*.TrimStart('\\')*/.Substring(ModPath.Length).TrimStart('\\');
                    var path = Path.Combine(outputFolderPath, subModPath);


                    //Debug.WriteLine("remapping output: " + entryPath + " -> " + path);
                    return path;

                }

                if (compressPackages)
                {
                    compressionQueue = new BlockingCollection<string>();
                }

                int numberOfPackagesToCompress = referencedFiles.Count(x => x.RepresentsPackageFilePath());
                int compressedPackageCount = 0;
                NamedBackgroundWorker compressionThread;
                if (compressPackages)
                {
                    compressionThread = new NamedBackgroundWorker(@"ImportingCompressionThread");
                    compressionThread.DoWork += (a, b) =>
                    {
                        try
                        {
                            while (true)
                            {
                                var package = compressionQueue.Take();
                                var p = MEPackageHandler.OpenMEPackage(package);
                                bool shouldNotCompress = Game == MEGame.ME1;
                                if (!shouldNotCompress)
                                {
                                    //updateTextCallback?.Invoke(M3L.GetString(M3L.string_interp_compressingX, Path.GetFileName(package)));
                                    FileInfo fileInfo = new FileInfo(package);
                                    var created = fileInfo.CreationTime; //File Creation
                                    var lastmodified = fileInfo.LastWriteTime;//File Modification

                                    compressedPackageCallback?.Invoke(M3L.GetString(M3L.string_interp_compressingX, Path.GetFileName(package)), compressedPackageCount, numberOfPackagesToCompress);
                                    Log.Information(@"Compressing package: " + package);
                                    p.Save(compress: true);
                                    File.SetCreationTime(package,created);
                                    File.SetLastWriteTime(package, lastmodified);
                                }
                                else
                                {
                                    Log.Information(@"Skipping compression for ME1 package file: " + package);
                                }


                                Interlocked.Increment(ref compressedPackageCount);
                                compressedPackageCallback?.Invoke(M3L.GetString(M3L.string_interp_compressedX, Path.GetFileName(package)), compressedPackageCount, numberOfPackagesToCompress);
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            //Done.
                            lock (compressionCompletedSignaler)
                            {
                                Monitor.Pulse(compressionCompletedSignaler);
                            }
                        }
                    };
                    compressionThread.RunWorkerAsync();
                }
                archiveFile.FileExtractionFinished += (sender, args) =>
                {
                    if (compressPackages)
                    {

                        var fToCompress = outputFilePathMapping(args.FileInfo);
                        if (fToCompress.RepresentsPackageFilePath())
                        {
                            //Debug.WriteLine("Adding to blocking queue");
                            compressionQueue.TryAdd(fToCompress);
                        }
                    }
                };

                if (!testRun)
                {
                    Log.Information(@"Extracting files...");
                    archiveFile.ExtractFiles(outputFolderPath, outputFilePathMapping, fileIndicesToExtract.ToArray());
                }
                else
                {
                    // test run mode
                    // exes can have duplicate filenames but different indexes so we must check for those here.
                    if (fileIndicesToExtract.Count != referencedFiles.Count && filePathsToExtractTESTONLY.Distinct().ToList().Count != referencedFiles.Count)
                    {
                        throw new Exception(@"The amount of referenced files does not match the amount of files that are going to be extracted!");
                    }
                }
                Log.Information(@"File extraction completed.");


                compressionQueue?.CompleteAdding();
                if (compressPackages && numberOfPackagesToCompress > 0 && numberOfPackagesToCompress > compressedPackageCount)
                {
                    Log.Information(@"Waiting for compression of packages to complete.");
                    while (!compressionQueue.IsCompleted)
                    {
                        lock (compressionCompletedSignaler)
                        {
                            Monitor.Wait(compressionCompletedSignaler);
                        }
                    }

                    Log.Information(@"Package compression has completed.");
                }
                ModPath = outputFolderPath;
                if (IsVirtualized)
                {
                    var parser = new IniDataParser().Parse(VirtualizedIniText);
                    parser[@"ModInfo"][@"modver"] = ModVersionString; //In event relay service resolved this
                    if (!testRun)
                    {
                        File.WriteAllText(Path.Combine(ModPath, @"moddesc.ini"), parser.ToString());
                    }
                    IsVirtualized = false; //no longer virtualized
                }

                if (ExeExtractionTransform != null)
                {
                    if (ExeExtractionTransform.VPatches.Any())
                    {
                        // MEHEM uses Vpatching for its alternates.
                        var vpat = Utilities.GetCachedExecutablePath(@"vpat.exe");
                        if (!testRun)
                        {
                            Utilities.ExtractInternalFile(@"MassEffectModManagerCore.modmanager.executables.vpat.exe", vpat, true);
                        }
                        //Handle VPatching
                        foreach (var transform in ExeExtractionTransform.VPatches)
                        {
                            var patchfile = Path.Combine(Utilities.GetVPatchRedirectsFolder(), transform.patchfile);
                            var inputfile = Path.Combine(ModPath, transform.inputfile);
                            var outputfile = Path.Combine(ModPath, transform.outputfile);

                            var args = $"\"{patchfile}\" \"{inputfile}\" \"{outputfile}\""; //do not localize
                            if (!testRun)
                            {
                                Directory.CreateDirectory(Directory.GetParent(outputfile).FullName); //ensure output directory exists as vpatch will not make one.
                            }
                            Log.Information($@"VPatching file into alternate: {inputfile} to {outputfile}");
                            updateTextCallback?.Invoke(M3L.GetString(M3L.string_interp_vPatchingIntoAlternate, Path.GetFileName(inputfile)));
                            if (!testRun)
                            {
                                Utilities.RunProcess(vpat, args, true, false, false, true);
                            }
                        }
                    }

                    //Handle copyfile
                    foreach (var copyfile in ExeExtractionTransform.CopyFiles)
                    {
                        string srcfile = Path.Combine(ModPath, copyfile.inputfile);
                        string destfile = Path.Combine(ModPath, copyfile.outputfile);
                        Log.Information($@"Applying transform copyfile: {srcfile} -> {destfile}");
                        if (!testRun)
                        {
                            File.Copy(srcfile, destfile, true);
                        }
                    }

                    if (ExeExtractionTransform.PostTransformModdesc != null)
                    {
                        //fetch online moddesc for this mod.
                        Log.Information(@"Fetching post-transform third party moddesc.");
                        var moddesc = OnlineContent.FetchThirdPartyModdesc(ExeExtractionTransform.PostTransformModdesc);
                        if (!testRun)
                        {
                            File.WriteAllText(Path.Combine(ModPath, @"moddesc.ini"), moddesc);
                        }
                    }
                }

                //int packagesCompressed = 0;
                //if (compressPackages)
                //{
                //    var packages = Utilities.GetPackagesInDirectory(ModPath, true);
                //    extractingCallback?.Invoke(new ProgressEventArgs((byte)(packagesCompressed * 100.0 / packages.Count), 0));
                //    foreach (var package in packages)
                //    {
                //        updateTextCallback?.Invoke(M3L.GetString(M3L.string_interp_compressingX, Path.GetFileName(package)));
                //        Log.Information("Compressing package: " + package);
                //        var p = MEPackageHandler.OpenMEPackage(package);
                //        p.save(true);

                //        packagesCompressed++;
                //        extractingCallback?.Invoke(new ProgressEventArgs((byte)(packagesCompressed * 100.0 / packages.Count), 0));
                //    }
                //}
            }
        }


        public void ExtractRCWModToM3LibraryMod(string modpath)
        {
            //Write RCW
            var rcw = GetJob(ModJob.JobHeader.ME2_RCWMOD)?.RCW;
            if (rcw != null)
            {
                //Write RCW
                var sanitizedName = Utilities.SanitizePath(ModName);
                rcw.WriteToFile(Path.Combine(modpath, sanitizedName + @".me2mod"));

                //Write moddesc.ini
                IniData ini = new IniData();
                ini[@"ModManager"][@"cmmver"] = App.HighestSupportedModDesc.ToString(CultureInfo.InvariantCulture);
                ini[@"ModInfo"][@"game"] = @"ME2";
                ini[@"ModInfo"][@"modname"] = ModName;
                ini[@"ModInfo"][@"moddev"] = ModDeveloper;
                ini[@"ModInfo"][@"moddesc"] = Utilities.ConvertNewlineToBr(ModDescription);
                ini[@"ModInfo"][@"modver"] = @"1.0"; //Not going to bother looking this up to match the source

                ini[@"ME2_RCWMOD"][@"modfile"] = sanitizedName + @".me2mod";
                var modDescPath = Path.Combine(modpath, @"moddesc.ini");
                new FileIniDataParser().WriteFile(modDescPath, ini, new UTF8Encoding(false));
            }
            else
            {
                Log.Error(@"Tried to extract RCW mod to M3 mod but the job was empty.");
                Crashes.TrackError(new Exception(@"Tried to extract RCW mod to M3 mod but the job was empty."));
            }
        }
    }
}
