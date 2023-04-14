using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME3TweaksModManager.modmanager.objects.alternates;
using ME3TweaksModManager.modmanager.usercontrols;
using SevenZip;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    //This file contains deployment to archive related functionality
    public partial class Mod
    {
        /// <summary>
        /// Transform that is applied to help convert an exe installer to a mod manager mod
        /// </summary>
        internal ModArchiveImporter.ExeTransform ExeExtractionTransform;

        /// <summary>
        /// Generates a blank mod object. You must specify you know what you are doing for this by specifying if this is a valid mod or not.
        /// </summary>
        /// <param name="iKnowWhatImDoing"></param>
        public Mod(bool valid)
        {
            ValidMod = valid;
        }

        /// <summary>
        /// Gets all files referenced by this mod. This does not include moddessc.ini by default
        /// </summary>
        /// <param name="includeModdesc">Include moddesc.ini in the results</param>
        /// <param name="archive">Archive, if this mod is in an archive.</param>
        /// <returns></returns>
        public List<string> GetAllRelativeReferences(bool includeModdesc = false, SevenZipExtractor archive = null)
        {
            var references = new List<string>();
            //references.Add("moddesc.ini"); //Moddesc is implicitly referenced by the mod.
            //Replace or Add references
            foreach (var job in InstallationJobs)
            {

                foreach (var jobFile in job.FilesToInstall.Values)
                {
                    if (job.JobDirectory == @"." || job.JobDirectory == null)
                    {
                        references.Add(jobFile);
                    }
                    else
                    {
                        references.Add(job.JobDirectory + @"\" + jobFile);
                    }
                }

                foreach (var dlc in job.AlternateDLCs)
                {
                    if (dlc.HasRelativeFiles())
                    {
                        if (dlc.AlternateDLCFolder != null)
                        {
                            var files = FilesystemInterposer
                                .DirectoryGetFiles(
                                    FilesystemInterposer.PathCombine(IsInArchive, ModPath, dlc.AlternateDLCFolder), "*",
                                    SearchOption.AllDirectories, archive).Select(x =>
                                    (IsInArchive && ModPath.Length == 0) ? x : x.Substring(ModPath.Length + 1))
                                .ToList();
                            references.AddRange(files);
                        }
                        else if (dlc.MultiListSourceFiles != null)
                        {
                            foreach (var mf in dlc.MultiListSourceFiles)
                            {
                                var relpath = Path.Combine(ModPath, dlc.MultiListRootPath, mf)
                                    .Substring(ModPath.Length > 0 ? ModPath.Length + 1 : 0);
                                references.Add(relpath);
                            }
                        }
                    }

                    // Add the referenced image asset
                    if (dlc.ImageAssetName != null)
                    {
                        references.Add(FilesystemInterposer
                            .PathCombine(IsInArchive, ModImageAssetsPath, dlc.ImageAssetName)
                            .Substring(ModPath.Length + (ModPath.Length > 1 ? 1 : 0)));
                    }
                }

                foreach (var file in job.AlternateFiles)
                {
                    if (file.HasRelativeFile())
                    {
                        if (file.AltFile != null)
                        {
                            //Commented out: AltFile should be direct path to file from mod root, we should only put in relative path
                            //if (IsInArchive)
                            //{
                            //    references.Add(FilesystemInterposer.PathCombine(true, ModPath, file.AltFile));
                            //}
                            //else
                            //{
                            references.Add(file.AltFile);
                            //}
                        }
                        else if (file.MultiListSourceFiles != null)
                        {
                            foreach (var mf in file.MultiListSourceFiles)
                            {
                                var relPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath,
                                    file.MultiListRootPath, mf);
                                //Should this be different from above AltFile?
                                if (IsInArchive)
                                {
                                    references.Add(
                                        relPath.Substring(ModPath.Length +
                                                          (ModPath.Length > 1
                                                              ? 1
                                                              : 0))); //substring so its relative to the path of the mod in the archive
                                }
                                else
                                {
                                    references.Add(
                                        relPath.Substring(ModPath.Length +
                                                          1)); //chop off the root path of the moddesc.ini
                                }
                            }
                        }
                        else if (file.MergeMods != null)
                        {
                            foreach (var mf in file.MergeMods)
                            {
                                var relPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath,
                                    Mod.MergeModFolderName, mf.MergeModFilename);
                                if (IsInArchive)
                                {
                                    references.Add(
                                        relPath.Substring(ModPath.Length +
                                                          (ModPath.Length > 1
                                                              ? 1
                                                              : 0))); //substring so its relative to the path of the mod in the archive
                                }
                                else
                                {
                                    references.Add(
                                        relPath.Substring(ModPath.Length +
                                                          1)); //chop off the root path of the moddesc.ini
                                }
                            }
                        }
                    }

                    // Add the referenced image asset
                    if (file.ImageAssetName != null)
                    {
                        references.Add(FilesystemInterposer
                            .PathCombine(IsInArchive, ModImageAssetsPath, file.ImageAssetName)
                            .Substring(ModPath.Length + (ModPath.Length > 1 ? 1 : 0)));
                    }
                }

                // Add texture mod reference files
                if (job.Header == ModJob.JobHeader.TEXTUREMODS)
                {
                    foreach (var textureModRef in job.TextureModReferences)
                    {
                        references.Add(textureModRef.GetRelativePathToMEM());
                    }
                }

                // Add headmorph files
                if (job.Header == ModJob.JobHeader.HEADMORPHS)
                {
                    foreach (var headmorphRef in job.HeadMorphFiles)
                    {
                        references.AddRange(headmorphRef.GetRelativeReferences(this));
                    }
                }

                foreach (var customDLCmapping in job.CustomDLCFolderMapping)
                {
                    references.AddRange(FilesystemInterposer.DirectoryGetFiles(FilesystemInterposer.PathCombine(IsInArchive, ModPath, customDLCmapping.Key), "*", SearchOption.AllDirectories, archive).Select(x => (IsInArchive && ModPath.Length == 0) ? x : x.Substring(ModPath.Length + 1)).ToList());
                }
                foreach (var mm in job.MergeMods)
                {
                    references.Add($@"{Mod.MergeModFolderName}\{mm.MergeModFilename}");
                }

                if (job.Game1TLKXmls != null)
                {
                    bool usedCombinedFile = false;
                    if (ModDescTargetVersion >= 8.0)
                    {
                        var m3zaf = FilesystemInterposer.PathCombine(IsInArchive, ModPath, Mod.Game1EmbeddedTlkFolderName, Mod.Game1EmbeddedTlkCompressedFilename);
                        if (FilesystemInterposer.FileExists(m3zaf, archive))
                        {
                            // This file is referenced
                            references.Add($@"{Mod.Game1EmbeddedTlkFolderName}\{Mod.Game1EmbeddedTlkCompressedFilename}");
                            usedCombinedFile = true;
                        }
                    }

                    if (!usedCombinedFile)
                    {
                        foreach (var tlkXml in job.Game1TLKXmls)
                        {
                            references.Add($@"{Mod.Game1EmbeddedTlkFolderName}\{tlkXml}");
                        }
                    }
                }
            }
            references.AddRange(AdditionalDeploymentFiles);
            foreach (var additionalDeploymentDir in AdditionalDeploymentFolders)
            {
                references.AddRange(FilesystemInterposer.DirectoryGetFiles(FilesystemInterposer.PathCombine(IsInArchive, ModPath, additionalDeploymentDir), "*", SearchOption.AllDirectories, archive).Select(x => (IsInArchive && ModPath.Length == 0) ? x : x.Substring(ModPath.Length + 1)).ToList());
            }

            // Banner Image
            if (!string.IsNullOrWhiteSpace(BannerImageName))
            {
                references.Add(FilesystemInterposer.PathCombine(IsInArchive, Mod.M3IMAGES_FOLDER_NAME, BannerImageName));
            }

            if (includeModdesc && GetJob(ModJob.JobHeader.ME2_RCWMOD) == null)
            {
                references.Add(ModDescPath.Substring(ModPath.Length).TrimStart('/', '\\'));
                //references.Add(ModDescPath.TrimStart('/', '\\'));
            }
            return references.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
        }

        /// <summary>
        /// Returns all alternate options that are attached to any job.
        /// </summary>
        /// <returns></returns>
        public List<AlternateOption> GetAllAlternates()
        {
            List<AlternateOption> alternates = new List<AlternateOption>();
            foreach (var mj in InstallationJobs)
            {
                alternates.AddRange(mj.AlternateFiles);
                alternates.AddRange(mj.AlternateDLCs);
            }
            return alternates;
        }

        /// <summary>
        /// Gets a list of all possible DLC folders that can be installed by this mod, if all the alternates were also chosen that could produce a new DLC folder
        /// </summary>
        /// <returns></returns>
        public List<string> GetAllPossibleCustomDLCFolders()
        {
            var custDlcJob = GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (custDlcJob != null)
            {
                var folders = custDlcJob.CustomDLCFolderMapping.Values.Select(x => x).ToList();
                folders.AddRange(custDlcJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC)
                    .Select(x => x.DestinationDLCFolder));
                return folders;
            }

            return new List<string>();
        }
    }
}
