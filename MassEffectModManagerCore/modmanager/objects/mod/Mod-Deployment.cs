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
                    if (job.JobDirectory == @".")
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
                        var files = FilesystemInterposer.DirectoryGetFiles(FilesystemInterposer.PathCombine(IsInArchive, ModPath, dlc.AlternateDLCFolder), "*", SearchOption.AllDirectories, archive).Select(x => IsInArchive ? x : x.Substring(ModPath.Length + 1)).ToList();
                        references.AddRange(files);
                    }
                }
                foreach (var file in job.AlternateFiles)
                {
                    if (file.HasRelativeFile())
                    {
                        if (IsInArchive)
                        {
                            references.Add(FilesystemInterposer.PathCombine(true, ModPath, file.AltFile));
                        }
                        else
                        {
                            references.Add(file.AltFile);
                        }
                    }
                }

                foreach (var customDLCmapping in job.CustomDLCFolderMapping)
                {
                    references.AddRange(FilesystemInterposer.DirectoryGetFiles(FilesystemInterposer.PathCombine(IsInArchive, ModPath, customDLCmapping.Key), "*", SearchOption.AllDirectories, archive).Select(x => IsInArchive ? x : x.Substring(ModPath.Length + 1)).ToList());
                }
            }
            references.AddRange(AdditionalDeploymentFiles);
            foreach (var additionalDeploymentDir in AdditionalDeploymentFolders)
            {
                references.AddRange(FilesystemInterposer.DirectoryGetFiles(FilesystemInterposer.PathCombine(IsInArchive, ModPath, additionalDeploymentDir), "*", SearchOption.AllDirectories, archive).Select(x => IsInArchive ? x : x.Substring(ModPath.Length + 1)).ToList());
            }
            if (includeModdesc)
            {
                references.Add(ModDescPath);
            }
            return references;
        }
    }
}
