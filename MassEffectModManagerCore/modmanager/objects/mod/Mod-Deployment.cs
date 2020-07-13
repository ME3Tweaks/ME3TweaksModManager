using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.usercontrols;
using SevenZip;

namespace MassEffectModManagerCore.modmanager
{
    //This file contains deployment to archive related functionality
    public partial class Mod
    {
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
                            var files = FilesystemInterposer.DirectoryGetFiles(FilesystemInterposer.PathCombine(IsInArchive, ModPath, dlc.AlternateDLCFolder), "*", SearchOption.AllDirectories, archive).Select(x => (IsInArchive && ModPath.Length == 0) ? x : x.Substring(ModPath.Length + 1)).ToList();
                            references.AddRange(files);
                        }
                        else if (dlc.MultiListSourceFiles != null)
                        {
                            foreach (var mf in dlc.MultiListSourceFiles)
                            {
                                var relpath = Path.Combine(ModPath, dlc.MultiListRootPath, mf).Substring(ModPath.Length > 0 ? ModPath.Length + 1 : 0);
                                references.Add(relpath);
                            }
                        }
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
                                var relPath = FilesystemInterposer.PathCombine(IsInArchive, ModPath, file.MultiListRootPath, mf);
                                //Should this be different from above AltFile?
                                if (IsInArchive)
                                {
                                    references.Add(relPath.Substring(ModPath.Length + (ModPath.Length > 1 ? 1 : 0))); //substring so its relative to the path of the mod in the archive
                                }
                                else
                                {
                                    references.Add(relPath.Substring(ModPath.Length + 1)); //chop off the root path of the moddesc.ini
                                }
                            }
                        }
                    }
                }

                foreach (var customDLCmapping in job.CustomDLCFolderMapping)
                {
                    references.AddRange(FilesystemInterposer.DirectoryGetFiles(FilesystemInterposer.PathCombine(IsInArchive, ModPath, customDLCmapping.Key), "*", SearchOption.AllDirectories, archive).Select(x => (IsInArchive && ModPath.Length == 0) ? x : x.Substring(ModPath.Length + 1)).ToList());
                }
            }
            references.AddRange(AdditionalDeploymentFiles);
            foreach (var additionalDeploymentDir in AdditionalDeploymentFolders)
            {
                references.AddRange(FilesystemInterposer.DirectoryGetFiles(FilesystemInterposer.PathCombine(IsInArchive, ModPath, additionalDeploymentDir), "*", SearchOption.AllDirectories, archive).Select(x => (IsInArchive && ModPath.Length == 0) ? x : x.Substring(ModPath.Length + 1)).ToList());
            }
            if (includeModdesc && GetJob(ModJob.JobHeader.ME2_RCWMOD) == null)
            {
                references.Add(ModDescPath.Substring(ModPath.Length).TrimStart('/', '\\'));
                //references.Add(ModDescPath.TrimStart('/', '\\'));
            }
            return references.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
        }
    }
}
