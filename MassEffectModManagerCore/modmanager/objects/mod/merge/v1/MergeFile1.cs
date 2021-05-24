using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using MassEffectModManagerCore.modmanager.windows;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge.v1
{
    public class MergeFile1
    {
        [JsonProperty(@"filename")]
        public string FileName { get; set; }

        [JsonProperty("changes")]
        public List<MergeFileChange1> MergeChanges { get; set; }

        [JsonProperty("fileexistenceoptional")]
        public bool FileExistenceOptional { get; set; }
        [JsonProperty("applytoalllocalizations")] public bool ApplyToAllLocalizations { get; set; }

        [JsonIgnore] public MergeMod1 Parent;

        [JsonIgnore]
        public MergeMod1 OwningMM => Parent;

        public void SetupParent(MergeMod1 mm)
        {
            Parent = mm;
            foreach (var mc in MergeChanges)
            {
                mc.SetupParent(this);
            }
        }

        public void ApplyChanges(CaseInsensitiveDictionary<string> loadedFiles, Mod associatedMod)
        {
            List<string> targetFiles = new List<string>();
            if (ApplyToAllLocalizations)
            {
                var targetnameBase = Path.GetFileNameWithoutExtension(FileName);
                var targetExtension = Path.GetExtension(FileName);
#if DEBUG
                var localizations = StarterKitGeneratorWindow.GetLanguagesForGame(associatedMod?.Game ?? MEGame.LE2);
#else
                var localizations = StarterKitGeneratorWindow.GetLanguagesForGame(associatedMod.Game);

#endif
                // Ensure end name is not present on base
                foreach (var l in localizations)
                {
                    if (targetnameBase.EndsWith($"_{l}", StringComparison.InvariantCultureIgnoreCase))
                        targetnameBase = targetnameBase.Substring(0,targetnameBase.Length - 4);
                }

                foreach (var l in localizations)
                {
                    targetFiles.Add($"{targetnameBase}_{l}{targetExtension}");
                }
            }
            else
            {
                targetFiles.Add(FileName);
            }

            foreach (var f in targetFiles)
            {
                if (loadedFiles.TryGetValue(f, out var fullpath))
                {
                    var package = MEPackageHandler.OpenMEPackage(fullpath);
                    foreach (var pc in MergeChanges)
                    {
                        pc.ApplyChanges(package, associatedMod);
                    }
                    Log.Information($@"Saving package {package.FilePath}");
                    package.Save(compress: true);
                }
                else
                {
                    Log.Warning($@"File not found in game: {f}, skipping...");
                }
            }
        }
    }
}
