using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using Serilog;
using SevenZip;

namespace MassEffectModManagerCore.modmanager
{
    public partial class Mod
    {
        /// <summary>
        /// Builds the installation queues for the mod. Item 1 is the unpacked job mappings (per modjob) along with the list of custom dlc folders being installed. Item 2 is the list of modjobs, their sfar paths, and the list of source files to install for SFAR jobs.
        /// </summary>
        /// <param name="gameTarget"></param>
        /// <returns></returns>
        public (Dictionary<ModJob, (Dictionary<string, InstallSourceFile> unpackedJobMapping, List<string> dlcFoldersBeingInstalled)>, List<(ModJob job, string sfarPath, Dictionary<string, InstallSourceFile>)>) GetInstallationQueues(GameTarget gameTarget)
        {
            if (IsInArchive) Archive = new SevenZipExtractor(ArchivePath); //load archive file for inspection
            var gameDLCPath = MEDirectories.DLCPath(gameTarget);
            var customDLCMapping = InstallationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.CUSTOMDLC)?.CustomDLCFolderMapping;
            if (customDLCMapping != null)
            {
                //Make clone so original value is not modified
                customDLCMapping = new Dictionary<string, string>(customDLCMapping); //prevent altering the source object
            }

            var unpackedJobInstallationMapping = new Dictionary<ModJob, (Dictionary<string, InstallSourceFile> mapping, List<string> dlcFoldersBeingInstalled)>();
            var sfarInstallationJobs = new List<(ModJob job, string sfarPath, Dictionary<string, InstallSourceFile> installationMapping)>();
            foreach (var job in InstallationJobs)
            {
                Log.Information($@"Preprocessing installation job: {job.Header}");
                var alternateFiles = job.AlternateFiles.Where(x => x.IsSelected && x.Operation != AlternateFile.AltFileOperation.OP_NOTHING
                && x.Operation != AlternateFile.AltFileOperation.OP_NOINSTALL_MULTILISTFILES).ToList();
                var alternateDLC = job.AlternateDLCs.Where(x => x.IsSelected).ToList();
                if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                {
                    #region Installation: CustomDLC
                    //Key = destination file, value = source file to install
                    var installationMapping = new Dictionary<string, InstallSourceFile>();
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
                        var allSourceDirFiles = FilesystemInterposer.DirectoryGetFiles(source, "*", SearchOption.AllDirectories, Archive).Select(x => x.Substring(ModPath.Length).TrimStart('\\')).ToList();
                        unpackedJobInstallationMapping[job].dlcFoldersBeingInstalled.Add(target);
                        //loop over every file 
                        foreach (var sourceFile in allSourceDirFiles)
                        {
                            //Check against alt files
                            bool altApplied = false;
                            foreach (var altFile in alternateFiles)
                            {
                                if (altFile.ModFile.Equals(sourceFile, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    //Alt applies to this file
                                    switch (altFile.Operation)
                                    {
                                        case AlternateFile.AltFileOperation.OP_NOINSTALL:
                                            CLog.Information($@"Not installing {sourceFile} for Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL", Settings.LogModInstallation);
                                            //we simply don't map as we just do a continue below.
                                            altApplied = true;
                                            break;
                                        case AlternateFile.AltFileOperation.OP_SUBSTITUTE:
                                            CLog.Information($@"Repointing {sourceFile} to {altFile.AltFile} for Alternate File {altFile.FriendlyName} due to operation OP_SUBSTITUTE", Settings.LogModInstallation);
                                            if (job.JobDirectory != null && altFile.AltFile.StartsWith(job.JobDirectory))
                                            {
                                                installationMapping[sourceFile] = new InstallSourceFile(altFile.AltFile.Substring(job.JobDirectory.Length).TrimStart('/', '\\'))
                                                {
                                                    AltApplied = true,
                                                    IsFullRelativeFilePath = true
                                                };//use alternate file as key instead
                                            }
                                            else
                                            {
                                                installationMapping[sourceFile] = new InstallSourceFile(altFile.AltFile) { AltApplied = true, IsFullRelativeFilePath = true }; //use alternate file as key instead
                                            }
                                            altApplied = true;
                                            break;
                                        case AlternateFile.AltFileOperation.OP_INSTALL:
                                            //same logic as substitute, just different logging.
                                            CLog.Information($@"Adding {sourceFile} to install (from {altFile.AltFile}) as part of Alternate File {altFile.FriendlyName} due to operation OP_INSTALL", Settings.LogModInstallation);
                                            if (job.JobDirectory != null && altFile.AltFile.StartsWith(job.JobDirectory))
                                            {
                                                installationMapping[sourceFile] = new InstallSourceFile(altFile.AltFile.Substring(job.JobDirectory.Length).TrimStart('/', '\\'))
                                                {
                                                    AltApplied = true,
                                                    IsFullRelativeFilePath = true
                                                }; //use alternate file as key instead
                                            }
                                            else
                                            {
                                                installationMapping[sourceFile] = new InstallSourceFile(altFile.AltFile) { AltApplied = true, IsFullRelativeFilePath = true }; //use alternate file as key instead
                                            }
                                            altApplied = true;
                                            break;
                                    }
                                    break;
                                }
                            }

                            if (altApplied) continue; //no further processing for file
                            var relativeDestStartIndex = sourceFile.IndexOf(mapping.Value);
                            string destPath = sourceFile.Substring(relativeDestStartIndex);
                            installationMapping[destPath] = new InstallSourceFile(sourceFile); //destination is mapped to source file that will replace it.
                        }

                        foreach (var altdlc in alternateDLC)
                        {
                            if (altdlc.Operation == AlternateDLC.AltDLCOperation.OP_ADD_FOLDERFILES_TO_CUSTOMDLC)
                            {
                                string alternatePathRoot = FilesystemInterposer.PathCombine(IsInArchive, ModPath, altdlc.AlternateDLCFolder);
                                var filesToAdd = FilesystemInterposer.DirectoryGetFiles(alternatePathRoot, "*", SearchOption.AllDirectories, Archive).Select(x => x.Substring(ModPath.Length).TrimStart('\\')).ToList();
                                foreach (var fileToAdd in filesToAdd)
                                {
                                    var destFile = Path.Combine(altdlc.DestinationDLCFolder, fileToAdd.Substring(altdlc.AlternateDLCFolder.Length).TrimStart('\\', '/'));
                                    CLog.Information($@"Adding extra CustomDLC file ({fileToAdd} => {destFile}) due to Alternate DLC {altdlc.FriendlyName}'s {altdlc.Operation}", Settings.LogModInstallation);

                                    installationMapping[destFile] = new InstallSourceFile(fileToAdd) { AltApplied = true };
                                }
                            }
                            else if (altdlc.Operation == AlternateDLC.AltDLCOperation.OP_ADD_MULTILISTFILES_TO_CUSTOMDLC)
                            {
                                string alternatePathRoot = FilesystemInterposer.PathCombine(IsInArchive, ModPath, altdlc.MultiListRootPath);
                                foreach (var fileToAdd in altdlc.MultiListSourceFiles)
                                {
                                    var sourceFile = FilesystemInterposer.PathCombine(IsInArchive, alternatePathRoot, fileToAdd).Substring(ModPath.Length).TrimStart('\\');
                                    var destFile = Path.Combine(altdlc.DestinationDLCFolder, fileToAdd.TrimStart('\\', '/'));
                                    CLog.Information($@"Adding extra CustomDLC file (MultiList) ({sourceFile} => {destFile}) due to Alternate DLC {altdlc.FriendlyName}'s {altdlc.Operation}", Settings.LogModInstallation);

                                    installationMapping[destFile] = new InstallSourceFile(sourceFile) { AltApplied = true };
                                }
                            }
                        }

                        // Process altfile removal of multilist, since it should be done last
                        var fileRemoveAltFiles = job.AlternateFiles.Where(x => x.IsSelected && x.Operation == AlternateFile.AltFileOperation.OP_NOINSTALL_MULTILISTFILES);
                        foreach (var altFile in fileRemoveAltFiles)
                        {
                            foreach (var multifile in altFile.MultiListSourceFiles)
                            {
                                CLog.Information($@"Attempting to remove multilist file {multifile} from install (from {altFile.MultiListTargetPath}) as part of Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL_MULTILISTFILES", Settings.LogModInstallation);
                                string relativeSourcePath = altFile.MultiListRootPath + '\\' + multifile;

                                var targetPath = altFile.MultiListTargetPath + '\\' + multifile;
                                if (installationMapping.Remove(targetPath))
                                {
                                    CLog.Information($@" > Removed multilist file {targetPath} from installation",
                                    Settings.LogModInstallation);
                                }
                            }
                        }
                    }
                    #endregion
                }
                else if (job.Header == ModJob.JobHeader.LOCALIZATION)
                {
                    #region Installation: LOCALIZATION
                    var installationMapping = new CaseInsensitiveDictionary<InstallSourceFile>();
                    unpackedJobInstallationMapping[job] = (installationMapping, new List<string>());
                    buildInstallationQueue(job, installationMapping, false);
                    #endregion
                }
                else if (job.Header == ModJob.JobHeader.BASEGAME || job.Header == ModJob.JobHeader.BALANCE_CHANGES || job.Header == ModJob.JobHeader.ME1_CONFIG)
                {
                    #region Installation: BASEGAME, BALANCE CHANGES, ME1 CONFIG
                    var installationMapping = new CaseInsensitiveDictionary<InstallSourceFile>();
                    unpackedJobInstallationMapping[job] = (installationMapping, new List<string>());
                    buildInstallationQueue(job, installationMapping, false);
                    #endregion
                }
                else if (Game == MEGame.ME3 && ModJob.ME3SupportedNonCustomDLCJobHeaders.Contains(job.Header)) //previous else if will catch BASEGAME
                {
                    #region Installation: DLC Unpacked and SFAR (ME3 ONLY)

                    if (MEDirectories.IsOfficialDLCInstalled(job.Header, gameTarget))
                    {
                        string sfarPath = job.Header == ModJob.JobHeader.TESTPATCH ? ME3Directory.GetTestPatchPath(gameTarget) : Path.Combine(gameDLCPath, ModJob.GetHeadersToDLCNamesMap(MEGame.ME3)[job.Header], @"CookedPCConsole", @"Default.sfar");


                        if (File.Exists(sfarPath))
                        {
                            var installationMapping = new CaseInsensitiveDictionary<InstallSourceFile>();
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
                        var installationMapping = new CaseInsensitiveDictionary<InstallSourceFile>();
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

        private void buildInstallationQueue(ModJob job, CaseInsensitiveDictionary<InstallSourceFile> installationMapping, bool isSFAR)
        {
            CLog.Information(@"Building installation queue for " + job.Header, Settings.LogModInstallation);
            foreach (var entry in job.FilesToInstall)
            {
                //Key is destination, value is source file
                var destFile = entry.Key;
                var sourceFile = entry.Value;

                bool altApplied = false;
                foreach (var altFile in job.AlternateFiles.Where(x => x.IsSelected))
                {
                    Debug.WriteLine(@"Checking alt conditions for application: " + altFile.FriendlyName);
                    if (altFile.Operation == AlternateFile.AltFileOperation.OP_NOTHING) continue; //skip nothing
                    if (altFile.Operation == AlternateFile.AltFileOperation.OP_APPLY_MULTILISTFILES) continue; //do not apply in the main loop.
                    if (altFile.Operation == AlternateFile.AltFileOperation.OP_NOINSTALL_MULTILISTFILES) continue; //do not apply in the main loop.

                    if (altFile.ModFile.Equals(destFile, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //Alt applies to this file
                        switch (altFile.Operation)
                        {
                            case AlternateFile.AltFileOperation.OP_NOINSTALL:
                                CLog.Information($@"Not installing {destFile} for Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL", Settings.LogModInstallation);
                                //we simply don't map as we just do a continue below.
                                altApplied = true;
                                break;
                            case AlternateFile.AltFileOperation.OP_SUBSTITUTE:
                                CLog.Information($@"Repointing {destFile} to {altFile.AltFile} for Alternate File {altFile.FriendlyName} due to operation OP_SUBSTITUTE", Settings.LogModInstallation);
                                if (job.JobDirectory != null && (altFile.AltFile.StartsWith(job.JobDirectory) && job.Header == ModJob.JobHeader.CUSTOMDLC))
                                {
                                    installationMapping[destFile] = new InstallSourceFile(altFile.AltFile.Substring(job.JobDirectory.Length).TrimStart('/', '\\'))
                                    {
                                        AltApplied = true,
                                        IsFullRelativeFilePath = true
                                    }; //use alternate file as key instead
                                }
                                else
                                {
                                    installationMapping[destFile] = new InstallSourceFile(altFile.AltFile)
                                    {
                                        AltApplied = true,
                                        IsFullRelativeFilePath = true
                                    }; //use alternate file as key instead
                                }

                                altApplied = true;
                                break;
                            case AlternateFile.AltFileOperation.OP_INSTALL:
                                //same logic as substitute, just different logging.
                                CLog.Information($@"Adding {sourceFile} to install (from {altFile.AltFile}) as part of Alternate File {altFile.FriendlyName} due to operation OP_INSTALL", Settings.LogModInstallation);
                                if (job.JobDirectory != null && (altFile.AltFile.StartsWith(job.JobDirectory) && job.Header == ModJob.JobHeader.CUSTOMDLC))
                                {
                                    installationMapping[destFile] = new InstallSourceFile(altFile.AltFile.Substring(job.JobDirectory.Length).TrimStart('/', '\\'))
                                    {
                                        AltApplied = true,
                                        IsFullRelativeFilePath = true
                                    }; //use alternate file as key instead
                                }
                                else
                                {
                                    installationMapping[destFile] = new InstallSourceFile(altFile.AltFile)
                                    {
                                        AltApplied = true,
                                        IsFullRelativeFilePath = true
                                    }; //use alternate file as key instead
                                }
                                altApplied = true;
                                break;
                        }
                        break;
                    }
                }

                if (altApplied) continue; //no further processing for file
                                          //installationMapping[sourceFile] = sourceFile; //Nothing different, just add to installation list


                installationMapping[destFile] = new InstallSourceFile(sourceFile);
                CLog.Information($@"Adding {job.Header} file to installation {(isSFAR ? @"SFAR" : @"unpacked")} queue: {entry.Value} -> {destFile}", Settings.LogModInstallation); //do not localize

            }

            //Apply autolist alternate files

            foreach (var altFile in job.AlternateFiles.Where(x => x.IsSelected && x.Operation == AlternateFile.AltFileOperation.OP_APPLY_MULTILISTFILES))
            {
                foreach (var multifile in altFile.MultiListSourceFiles)
                {
                    CLog.Information(
                        $@"Adding multilist file {multifile} to install (from {altFile.MultiListRootPath}) as part of Alternate File {altFile.FriendlyName} due to operation OP_APPLY_MULTILISTFILES",
                        Settings.LogModInstallation);
                    string relativeSourcePath = altFile.MultiListRootPath + '\\' + multifile;

                    var targetPath = altFile.MultiListTargetPath + '\\' + multifile;
                    installationMapping[targetPath] = new InstallSourceFile(relativeSourcePath)
                    {
                        AltApplied = true,
                        IsFullRelativeFilePath = true
                    }; //use alternate file as key instead
                       //}

                    //not sure if there should be an else case here.
                    //else
                    //{
                    //    installationMapping[destFile] = new InstallSourceFile(multifile)
                    //    {
                    //        AltApplied = true,
                    //        IsFullRelativeFilePath = true
                    //    }; //use alternate file as key instead
                    //}
                }
            }

            // Remove multilist noinstall files
            foreach (var altFile in job.AlternateFiles.Where(x => x.IsSelected && x.Operation == AlternateFile.AltFileOperation.OP_NOINSTALL_MULTILISTFILES))
            {
                foreach (var multifile in altFile.MultiListSourceFiles)
                {
                    CLog.Information(
                        $@"Attempting to remove multilist file {multifile} from install (from {altFile.MultiListRootPath}) as part of Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL_MULTILISTFILES",
                        Settings.LogModInstallation);
                    string relativeSourcePath = altFile.MultiListRootPath + '\\' + multifile;

                    var targetPath = altFile.MultiListTargetPath + '\\' + multifile;
                    if (installationMapping.Remove(targetPath))
                    {
                        CLog.Information($@" > Removed multilist file {targetPath} from installation",
                        Settings.LogModInstallation);
                    }
                    else
                    {
                        Log.Warning($@"Failed to remove multilist file from installation queue as specified by altfile: {targetPath}, path not present in installation files");
                    }
                }
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

        [DebuggerDisplay("InstallSourceFile {FilePath} IsFullRelPath: {IsFullRelativeFilePath}")] //do not localize
        public class InstallSourceFile
        {
            /// <summary>
            /// The path to the file. This is a relative path from the job root
            /// </summary>
            public string FilePath { get; set; }
            /// <summary>
            /// If this file is the result of an alternate operation
            /// </summary>
            public bool AltApplied { get; set; }
            /// <summary>
            /// If this path is a "full path", aka it should not include the job directory
            /// </summary>
            public bool IsFullRelativeFilePath { get; set; }

            public override bool Equals(object obj)
            {
                return obj is InstallSourceFile file &&
                       FilePath == file.FilePath;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(FilePath);
            }

            public InstallSourceFile(string path)
            {
                FilePath = path;
            }
        }
    }
}