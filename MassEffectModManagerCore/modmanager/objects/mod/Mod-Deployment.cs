using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MassEffectModManagerCore.modmanager
{
    //This file contains deployment to archive related functionality
    public partial class Mod
    {
        public List<string> GetAllRelativeReferences()
        {
            var references = new List<string>();
            references.Add("moddesc.ini");
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
                        var files = Directory.GetFiles(Path.Combine(ModPath, dlc.AlternateDLCFolder), "*", SearchOption.AllDirectories).Select(x => x.Substring(ModPath.Length + 1)).ToList();
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
            }
            references.AddRange(AdditionalDeploymentFiles);
            references.AddRange(AdditionalDeploymentFolders);
            return references;
        }
    }
}
