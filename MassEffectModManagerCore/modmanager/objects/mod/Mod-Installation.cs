using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using MassEffectModManager;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.objects;
using Serilog;

namespace MassEffectModManagerCore.modmanager
{
    public partial class Mod
    {
        public (Dictionary<ModJob, Dictionary<string, string>>, List<(ModJob job, string sfarPath)>) GetInstallationQueues(GameTarget gameTarget)
        {
            var gameDLCPath = MEDirectories.DLCPath(gameTarget);
            var customDLCMapping = InstallationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.CUSTOMDLC)?.CustomDLCFolderMapping;
            if (customDLCMapping != null)
            {
                //Make clone so original value is not modified
                customDLCMapping = new Dictionary<string, string>(customDLCMapping); //prevent altering the source object
            }

            var unpackedJobInstallationMapping = new Dictionary<ModJob, Dictionary<string, string>>();
            var sfarInstallationJobs = new List<(ModJob job, string sfarPath)>();
            foreach (var job in InstallationJobs)
            {
                Log.Information($"Preprocessing installation job: {job.Header}");
                var alternateFiles = job.AlternateFiles.Where(x => x.IsSelected).ToList();
                var alternateDLC = job.AlternateDLCs.Where(x => x.IsSelected).ToList();
                if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                {
                    #region Installation: CustomDLC
                    var installationMapping = new Dictionary<string, string>();
                    unpackedJobInstallationMapping[job] = installationMapping;
                    foreach (var altdlc in alternateDLC)
                    {
                        if (altdlc.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC)
                        {
                            customDLCMapping[altdlc.AlternateDLCFolder] = altdlc.DestinationDLCFolder;
                        }
                    }


                    foreach (var mapping in customDLCMapping)
                    {
                        //Mapping is done as DESTINATIONFILE = SOURCEFILE so you can override keys
                        var source = Path.Combine(ModPath, mapping.Key);
                        var target = Path.Combine(gameDLCPath, mapping.Value);

                        //get list of all normal files we will install
                        var allSourceDirFiles = Directory.GetFiles(source, "*", SearchOption.AllDirectories).Select(x => x.Substring(ModPath.Length)).ToList();

                        //loop over every file 
                        foreach (var sourceFile in allSourceDirFiles)
                        {

                            bool altApplied = false;
                            foreach (var altFile in alternateFiles)
                            {
                                //todo: Support wildcards if OP_NOINSTALL
                                if (altFile.ModFile.Equals(sourceFile, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    //Alt applies to this file
                                    switch (altFile.Operation)
                                    {
                                        case AlternateFile.AltFileOperation.OP_NOINSTALL:
                                            CLog.Information($"Not installing {sourceFile} for Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL", Settings.LogModInstallation);
                                            altApplied = true;
                                            break;
                                        case AlternateFile.AltFileOperation.OP_SUBSTITUTE:
                                            CLog.Information($"Repointing {sourceFile} to {altFile.AltFile} for Alternate File {altFile.FriendlyName} due to operation OP_SUBSTITUTE", Settings.LogModInstallation);
                                            installationMapping[altFile.AltFile] = sourceFile; //use alternate file as key instead
                                            altApplied = true;
                                            break;
                                    }
                                    break;
                                }
                            }

                            if (altApplied) continue; //no further processing for file
                            installationMapping[sourceFile] = sourceFile; //Nothing different, just add to installation list
                        }

                        foreach (var altdlc in alternateDLC)
                        {
                            if (altdlc.Operation == AlternateDLC.AltDLCOperation.OP_ADD_FOLDERFILES_TO_CUSTOMDLC)
                            {
                                string alternatePathRoot = FilesystemInterposer.PathCombine(IsInArchive, ModPath, altdlc.AlternateDLCFolder);
                                //Todo: Change to Filesystem Interposer to support installation from archives
                                var filesToAdd = Directory.GetFiles(alternatePathRoot).Select(x => x.Substring(ModPath.Length).TrimStart('\\')).ToList();
                                foreach (var fileToAdd in filesToAdd)
                                {
                                    var destFile = Path.Combine(altdlc.DestinationDLCFolder, Path.GetFileName(fileToAdd));
                                    CLog.Information($"Adding extra CustomDLC file ({fileToAdd} => {destFile}) due to Alternate DLC {altdlc.FriendlyName}'s {altdlc.Operation}", Settings.LogModInstallation);

                                    installationMapping[destFile] = fileToAdd;
                                }
                            }
                        }

                        //Log.Information($"Copying CustomDLC to target: {source} -> {target}");
                        //CopyDir.CopyFiles_ProgressBar(installationMapping, FileInstalledCallback);
                        //Log.Information($"Installed CustomDLC {mapping.Value}");
                    }

                    #endregion
                }
                else if (job.Header == ModJob.JobHeader.BASEGAME || job.Header == ModJob.JobHeader.BALANCE_CHANGES)
                {
                    #region Installation: BASEGAME
                    var installationMapping = new Dictionary<string, string>();
                    unpackedJobInstallationMapping[job] = installationMapping;
                    buildUnpackedInstallationQueue(job, installationMapping);
                    #endregion
                }
                else if (Game == MEGame.ME3 && ModJob.SupportedNonCustomDLCJobHeaders.Contains(job.Header)) //previous else if will catch BASEGAME
                {
                    #region Installation: DLC (Unpacked and SFAR) - ME3 ONLY
                    string sfarPath = job.Header == ModJob.JobHeader.TESTPATCH ? Utilities.GetTestPatchPath(gameTarget) : Path.Combine(gameDLCPath, ModJob.HeadersToDLCNamesMap[job.Header], "CookedPCConsole", "Default.sfar");


                    if (File.Exists(sfarPath))
                    {
                        if (new FileInfo(sfarPath).Length == 32)
                        {
                            //Unpacked
                            var installationMapping = new Dictionary<string, string>();
                            unpackedJobInstallationMapping[job] = installationMapping;
                            buildUnpackedInstallationQueue(job, installationMapping);
                        }
                        else
                        {
                            //Packed
                            sfarInstallationJobs.Add((job, sfarPath));
                        }
                    }
                    else
                    {
                        Log.Warning($"SFAR doesn't exist {sfarPath}, skipping job: {job.Header}");
                    }
                    #endregion
                }
                else
                {
                    //BINI
                    throw new Exception("Unsupported installation job header! " + job.Header);
                }
            }

            return (unpackedJobInstallationMapping, sfarInstallationJobs);
        }

        private void buildUnpackedInstallationQueue(ModJob job, Dictionary<string, string> installationMapping)
        {
            foreach (var entry in job.FilesToInstall)
            {
                //Key is destination, value is source file
                var destFile = entry.Key;
                var sourceFile = entry.Value;

                bool altApplied = false;
                foreach (var altFile in job.AlternateFiles)
                {
                    //todo: Support wildcards if OP_NOINSTALL
                    if (altFile.ModFile.Equals(sourceFile, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //Alt applies to this file
                        switch (altFile.Operation)
                        {
                            case AlternateFile.AltFileOperation.OP_NOINSTALL:
                                CLog.Information($"Not installing {sourceFile} for Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL", Settings.LogModInstallation);
                                altApplied = true;
                                break;
                            case AlternateFile.AltFileOperation.OP_SUBSTITUTE:
                                CLog.Information($"Repointing {sourceFile} to {altFile.AltFile} for Alternate File {altFile.FriendlyName} due to operation OP_SUBSTITUTE", Settings.LogModInstallation);
                                installationMapping[altFile.AltFile] = sourceFile; //use alternate file as key instead
                                altApplied = true;
                                break;
                        }
                        break;
                    }
                }

                if (altApplied) continue; //no further processing for file
                //installationMapping[sourceFile] = sourceFile; //Nothing different, just add to installation list


                installationMapping[destFile] = sourceFile;
                Log.Information($"Adding {job.Header} file to installation queue: {entry.Value} -> {destFile}");

            }
        }
    }
}
