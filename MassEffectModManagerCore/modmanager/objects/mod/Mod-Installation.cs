using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.objects;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore.modmanager
{
    public partial class Mod
    {
        public (Dictionary<ModJob, (Dictionary<string, string> unpackedJobMapping, List<string> dlcFoldersBeingInstalled)>, List<(ModJob job, string sfarPath, Dictionary<string, string>)>) GetInstallationQueues(GameTarget gameTarget)
        {
            if (IsInArchive) Archive = new SevenZipExtractor(ArchivePath); //load archive file for inspection
            var gameDLCPath = MEDirectories.DLCPath(gameTarget);
            var customDLCMapping = InstallationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.CUSTOMDLC)?.CustomDLCFolderMapping;
            if (customDLCMapping != null)
            {
                //Make clone so original value is not modified
                customDLCMapping = new Dictionary<string, string>(customDLCMapping); //prevent altering the source object
            }

            var unpackedJobInstallationMapping = new Dictionary<ModJob, (Dictionary<string, string> mapping, List<string> dlcFoldersBeingInstalled)>();
            var sfarInstallationJobs = new List<(ModJob job, string sfarPath, Dictionary<string, string> installationMapping)>();
            foreach (var job in InstallationJobs)
            {
                Log.Information($@"Preprocessing installation job: {job.Header}");
                var alternateFiles = job.AlternateFiles.Where(x => x.IsSelected).ToList();
                var alternateDLC = job.AlternateDLCs.Where(x => x.IsSelected).ToList();
                if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                {
                    #region Installation: CustomDLC
                    var installationMapping = new Dictionary<string, string>();
                    unpackedJobInstallationMapping[job] = (installationMapping, new List<string>());
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
                        var source = FilesystemInterposer.PathCombine(IsInArchive, ModPath, mapping.Key);
                        var target = Path.Combine(gameDLCPath, mapping.Value);

                        //get list of all normal files we will install
                        //var allSourceDirFiles = Directory.GetFiles(source, "*", SearchOption.AllDirectories).Select(x => x.Substring(ModPath.Length)).ToList();
                        var allSourceDirFiles = FilesystemInterposer.DirectoryGetFiles(source, "*", SearchOption.AllDirectories, Archive).Select(x => x.Substring(ModPath.Length).TrimStart('\\')).ToList();
                        unpackedJobInstallationMapping[job].dlcFoldersBeingInstalled.Add(target);
                        //loop over every file 
                        foreach (var sourceFile in allSourceDirFiles)
                        {
                            //Check against alt files
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
                                            CLog.Information($@"Not installing {sourceFile} for Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL", Settings.LogModInstallation);
                                            altApplied = true;
                                            break;
                                        case AlternateFile.AltFileOperation.OP_SUBSTITUTE:
                                            CLog.Information($@"Repointing {sourceFile} to {altFile.AltFile} for Alternate File {altFile.FriendlyName} due to operation OP_SUBSTITUTE", Settings.LogModInstallation);
                                            installationMapping[altFile.AltFile] = sourceFile; //use alternate file as key instead
                                            altApplied = true;
                                            break;
                                        case AlternateFile.AltFileOperation.OP_INSTALL:
                                            //same logic as substitute, just different logging.
                                            CLog.Information($@"Adding {sourceFile} to install (from {altFile.AltFile}) as part of Alternate File {altFile.FriendlyName} due to operation OP_INSTALL", Settings.LogModInstallation);
                                            installationMapping[altFile.AltFile] = sourceFile; //use alternate file as key instead
                                            altApplied = true;
                                            break;
                                    }
                                    break;
                                }
                            }

                            if (altApplied) continue; //no further processing for file
                            var relativeDestStartIndex = sourceFile.IndexOf(mapping.Value);
                            string destPath = sourceFile.Substring(relativeDestStartIndex);
                            installationMapping[destPath] = sourceFile; //destination is mapped to source file that will replace it.
                        }

                        foreach (var altdlc in alternateDLC)
                        {
                            if (altdlc.Operation == AlternateDLC.AltDLCOperation.OP_ADD_FOLDERFILES_TO_CUSTOMDLC)
                            {
                                string alternatePathRoot = FilesystemInterposer.PathCombine(IsInArchive, ModPath, altdlc.AlternateDLCFolder);
                                //Todo: Change to Filesystem Interposer to support installation from archives
                                var filesToAdd = FilesystemInterposer.DirectoryGetFiles(alternatePathRoot, "*", SearchOption.AllDirectories, Archive).Select(x => x.Substring(ModPath.Length).TrimStart('\\')).ToList();
                                foreach (var fileToAdd in filesToAdd)
                                {
                                    var destFile = Path.Combine(altdlc.DestinationDLCFolder, fileToAdd.Substring(altdlc.AlternateDLCFolder.Length).TrimStart('\\', '/'));
                                    CLog.Information($@"Adding extra CustomDLC file ({fileToAdd} => {destFile}) due to Alternate DLC {altdlc.FriendlyName}'s {altdlc.Operation}", Settings.LogModInstallation);

                                    installationMapping[destFile] = fileToAdd;
                                }
                            }
                        }

                        //Log.Information($"Copying CustomDLC to target: {source} -> {target}");
                        //CopyDir.CopyFiles_ProgressBar(installatnionMapping, FileInstalledCallback);
                        //Log.Information($"Installed CustomDLC {mapping.Value}");
                    }

                    #endregion
                }
                else if (job.Header == ModJob.JobHeader.BASEGAME || job.Header == ModJob.JobHeader.BALANCE_CHANGES || job.Header == ModJob.JobHeader.ME1_CONFIG)
                {
                    #region Installation: BASEGAME, BALANCE CHANGES, ME1 CONFIG
                    var installationMapping = new Dictionary<string, string>();
                    unpackedJobInstallationMapping[job] = (installationMapping, new List<string>());
                    buildInstallationQueue(job, installationMapping, false);
                    #endregion
                }
                else if (Game == MEGame.ME3 && ModJob.ME3SupportedNonCustomDLCJobHeaders.Contains(job.Header)) //previous else if will catch BASEGAME
                {
                    #region Installation: DLC Unpacked and SFAR (ME3 ONLY)

                    if (MEDirectories.IsOfficialDLCInstalled(job.Header, gameTarget))
                    {
                        string sfarPath = job.Header == ModJob.JobHeader.TESTPATCH ? Utilities.GetTestPatchPath(gameTarget) : Path.Combine(gameDLCPath, ModJob.GetHeadersToDLCNamesMap(MEGame.ME3)[job.Header], @"CookedPCConsole", @"Default.sfar");


                        if (File.Exists(sfarPath))
                        {
                            var installationMapping = new Dictionary<string, string>();
                            if (new FileInfo(sfarPath).Length == 32)
                            {
                                //Unpacked
                                unpackedJobInstallationMapping[job] = (installationMapping, new List<string>());
                                buildInstallationQueue(job, installationMapping, false);
                            }
                            else
                            {
                                //Packed
                                //unpackedJobInstallationMapping[job] = installationMapping;
                                buildInstallationQueue(job, installationMapping, true);
                                sfarInstallationJobs.Add((job, sfarPath, installationMapping));
                            }
                        }
                    }
                    else
                    {
                        Log.Warning($@"DLC not installed, skipping: {job.Header}");
                    }
                    #endregion
                }
                else if (Game == MEGame.ME2 || Game == MEGame.ME1)
                {
                    #region Installation: DLC Unpacked (ME1/ME2 ONLY)
                    //Unpacked
                    if (MEDirectories.IsOfficialDLCInstalled(job.Header, gameTarget))
                    {
                        var installationMapping = new Dictionary<string, string>();
                        unpackedJobInstallationMapping[job] = (installationMapping, new List<string>());
                        buildInstallationQueue(job, installationMapping, false);
                    }
                    else
                    {
                        Log.Warning($@"DLC not installed, skipping: {job.Header}");
                    }

                    #endregion
                }
                else
                {
                    //?? Header
                    throw new Exception(@"Unsupported installation job header! " + job.Header);
                }
            }

            return (unpackedJobInstallationMapping, sfarInstallationJobs);
        }

        private void buildInstallationQueue(ModJob job, Dictionary<string, string> installationMapping, bool isSFAR)
        {
            CLog.Information(@"Building installation queue for " + job.Header, Settings.LogModInstallation);
            foreach (var entry in job.FilesToInstall)
            {
                //Key is destination, value is source file
                var destFile = entry.Key;
                var sourceFile = entry.Value;

                bool altApplied = false;
                foreach (var altFile in job.AlternateFiles.Where(x=>x.IsSelected))
                {
                    Debug.WriteLine(@"Checking alt conditions for application: " + altFile.FriendlyName);
                    if (altFile.Operation == AlternateFile.AltFileOperation.OP_NOTHING) continue; //skip nothing
                    //todo: Support wildcards if OP_NOINSTALL
                    if (altFile.ModFile.Equals(destFile, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //Alt applies to this file
                        switch (altFile.Operation)
                        {
                            case AlternateFile.AltFileOperation.OP_NOINSTALL:
                                CLog.Information($@"Not installing {sourceFile} for Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL", Settings.LogModInstallation);
                                altApplied = true;
                                break;
                            case AlternateFile.AltFileOperation.OP_SUBSTITUTE:
                                CLog.Information($@"Repointing {sourceFile} to {altFile.AltFile} for Alternate File {altFile.FriendlyName} due to operation OP_SUBSTITUTE", Settings.LogModInstallation);
                                installationMapping[altFile.AltFile] = sourceFile; //use alternate file as key instead
                                altApplied = true;
                                break;
                            case AlternateFile.AltFileOperation.OP_INSTALL:
                                //same logic as substitute, just different logging.
                                CLog.Information($@"Adding {sourceFile} to install (from {altFile.AltFile}) as part of Alternate File {altFile.FriendlyName} due to operation OP_INSTALL", Settings.LogModInstallation);
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
                CLog.Information($@"Adding {job.Header} file to installation {(isSFAR ? @"SFAR" : @"unpacked")} queue: {entry.Value} -> {destFile}", Settings.LogModInstallation); //do not localize

            }
        }

        /// <summary>
        /// Validates this mod can install against a game target with respect to the list of RequiredDLC. 
        /// </summary>
        /// <param name="gameTarget">Target to validate against</param>
        /// <returns>List of missing DLC modules, or an empty list if none</returns>
        internal List<string> ValidateRequiredModulesAreInstalled(GameTarget gameTarget)
        {
            if (gameTarget.Game != Game)
            {
                throw new Exception(@"Cannot validate a mod against a gametarget that is not for its game");
            }

            var requiredDLC = RequiredDLC.Select(x =>
            {
                if (Enum.TryParse(x, out ModJob.JobHeader parsedHeader) && ModJob.GetHeadersToDLCNamesMap(Game).TryGetValue(parsedHeader, out var dlcname))
                {
                    return dlcname;
                }
                return x;
            });
            var installedDLC = MEDirectories.GetInstalledDLC(gameTarget);
            return requiredDLC.Except(installedDLC).ToList();
        }

        internal List<string> GetAllRelativeReadonlyTargets(bool includeME1Config)
        {
            var list = new List<string>();
            foreach (var job in InstallationJobs)
            {
                if (includeME1Config && job.Header == ModJob.JobHeader.ME1_CONFIG)
                {
                    list.AddRange(job.FilesToInstall.Keys);
                }
                else
                {
                    foreach (var item in job.ReadOnlyIndicators)
                    {
                        var destPath = job.FilesToInstall.FirstOrDefault(x => x.Value.Equals(item, StringComparison.InvariantCultureIgnoreCase));
                        if (destPath.Key == null) Log.Error(@"Error: Bug triggered: destPath for addreadonly files returned null!");
                        list.Add(destPath.Key); //pathcombine?
                    }
                }
            }
            return list;
        }
    }
}
