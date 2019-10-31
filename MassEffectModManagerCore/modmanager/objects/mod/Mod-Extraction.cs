using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using IniParser.Parser;
using MassEffectModManagerCore.gamefileformats.unreal;
using MassEffectModManagerCore.modmanager.helpers;
using ME3Explorer.Packages;
using Serilog;
using SevenZip;
using Threading;

namespace MassEffectModManagerCore.modmanager
{
    public partial class Mod
    {
        public bool SelectedForImport { get; set; } = true; //Default check on

        private BlockingCollection<string> compressionQueue;
        private object compressionCompletedSignaler = new object();
        public void ExtractFromArchive(string archivePath, string outputFolderPath, bool compressPackages, Action<string> updateTextCallback = null, Action<ProgressEventArgs> extractingCallback = null, Action<string, int, int> compressedPackageCallback = null)
        {
            if (!IsInArchive) throw new Exception("Cannot extract a mod that is not part of an archive.");
            compressPackages &= Game == MEGame.ME3; //ME3 ONLY FOR NOW
            using (var archiveFile = new SevenZipExtractor(archivePath))
            {
                var fileIndicesToExtract = new List<int>();
                var referencedFiles = GetAllRelativeReferences(archiveFile);

                if (!IsVirtualized)
                {
                    referencedFiles.Add(ModDescPath);
                }

                foreach (var info in archiveFile.ArchiveFileData)
                {
                    if (referencedFiles.Contains(info.FileName))
                    {
                        Debug.WriteLine("Add file to extraction list: " + info.FileName);
                        fileIndicesToExtract.Add(info.Index);
                    }
                }
                #region old
                /*
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
        }*/
                #endregion
                archiveFile.Extracting += (sender, args) => { extractingCallback?.Invoke(args); };

                string outputFilePathMapping(string entryPath)
                {
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
                    compressionThread = new NamedBackgroundWorker("ImportingCompressionThread");
                    compressionThread.DoWork += (a, b) =>
                    {
                        try
                        {
                            while (true)
                            {
                                var package = compressionQueue.Take();
                                //updateTextCallback?.Invoke($"Compressing {Path.GetFileName(package)}");
                                var p = MEPackageHandler.OpenMEPackage(package);
                                //Check if any compressed textures.
                                bool shouldNotCompress = false;
                                foreach (var texture in p.Exports.Where(x => x.IsTexture()))
                                {
                                    var storageType = Texture2D.GetTopMipStorageType(texture);
                                    shouldNotCompress |= storageType == ME3Explorer.Unreal.StorageTypes.pccLZO || storageType == ME3Explorer.Unreal.StorageTypes.pccZlib;
                                }

                                if (!shouldNotCompress)
                                {
                                    compressedPackageCallback?.Invoke($"Compressing {Path.GetFileName(package)}", compressedPackageCount, numberOfPackagesToCompress);
                                    Log.Information("Compressing package: " + package);
                                    p.save(true);
                                }
                                else
                                {
                                    Log.Information("Not compressing package due to file containing compressed textures: " + package);
                                }


                                Interlocked.Increment(ref compressedPackageCount);
                                compressedPackageCallback?.Invoke($"Compressed {Path.GetFileName(package)}", compressedPackageCount, numberOfPackagesToCompress);
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
                archiveFile.FileExtractionFinished += async (sender, args) =>
                {
                    if (compressPackages)
                    {

                        var fToCompress = outputFilePathMapping(args.FileInfo.FileName);
                        if (fToCompress.RepresentsPackageFilePath())
                        {
                            //Debug.WriteLine("Adding to blocking queue");
                            compressionQueue.TryAdd(fToCompress);
                        }
                    }

                    //    await compressPackage(fToCompress);
                    //    var p = MEPackageHandler.OpenMEPackage(fToCompress);
                    //    //Check if any compressed textures.
                    //    bool shouldNotCompress = false;
                    //    foreach (var texture in p.Exports.Where(x => x.IsTexture()))
                    //    {
                    //        var storageType = Texture2D.GetTopMipStorageType(texture);
                    //        shouldNotCompress |= storageType == ME3Explorer.Unreal.StorageTypes.pccLZO || storageType == ME3Explorer.Unreal.StorageTypes.pccZlib;
                    //    }
                    //    if (!shouldNotCompress)
                    //    {
                    //        compressedPackageCallback?.Invoke("Compressing " + Path.GetFileName(fToCompress), compressedPackageCount, numberOfPackagesToCompress);
                    //        Log.Information("Compressing package: " + fToCompress);
                    //        p.save(true);
                    //    }
                    //    else
                    //    {
                    //        Log.Information("Not compressing package due to file containing compressed textures: " + fToCompress);
                    //    }
                    //    compressedPackageCount++;
                    //};
                };



                archiveFile.ExtractFiles(outputFolderPath, outputFilePathMapping, fileIndicesToExtract.ToArray());
                compressionQueue?.CompleteAdding();
                if (compressPackages && numberOfPackagesToCompress > 0 && numberOfPackagesToCompress > compressedPackageCount)
                {
                    Log.Information("Waiting for compression of packages to complete.");
                    while (!compressionQueue.IsCompleted)
                    {
                        lock (compressionCompletedSignaler)
                        {
                            Monitor.Wait(compressionCompletedSignaler);
                        }
                    }

                    Log.Information("Package compression has completed.");
                }
                ModPath = outputFolderPath;
                if (IsVirtualized)
                {
                    var parser = new IniDataParser().Parse(VirtualizedIniText);
                    parser["ModInfo"]["modver"] = ModVersionString; //In event relay service resolved this
                    File.WriteAllText(Path.Combine(ModPath, "moddesc.ini"), parser.ToString());
                }

                //int packagesCompressed = 0;
                //if (compressPackages)
                //{
                //    var packages = Utilities.GetPackagesInDirectory(ModPath, true);
                //    extractingCallback?.Invoke(new ProgressEventArgs((byte)(packagesCompressed * 100.0 / packages.Count), 0));
                //    foreach (var package in packages)
                //    {
                //        updateTextCallback?.Invoke($"Compressing {Path.GetFileName(package)}");
                //        Log.Information("Compressing package: " + package);
                //        var p = MEPackageHandler.OpenMEPackage(package);
                //        p.save(true);

                //        packagesCompressed++;
                //        extractingCallback?.Invoke(new ProgressEventArgs((byte)(packagesCompressed * 100.0 / packages.Count), 0));
                //    }
                //}
            }
        }
    }
}
