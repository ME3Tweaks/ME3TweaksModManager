using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksModManager.modmanager.localizations;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksModManager.modmanager.starterkit;

namespace ME3TweaksModManager.modmanager.objects.starterkit
{
    /// <summary>
    /// UI bound object for choosing which 2DAs to generate in starter kit
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class Bio2DAOption
    {
        /// <summary>
        /// If option is chosen for generation
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// The title text of the option
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The template table path to use for reading the columns and data type when generating
        /// </summary>
        public LEXOpenable TemplateTable { get; set; }

        public Bio2DAOption(string title, LEXOpenable templateTable)
        {
            Title = title;
            TemplateTable = templateTable;
        }

        /// <summary>
        /// Generates a blank 2DA with info from this object at the specified path
        /// </summary>
        /// <param name="fetchTarget"></param>
        /// <param name="destPackagePath"></param>
        public void GenerateBlank2DA(ExportEntry sourceTable, string destPackagePath)
        {
            var newObjectName = $@"{sourceTable.ObjectName}_part";
            var index = 1;
            var nameRef = new NameReference(newObjectName, index);

            using var p = MEPackageHandler.OpenMEPackage(destPackagePath);
            if (p.Exports.Any(x => x.ObjectName == nameRef))
                return; // Already exists

            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceTable, p, null, true, new RelinkerOptionsPackage(), out var v);
            if (v is ExportEntry newEntry)
            {
                newEntry.ExportFlags &= ~UnrealFlags.EExportFlags.ForcedExport; // It is not ForcedExport in these seek free files.

                var twoDA = new Bio2DA(newEntry);
                twoDA.ClearRows();
                twoDA.Write2DAToExport();
                newEntry.ObjectName = nameRef;
                var objRef = StarterKitAddins.CreateObjectReferencer(p, false);
                StarterKitAddins.AddToObjectReferencer(objRef);
            }
            if (p.IsModified)
                p.Save();
        }
    }
}
