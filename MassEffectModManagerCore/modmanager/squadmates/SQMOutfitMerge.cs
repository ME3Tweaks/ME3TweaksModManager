using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.windows;
using WinCopies.Util;

namespace MassEffectModManagerCore.modmanager.squadmates
{
    class SQMOutfitMerge
    {

        private StructProperty GeneratePlotStreamingElement(string packageName, int conditionalNum)
        {
            PropertyCollection pc = new PropertyCollection();
            pc.AddOrReplaceProp(new NameProperty(packageName, @"ChunkName"));
            pc.AddOrReplaceProp(new IntProperty(conditionalNum, @"Conditional"));
            pc.AddOrReplaceProp(new BoolProperty(false, @"bFallback"));
            pc.AddOrReplaceProp(new NoneProperty());

            return new StructProperty(@"PlotStreamingElement", pc);
        }

        public void BuildBioPGlobal(GameTarget target)
        {
            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(target.Game, gameRootOverride: target.TargetPath);
            var mergeFiles = loadedFiles.Where(x =>
                x.Key.StartsWith(@"BioH_") && x.Key.Contains(@"_DLC_MOD_") && x.Key.EndsWith(@".pcc"));

            if (mergeFiles.Any())
            {
                var biopGlobal = MEPackageHandler.OpenMEPackageFromStream(
                    Utilities.GetResourceStream($@"MassEffectModManagerCore.modmanager.squadmates.{target.Game}.BioP_Global.pcc"));
                var lsk = biopGlobal.Exports.FirstOrDefault(x => x.ClassName == @"LevelStreamingKismet");
                var persistentLevel = biopGlobal.FindExport(@"TheWorld.PersistentLevel");

                // Clone LevelStreamingKismets
                foreach (var m in mergeFiles)
                {
                    var fName = Path.GetFileNameWithoutExtension(m.Key);
                    var newLSK = EntryCloner.CloneEntry(lsk);
                    newLSK.WriteProperty(new NameProperty(fName, @"PackageName"));

                    if (target.Game.IsGame3())
                    {
                        // Game 3 has _Explore files too
                        fName += @"_Explore";
                        newLSK = EntryCloner.CloneEntry(lsk);
                        newLSK.WriteProperty(new NameProperty(fName, @"PackageName"));
                    }
                }

                // Update BioWorldInfo
                // Doesn't have consistent number so we can't find it by instanced full path
                var bioWorldInfo = biopGlobal.Exports.FirstOrDefault(x => x.ClassName == @"BioWorldInfo");

                var props = bioWorldInfo.GetProperties();

                // Update Plot Streaming
                var plotStreaming = props.GetProp<ArrayProperty<StructProperty>>(@"PlotStreaming");
                foreach (var m in mergeFiles)
                {
                    var fName = Path.GetFileNameWithoutExtension(m.Key);

                    var henchName = fName.Substring(5);
                    henchName = henchName.Substring(0, henchName.IndexOf(@"_"));

                    // find item to add to

                    var element = plotStreaming.FirstOrDefault(x =>
                        x.GetProp<NameProperty>(@"VirtualChunkName").Value == $@"BioH_{henchName}");
                    if (element != null)
                    {
                        element.GetProp<ArrayProperty<StructProperty>>(@"Elements").Add(GeneratePlotStreamingElement(fName, 0)); // TODO: HOW TO DO CONDITIONAL NUM? HOW TO KNOW THIS DURING MERGE?
                    }
                }


                // Update StreamingLevels
                var streamingLevels = props.GetProp<ArrayProperty<ObjectProperty>>(@"StreamingLevels");
                streamingLevels.ReplaceAll(biopGlobal.Exports.Where(x => x.ClassName == @"LevelStreamingKismet").Select(x => new ObjectProperty(x)));

                bioWorldInfo.WriteProperties(props);


                // Generate M3 DLC Folder

                // Save BioP_Global into merge folder

            }
        }
    }
}
