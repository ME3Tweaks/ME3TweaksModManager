using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;

namespace ME3TweaksModManager.modmanager.merge.dlc.LE2
{
    /// <summary>
    /// Contains transient information about where an image should be injected into a package
    /// </summary>
    internal class LE2SquadmateImageInfo
    {
        /// <summary>
        /// The source export that we will be copying texture data from
        /// </summary>
        public ExportEntry SourceExport { get; set; }

        /// <summary>
        /// The destination texture name
        /// </summary>
        public string DestinationTextureName { get; set; }


        public void InjectSquadmateImageIntoPackage(IMEPackage destinationPackage)
        {
            // We are going to just clone textures and then rename them instead of generating new exports as that's kind of a PITA

            var texToClone = destinationPackage.FindExport(@"GUI_SF_TeamSelect.TeamSelect_I1");
            var newTexture = EntryCloner.CloneEntry(texToClone);
            newTexture.ObjectName = DestinationTextureName;

            // Copy the texture export over
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, SourceExport, destinationPackage, newTexture, true, new RelinkerOptionsPackage(), out _);

            var teamSelect = destinationPackage.FindExport(@"GUI_SF_TeamSelect.TeamSelect");
            var teamSelectRefs = teamSelect.GetProperty<ArrayProperty<ObjectProperty>>(@"References");
            teamSelectRefs.Add(new ObjectProperty(newTexture.UIndex));
            teamSelect.WriteProperty(teamSelectRefs);
        }
    }
}
