using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Helpers;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge.v1
{
    public class MergeFileChange1
    {
        [JsonProperty("entryname")] public string EntryName { get; set; }
        [JsonProperty("propertyupdates")] public List<PropertyUpdate1> PropertyUpdates { get; set; }
        [JsonProperty("assetupdate")] public AssetUpdate1 AssetUpdate { get; set; }

        [JsonIgnore] public MergeFile1 Parent;
        [JsonIgnore] public MergeMod1 OwningMM => Parent.OwningMM;

        public void ApplyChanges(IMEPackage package, Mod installingMod)
        {
            // APPLY PROPERTY UPDATES
            Log.Information($@"Merging changes into {package.FilePath}");
            if (PropertyUpdates != null)
            {
                foreach (var pu in PropertyUpdates)
                {
                    var export = package.FindExport(EntryName);
                    if (export == null)
                        throw new Exception($"Could not find export in package {package.FilePath}: {EntryName}! Cannot merge");

                    Log.Information($@"Applying property changes to {export.FileRef.FilePath} {export.InstancedFullPath}");
                    var props = export.GetProperties();
                    pu.ApplyUpdate(package, props);
                    export.WriteProperties(props);
                }
            }

            // APPLY ASSET UPDATE
            AssetUpdate?.ApplyUpdate(package, EntryName, installingMod);
        }

        public void SetupParent(MergeFile1 parent)
        {
            Parent = parent;
            if (AssetUpdate != null)
                AssetUpdate.Parent = this;
        }
    }

    public class PropertyUpdate1
    {
        [JsonProperty("propertyname")]
        public string PropertyName { get; set; }

        [JsonProperty("propertytype")]
        public string PropertyType { get; set; }

        [JsonProperty("propertyvalue")]
        public string PropertyValue { get; set; }

        public bool ApplyUpdate(IMEPackage package, PropertyCollection properties)
        {
            var propKeys = PropertyName.Split('.');

            PropertyCollection operatingCollection = properties;

            int i = 0;
            while (i < propKeys.Length - 1)
            {
                var matchingProp = operatingCollection.FirstOrDefault(x => x.Name.Instanced == propKeys[i]);
                if (matchingProp is StructProperty sp)
                {
                    operatingCollection = sp.Properties;
                }

                // ARRAY PROPERTIES NOT SUPPORTED
                i++;
            }

            Log.Information($@"Applying property update: {PropertyName} -> {PropertyValue}");
            switch (PropertyType)
            {
                case "FloatProperty":
                    FloatProperty fp = new FloatProperty(float.Parse(PropertyValue, CultureInfo.InvariantCulture), propKeys.Last());
                    operatingCollection.AddOrReplaceProp(fp);
                    break;
                case "IntProperty":
                    IntProperty ip = new IntProperty(int.Parse(PropertyValue), propKeys.Last());
                    operatingCollection.AddOrReplaceProp(ip);
                    break;
                case "BoolProperty":
                    BoolProperty bp = new BoolProperty(bool.Parse(PropertyValue), propKeys.Last());
                    operatingCollection.AddOrReplaceProp(bp);
                    break;
                default:
                    throw new Exception($"Unsupported property type for updating: {PropertyType}");
            }
            return true;
        }
    }

    public class AssetUpdate1
    {
        /// <summary>
        /// Name of asset file
        /// </summary>
        [JsonProperty("assetname")]
        public string AssetName { get; set; }

        /// <summary>
        /// Entry in the asset to use as porting source
        /// </summary>
        [JsonProperty("entryname")]
        public string EntryName { get; set; }

        [JsonIgnore] public MergeFileChange1 Parent;
        [JsonIgnore] public MergeMod1 OwningMM => Parent.OwningMM;

        public bool ApplyUpdate(IMEPackage package, string destEntryName, Mod installingMod)
        {
            var destEntry = package.FindExport(destEntryName);
            if (destEntry == null)
            {
                throw new Exception($"Cannot find AssetUpdate1 entry in target package {package.FilePath}: {EntryName}. Merge aborted");
            }

            Stream binaryStream;
            if (OwningMM.Assets[AssetName].AssetBinary != null)
            {
                binaryStream = new MemoryStream(OwningMM.Assets[AssetName].AssetBinary);
            }
            else
            {
                var sourcePath = FilesystemInterposer.PathCombine(installingMod.IsInArchive, installingMod.ModPath, Mod.MergeModFolderName, OwningMM.MergeModFilename);
                using var fileS = File.OpenRead(sourcePath);
                fileS.Seek(OwningMM.Assets[AssetName].FileOffset, SeekOrigin.Begin);
                binaryStream = fileS.
                    ReadToMemoryStream(OwningMM.Assets[AssetName].FileSize);
            }

            using var sourcePackage = MEPackageHandler.OpenMEPackageFromStream(binaryStream);
            var sourceEntry = sourcePackage.FindExport(EntryName);
            if (sourceEntry == null)
            {
                throw new Exception($"Cannot find AssetUpdate1 entry in source asset package {OwningMM.Assets[AssetName].FileName}: {EntryName}. Merge aborted");
            }

            var resultst = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular, sourceEntry, destEntry.FileRef, destEntry, true, out _, errorOccuredCallback: x => throw new Exception($"Error merging assets: {x}"));
            if (resultst.Any())
            {
                throw new Exception("Errors occurred merging!");
            }

            return true;
        }
    }


}
