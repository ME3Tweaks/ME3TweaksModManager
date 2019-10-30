using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MassEffectModManagerCore.modmanager.objects;
using SevenZip;

namespace MassEffectModManagerCore.modmanager
{
    //This file contains deployment to archive related functionality
    public partial class Mod
    {
        public List<string> GetAllRelativeReferences(SevenZipExtractor archive = null)
        {
            var references = new List<string>();
            //references.Add("moddesc.ini"); //Moddesc is implicitly referenced by the mod.
            //Replace or Add references
            foreach (var job in InstallationJobs)
            {
                foreach (var jobFile in job.FilesToInstall.Values)
                {
                    references.Add(job.JobDirectory + "\\" + jobFile);

                }
                foreach (var dlc in job.AlternateDLCs)
                {
                    if (dlc.AlternateDLCFolder != null)
                    {
                        var files = FilesystemInterposer.DirectoryGetFiles(FilesystemInterposer.PathCombine(IsInArchive, ModPath, dlc.AlternateDLCFolder), "*", SearchOption.AllDirectories, archive).Select(x => IsInArchive ? x : x.Substring(ModPath.Length + 1)).ToList();
                        references.AddRange(files);
                    }
                }
                foreach (var file in job.AlternateFiles)
                {
                    if (file.AltFile != null)
                    {
                        references.Add(file.AltFile);
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
            return references;
        }
    }
}
