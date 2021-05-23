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
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge.v1
{
    public class MergeFileChange1
    {
        [JsonProperty]
        public string EntryName { get; set; }

        [JsonProperty("propertyupdates")] public List<PropertyUpdate1> PropertyUpdates { get; set; }
        [JsonProperty("assetupdate")] public AssetUpdate1 AssetUpdate { get; set; }


        public void ApplyChanges(IMEPackage package, MergeMod1 mergeMod)
        {
            if (PropertyUpdates != null)
            {
                foreach (var pu in PropertyUpdates)
                {
                    var export = package.FindExport(EntryName);
                    if (export == null)
                        throw new Exception($"Could not find export in package {package.FilePath}: {EntryName}! Cannot merge");

                    var props = export.GetProperties();
                    pu.ApplyUpdate(package, props);
                    export.WriteProperties(props);
                }
            }

            AssetUpdate?.ApplyUpdate(package, EntryName, mergeMod);
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
                    var fp = operatingCollection.GetProp<FloatProperty>(propKeys.Last());
                    fp.Value = float.Parse(PropertyValue, CultureInfo.InvariantCulture);
                    break;
                case "IntProperty":
                    var ip = operatingCollection.GetProp<FloatProperty>(propKeys.Last());
                    ip.Value = int.Parse(PropertyValue, CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new Exception($"Unsupported property type for updating: {PropertyType}");
            }
            return true;
        }
    }

    public class AssetUpdate1
    {
        [JsonProperty("assetindex")]
        public int AssetIndex { get; set; }

        [JsonProperty("entryname")]
        public string EntryName { get; set; }

        public bool ApplyUpdate(IMEPackage package, string destEntryName, MergeMod1 mergeMod1)
        {
            var destEntry = package.FindExport(destEntryName);
            if (destEntry == null)
            {
                throw new Exception($"Cannot find AssetUpdate1 entry in target package {package.FilePath}: {EntryName}. Merge aborted");
            }

            using var sourcePackage = MEPackageHandler.OpenMEPackageFromStream(new MemoryStream(mergeMod1.Assets[AssetIndex].AssetBinary));
            var sourceEntry = sourcePackage.FindExport(EntryName);
            if (sourceEntry == null)
            {
                throw new Exception($"Cannot find AssetUpdate1 entry in source asset package {mergeMod1.Assets[AssetIndex].FileName}: {EntryName}. Merge aborted");
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
