using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Objects;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.modmanager.objects.tlk;
using SevenZip;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    public partial class Mod
    {
        /// <summary>
        /// Builds the installation mapping for the mod.
        /// </summary>
        /// <param name="gameTarget"></param>
        /// <returns></returns>
        public InstallMapping GetInstallationQueues(ModInstallOptionsPackage options)
        {
            if (IsInArchive)
            {
                // This is a hack to try to keep archive ready for use. It's not very reliable though...
#if DEBUG
                if (Archive.IsDisposed())
                {
                    Debug.WriteLine(@">>> ARCHIVE IS DISPOSED");
                }
#endif

                ReOpenArchiveIfNecessary();
            }

            InstallMapping installmapping = new InstallMapping();

            var gameDLCPath = M3Directories.GetDLCPath(options.InstallTarget);
            var customDLCMapping = InstallationJobs.FirstOrDefault(x => x.Header == ModJob.JobHeader.CUSTOMDLC)?.CustomDLCFolderMapping;
            if (customDLCMapping != null)
            {
                //Make clone so original value is not modified
                customDLCMapping = new Dictionary<string, string>(customDLCMapping); //prevent altering the source object
            }

            foreach (var job in InstallationJobs)
            {
                M3Log.Information($@"Preprocessing installation job: {job.Header}");

                // Not sure why we check the ops here and not for alternate DLC...
                var alternateFiles = options.SelectedOptions[job.Header].OfType<AlternateFile>().Where(x => x.Operation != AlternateFile.AltFileOperation.OP_NOTHING
                                                                                                                                && x.Operation != AlternateFile.AltFileOperation.OP_NOINSTALL_MULTILISTFILES).ToList();

                var alternateDLC = options.SelectedOptions[job.Header].OfType<AlternateDLC>().ToList();
                if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                {
                    #region Installation: CustomDLC
                    UnpackedFileMapping unpackedMapping = new UnpackedFileMapping();
                    var installationMapping = unpackedMapping.FileMapping; // This is just shortcut variable to make code a bit easier to read.

                    //Key = destination file, value = source file to install
                    //var installationMapping = new Dictionary<string, InstallSourceFile>();
                    //unpackedJobInstallationMapping[job] = (installationMapping, new List<string>());
                    foreach (var altdlc in alternateDLC)
                    {
                        if (altdlc.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC)
                        {
                            customDLCMapping[altdlc.AlternateDLCFolder] = altdlc.DestinationDLCFolder;
                        }
                    }

                    // Enumerate over all DLC folders to be installed.
                    foreach (var mapping in customDLCMapping)
                    {
                        //Mapping is done as DESTINATIONFILE = SOURCEFILE so you can override keys
                        var source = FilesystemInterposer.PathCombine(IsInArchive, ModPath, mapping.Key);
                        var target = Path.Combine(gameDLCPath, mapping.Value);

                        //get list of all normal files we will install
                        var allSourceDirFiles = FilesystemInterposer.DirectoryGetFiles(source, "*", SearchOption.AllDirectories, Archive).Select(x => x.Substring(ModPath.Length).TrimStart('\\')).ToList();
                        unpackedMapping.DLCFoldersBeingInstalled.Add(target);
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
                                            M3Log.Information($@"Not installing {sourceFile} for Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL", Settings.LogModInstallation);
                                            //we simply don't map as we just do a continue below.
                                            altApplied = true;
                                            break;
                                        case AlternateFile.AltFileOperation.OP_SUBSTITUTE:
                                            M3Log.Information($@"Repointing {sourceFile} to {altFile.AltFile} for Alternate File {altFile.FriendlyName} due to operation OP_SUBSTITUTE", Settings.LogModInstallation);
                                            if (job.JobDirectory != null && altFile.AltFile.StartsWith((string)job.JobDirectory))
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
                                            M3Log.Information($@"Adding {sourceFile} to install (from {altFile.AltFile}) as part of Alternate File {altFile.FriendlyName} due to operation OP_INSTALL", Settings.LogModInstallation);
                                            if (job.JobDirectory != null && altFile.AltFile.StartsWith((string)job.JobDirectory))
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
                            var relativeDestStartIndex = sourceFile.IndexOf(mapping.Key); // Source file will use mod directory name, so it must use key, not value (dest)
                            string destPath = mapping.Value + Path.DirectorySeparatorChar + sourceFile.Substring(relativeDestStartIndex + 1 + mapping.Key.Length);
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
                                    M3Log.Information($@"Adding extra CustomDLC file ({fileToAdd} => {destFile}) due to Alternate DLC {altdlc.FriendlyName}'s {altdlc.Operation}", Settings.LogModInstallation);

                                    installationMapping[destFile] = new InstallSourceFile(fileToAdd) { AltApplied = true };
                                }
                            }
                            else if (altdlc.Operation == AlternateDLC.AltDLCOperation.OP_ADD_MULTILISTFILES_TO_CUSTOMDLC)
                            {
                                string alternatePathRoot = FilesystemInterposer.PathCombine(IsInArchive, ModPath, altdlc.MultiListRootPath);
                                foreach (var fileToAdd in altdlc.MultiListSourceFiles)
                                {
                                    var realFileToAdd = fileToAdd;
                                    var sourceFile = FilesystemInterposer.PathCombine(IsInArchive, alternatePathRoot, realFileToAdd).Substring(ModPath.Length).TrimStart('\\');

                                    // ModDesc 8: Flattening output allows you to draw from multiple folders
                                    // and have a single output directory
                                    if (altdlc.FlattenMultilistOutput)
                                    {
                                        realFileToAdd = Path.GetFileName(fileToAdd);
                                    }

                                    var destFile = Path.Combine(altdlc.DestinationDLCFolder, realFileToAdd.TrimStart('\\', '/'));
                                    M3Log.Information($@"Adding extra CustomDLC file (MultiList) ({sourceFile} => {destFile}) due to Alternate DLC {altdlc.FriendlyName}'s {altdlc.Operation}", Settings.LogModInstallation);

                                    installationMapping[destFile] = new InstallSourceFile(sourceFile) { AltApplied = true };
                                }
                            }
                        }

                        // Process altfile removal of multilist, since it should be done last
                        var fileRemoveAltFiles = job.AlternateFiles.Where(x => x.UIIsSelected && x.Operation == AlternateFile.AltFileOperation.OP_NOINSTALL_MULTILISTFILES);
                        foreach (var altFile in fileRemoveAltFiles)
                        {
                            foreach (var multifile in altFile.MultiListSourceFiles)
                            {
                                M3Log.Information($@"Attempting to remove multilist file {multifile} from install (from {altFile.MultiListTargetPath}) as part of Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL_MULTILISTFILES", Settings.LogModInstallation);
                                string relativeSourcePath = altFile.MultiListRootPath + '\\' + multifile;

                                var targetPath = altFile.MultiListTargetPath + '\\' + multifile;
                                if (installationMapping.Remove(targetPath))
                                {
                                    M3Log.Information($@" > Removed multilist file {targetPath} from installation",
                                    Settings.LogModInstallation);
                                }
                            }
                        }
                    }

                    installmapping.UnpackedJobMappings[job] = unpackedMapping; // Map to installation of jobs.

                    #endregion
                }
                else if (job.Header == ModJob.JobHeader.LOCALIZATION)
                {
                    #region Installation: LOCALIZATION
                    installmapping.UnpackedJobMappings[job] = new UnpackedFileMapping();
                    buildInstallationQueue(job, installmapping.UnpackedJobMappings[job].FileMapping, false);
                    #endregion
                }
                else if (job.Header is ModJob.JobHeader.BASEGAME or ModJob.JobHeader.BALANCE_CHANGES or ModJob.JobHeader.ME1_CONFIG)
                {
                    #region Installation: BASEGAME, BALANCE CHANGES, ME1 CONFIG
                    installmapping.UnpackedJobMappings[job] = new UnpackedFileMapping();
                    buildInstallationQueue(job, installmapping.UnpackedJobMappings[job].FileMapping, false);
                    #endregion
                }
                else if (Game == MEGame.ME3 && ModJob.ME3SupportedNonCustomDLCJobHeaders.Contains(job.Header)) //previous else if will catch BASEGAME
                {
                    #region Installation: DLC Unpacked and SFAR (ME3 ONLY)

                    if (options.InstallTarget.IsOfficialDLCInstalled(job.Header))
                    {
                        string sfarPath = job.Header == ModJob.JobHeader.TESTPATCH ? M3Directories.GetTestPatchSFARPath(options.InstallTarget) : Path.Combine(gameDLCPath, ModJob.GetHeadersToDLCNamesMap(MEGame.ME3)[job.Header], @"CookedPCConsole", @"Default.sfar");

                        if (File.Exists(sfarPath))
                        {
                            var installationMapping = new CaseInsensitiveDictionary<InstallSourceFile>();
                            if (new FileInfo(sfarPath).Length == 32)
                            {
                                //Unpacked
                                installmapping.UnpackedJobMappings[job] = new UnpackedFileMapping();
                                buildInstallationQueue(job, installmapping.UnpackedJobMappings[job].FileMapping, false);
                            }
                            else
                            {
                                //Packed
                                buildInstallationQueue(job, installationMapping, true);
                                var SfarMapping = new SFARFileMapping()
                                {
                                    Job = job,
                                    SFARPath = sfarPath,
                                    SFARInstallationMapping = installationMapping
                                };
                                installmapping.SFARJobs.Add(SfarMapping);
                            }
                        }
                    }
                    else
                    {
                        M3Log.Warning($@"DLC not installed, skipping: {job.Header}");
                    }
                    #endregion
                }
                else if (Game is MEGame.ME2 or MEGame.ME1)
                {
                    #region Installation: DLC Unpacked (ME1/ME2 ONLY)
                    //Unpacked
                    if (options.InstallTarget.IsOfficialDLCInstalled(job.Header))
                    {
                        installmapping.UnpackedJobMappings[job] = new UnpackedFileMapping();
                        buildInstallationQueue(job, installmapping.UnpackedJobMappings[job].FileMapping, false);
                    }
                    else
                    {
                        M3Log.Warning($@"DLC not installed, skipping: {job.Header}");
                    }

                    #endregion
                }
                else if (job.Header == ModJob.JobHeader.LELAUNCHER && Game == MEGame.LELauncher)
                {
                    #region Installation: LELAUNCHER
                    installmapping.UnpackedJobMappings[job] = new UnpackedFileMapping();
                    buildInstallationQueue(job, installmapping.UnpackedJobMappings[job].FileMapping, false);
                    #endregion
                }
                else if (job.Header == ModJob.JobHeader.GAME1_EMBEDDED_TLK)
                {
                    #region Installation: GAME_EMBEDDED TLK
                    // We don't parse this here
                    #endregion
                }
                else if (job.Header == ModJob.JobHeader.HEADMORPHS)
                {
                    // Do nothing
                }
                else if (job.Header == ModJob.JobHeader.TEXTUREMODS)
                {
                    // Do nothing
                }
                else
                {
                    //?? Header
                    throw new Exception($@"Unsupported installation job header: {job.Header} for game {Game}");
                }
            }

            return installmapping;
        }

        /// <summary>
        /// Attempts to re-open the archive file if this mod is in an archive. Will re-open if the current thread does not match
        /// the one used to open the archive to help prevent issues.
        /// </summary>
        internal void ReOpenArchiveIfNecessary()
        {
            if (!IsInArchive)
                return; // Cannot open archive if mod is not serialized from archive
            if (Archive != null && !Archive.IsDisposed())
            {
                if (Archive.ThreadId == Thread.CurrentThread.ManagedThreadId)
                {
                    // We don't need to re-open this
                    return;
                }

                // Needs closed and re-opened

                Archive.DisposeObjectOnly();
            }

            // Reopen archive if we need to
            if (SevenZipHelper.ReopenSevenZipArchive(ArchivePath, Archive))
            {
                M3MemoryAnalyzer.AddTrackedMemoryItem($@"Re-opened SVE archive for {ModName}", Archive);
            }
        }

        private void buildInstallationQueue(ModJob job, CaseInsensitiveDictionary<InstallSourceFile> installationMapping, bool isSFAR)
        {
            M3Log.Information(@"Building installation queue for " + job.Header, Settings.LogModInstallation);
            foreach (var entry in job.FilesToInstall)
            {
                //Key is destination, value is source file
                var destFile = entry.Key;
                var sourceFile = entry.Value;

                bool altApplied = false;
                foreach (var altFile in job.AlternateFiles.Where(x => x.UIIsSelected))
                {
                    Debug.WriteLine(@"Checking alt conditions for application: " + altFile.FriendlyName);
                    if (altFile.Operation == AlternateFile.AltFileOperation.OP_NOTHING) continue; //skip nothing
                    if (altFile.Operation == AlternateFile.AltFileOperation.OP_APPLY_MULTILISTFILES) continue; //do not apply in the main loop.
                    if (altFile.Operation == AlternateFile.AltFileOperation.OP_NOINSTALL_MULTILISTFILES) continue; //do not apply in the main loop.
                    if (altFile.Operation == AlternateFile.AltFileOperation.OP_APPLY_MERGEMODS) continue; //do not apply in the main loop.

                    if (altFile.ModFile.Equals(destFile, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //Alt applies to this file
                        switch (altFile.Operation)
                        {
                            case AlternateFile.AltFileOperation.OP_NOINSTALL:
                                M3Log.Information($@"Not installing {destFile} for Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL", Settings.LogModInstallation);
                                //we simply don't map as we just do a continue below.
                                altApplied = true;
                                break;
                            case AlternateFile.AltFileOperation.OP_SUBSTITUTE:
                                M3Log.Information($@"Repointing {destFile} to {altFile.AltFile} for Alternate File {altFile.FriendlyName} due to operation OP_SUBSTITUTE", Settings.LogModInstallation);
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
                                M3Log.Information($@"Adding {sourceFile} to install (from {altFile.AltFile}) as part of Alternate File {altFile.FriendlyName} due to operation OP_INSTALL", Settings.LogModInstallation);
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
                M3Log.Information($@"Adding {job.Header} file to installation {(isSFAR ? @"SFAR" : @"unpacked")} queue: {entry.Value} -> {destFile}", Settings.LogModInstallation); //do not localize

            }

            //Apply autolist alternate files

            foreach (var altFile in job.AlternateFiles.Where(x => x.UIIsSelected && x.Operation == AlternateFile.AltFileOperation.OP_APPLY_MULTILISTFILES))
            {
                foreach (var multifile in altFile.MultiListSourceFiles)
                {
                    var realMultiFile = multifile;
                    M3Log.Information($@"Adding multilist file {realMultiFile} to install (from {altFile.MultiListRootPath}) as part of Alternate File {altFile.FriendlyName} due to operation OP_APPLY_MULTILISTFILES",
                        Settings.LogModInstallation);
                    string relativeSourcePath = altFile.MultiListRootPath + '\\' + realMultiFile;

                    // ModDesc 8: Allow flattening output directory
                    if (altFile.FlattenMultilistOutput)
                    {
                        realMultiFile = Path.GetFileName(realMultiFile);
                    }
                    var targetPath = altFile.MultiListTargetPath + '\\' + realMultiFile;

                    // Map the installation mapping of this source file.
                    installationMapping[targetPath] = new InstallSourceFile(relativeSourcePath)
                    {
                        AltApplied = true,
                        IsFullRelativeFilePath = true
                    };
                }
            }

            // Remove multilist noinstall files
            foreach (var altFile in job.AlternateFiles.Where(x => x.UIIsSelected && x.Operation == AlternateFile.AltFileOperation.OP_NOINSTALL_MULTILISTFILES))
            {
                foreach (var multifile in altFile.MultiListSourceFiles)
                {
                    M3Log.Information(
                        $@"Attempting to remove multilist file {multifile} from install (from {altFile.MultiListRootPath}) as part of Alternate File {altFile.FriendlyName} due to operation OP_NOINSTALL_MULTILISTFILES",
                        Settings.LogModInstallation);
                    string relativeSourcePath = altFile.MultiListRootPath + '\\' + multifile;

                    var targetPath = altFile.MultiListTargetPath + '\\' + multifile;
                    if (installationMapping.Remove(targetPath))
                    {
                        M3Log.Information($@" > Removed multilist file {targetPath} from installation",
                        Settings.LogModInstallation);
                    }
                    else
                    {
                        M3Log.Warning($@"Failed to remove multilist file from installation queue as specified by altfile: {targetPath}, path not present in installation files");
                    }
                }
            }
        }

        /// <summary>
        /// Validates this mod can install against a game target with respect to the list of RequiredDLC. 
        /// </summary>
        /// <param name="gameTarget">Target to validate against</param>
        /// <returns>List of missing DLC modules, or an empty list if none</returns>
        internal List<DLCRequirement> ValidateRequiredModulesAreInstalled(GameTargetWPF gameTarget)
        {
            if (gameTarget.Game != Game)
            {
                throw new Exception(@"Cannot validate a mod against a gametarget that is not for its game");
            }

            List<DLCRequirement> failedReqs = new List<DLCRequirement>();
            var installedDLC = gameTarget.GetInstalledDLC();

            foreach (var req in RequiredDLC)
            {
                if (!req.IsRequirementMet(gameTarget, installedDLC))
                {
                    failedReqs.Add(req);
                }
            }

            return failedReqs;
        }

        /// <summary>
        /// Validates this mod can install against a game target with respect to the list of RequiredDLC. 
        /// </summary>
        /// <param name="gameTarget">Target to validate against</param>
        /// <returns>List of missing DLC modules, or an empty list if none</returns>
        internal bool ValidateSingleOptionalRequiredDLCInstalled(GameTargetWPF gameTarget)
        {
            if (gameTarget.Game != Game)
            {
                throw new Exception(@"Cannot validate a mod against a gametarget that is not for its game");
            }

            if (OptionalSingleRequiredDLC.Any())
            {
                var installedDLC = gameTarget.GetInstalledDLC();

                foreach (var req in OptionalSingleRequiredDLC)
                {
                    if (req.IsRequirementMet(gameTarget, installedDLC))
                    {
                        return true;
                    }
                }
                return false;
            }

            return true;
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
            }
            return list;
        }

        /// <summary>
        /// Gets a list of all files that *may* be installed by a mod.
        /// </summary>
        /// <returns></returns>
        public List<string> GetAllInstallableFiles(bool includeModifable = false)
        {
            ReOpenArchiveIfNecessary();

            var list = new List<string>();

            foreach (var job in InstallationJobs)
            {
                if (ModJob.IsVanillaJob(job, Game))
                {
                    // Basegame, Official DLC
                    list.AddRange(job.FilesToInstall.Keys);

                }
                else if (job.Header == ModJob.JobHeader.CUSTOMDLC)
                {

                    foreach (var cdlcDir in job.CustomDLCFolderMapping)
                    {
                        var dlcSourceDir = FilesystemInterposer.PathCombine(IsInArchive, ModPath, cdlcDir.Key);
                        var files = FilesystemInterposer.DirectoryGetFiles(dlcSourceDir, @"*", SearchOption.AllDirectories, Archive).Select(x => x.Substring(dlcSourceDir.Length + 1));
                        list.AddRange(files.Select(x => $@"{MEDirectories.GetDLCPath(Game, @"")}\{cdlcDir.Value}\{x}")); // do not localize
                    }
                    /*
                    foreach (var cdlcDir in job.CustomDLCFolderMapping)
                    {
                        var dlcSourceDir = Path.Combine(ModPath, cdlcDir.Key);
                        var files = Directory.GetFiles(dlcSourceDir, @"*", SearchOption.AllDirectories).Select(x => x.Substring(dlcSourceDir.Length + 1));
                        list.AddRange(files.Select(x => $@"{MEDirectories.GetDLCPath(Game, @"")}\{cdlcDir.Value}\{x}")); // do not localize
                    }*/
                }
                else if (job.Header == ModJob.JobHeader.GAME1_EMBEDDED_TLK)
                {
                    if (job.Game1TLKXmls != null)
                    {
                        // We need to check the files
                        CompressedTLKMergeData data = null;
                        var map = PrepareTLKMerge(out data);

                        list.AddRange(map.Keys.Select(x => $@"{x}{(Game == MEGame.ME1 ? @".sfm" : @".pcc")}")); // do not localize
                    }
                }

                foreach (var v in job.AlternateFiles)
                {
                    if (v.Operation == AlternateFile.AltFileOperation.OP_INSTALL)
                    {
                        list.Add(v.ModFile);
                    }
                    else if (v.Operation == AlternateFile.AltFileOperation.OP_APPLY_MULTILISTFILES)
                    {
                        foreach (var mlFile in v.MultiListSourceFiles)
                        {
                            if (v.FlattenMultilistOutput)
                            {
                                list.Add(v.MultiListTargetPath + @"\" + Path.GetFileName(mlFile));
                            }
                            else
                            {
                                list.Add(v.MultiListTargetPath + @"\" + mlFile);
                            }
                        }
                    }
                    else if (v.Operation == AlternateFile.AltFileOperation.OP_APPLY_MERGEMODS)
                    {
                        foreach (var mm in v.MergeMods)
                        {
                            list.AddRange(mm.GetMergeFileTargetFiles());
                        }
                    }
                }


                foreach (var v in job.AlternateDLCs)
                {
                    if (v.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC || v.Operation == AlternateDLC.AltDLCOperation.OP_ADD_FOLDERFILES_TO_CUSTOMDLC)
                    {
                        var dlcSourceDir = FilesystemInterposer.PathCombine(IsInArchive, ModPath, v.AlternateDLCFolder);
                        var files = FilesystemInterposer.DirectoryGetFiles(dlcSourceDir, @"*", SearchOption.AllDirectories, Archive).Select(x => x.Substring(dlcSourceDir.Length + 1));
                        list.AddRange(files.Select(x => $@"{MEDirectories.GetDLCPath(Game, @"")}\{v.DestinationDLCFolder}\{x}")); //do not localize
                    }

                    if (v.Operation == AlternateDLC.AltDLCOperation.OP_ADD_MULTILISTFILES_TO_CUSTOMDLC)
                    {
                        foreach (var mlFile in v.MultiListSourceFiles)
                        {
                            if (v.FlattenMultilistOutput)
                            {
                                list.Add($@"{MEDirectories.GetDLCPath(Game, @"")}\{v.DestinationDLCFolder}\{Path.GetFileName(mlFile)}"); // do not localize
                            }
                            else
                            {
                                list.Add($@"{MEDirectories.GetDLCPath(Game, @"")}\{v.DestinationDLCFolder}\{mlFile}"); //do not localize
                            }
                        }
                    }
                }

                foreach (var mm in job.MergeMods)
                {
                    list.AddRange(mm.GetMergeFileTargetFiles());
                }
            }

            return list.Distinct().OrderBy(x => x).ToList();
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