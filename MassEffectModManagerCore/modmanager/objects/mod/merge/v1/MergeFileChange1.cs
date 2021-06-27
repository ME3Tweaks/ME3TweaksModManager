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
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using MassEffectModManagerCore.modmanager.localizations;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManagerCore.modmanager.objects.mod.merge.v1
{
    public class MergeFileChange1
    {
        [JsonProperty(@"entryname")] public string EntryName { get; set; }
        [JsonProperty(@"propertyupdates")] public List<PropertyUpdate1> PropertyUpdates { get; set; }
        [JsonProperty(@"assetupdate")] public AssetUpdate1 AssetUpdate { get; set; }
        [JsonProperty(@"scriptupdate")] public ScriptUpdate1 ScriptUpdate { get; set; }
        [JsonProperty(@"sequenceskipupdate")] public SequenceSkipUpdate1 SequenceSkipUpdate { get; set; }

        [JsonIgnore] public MergeFile1 Parent;
        [JsonIgnore] public MergeMod1 OwningMM => Parent.OwningMM;

        public void ApplyChanges(IMEPackage package, MergeAssetCache1 assetsCache, Mod installingMod, GameTarget gameTarget)
        {
            // APPLY PROPERTY UPDATES
            Log.Information($@"Merging changes into {package.FilePath}");
            var export = package.FindExport(EntryName);
            if (export == null)
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_couldNotFindExportInPackage, package.FilePath, EntryName));

            if (PropertyUpdates != null)
            {
                foreach (var pu in PropertyUpdates)
                {
                    Log.Information($@"Applying property changes to {export.FileRef.FilePath} {export.InstancedFullPath}");
                    var props = export.GetProperties();
                    pu.ApplyUpdate(package, props);
                    export.WriteProperties(props);
                }
            }

            // APPLY ASSET UPDATE
            AssetUpdate?.ApplyUpdate(package, export, installingMod);

            // APPLY SCRIPT UDPATE
            ScriptUpdate?.ApplyUpdate(package, export, assetsCache, installingMod, gameTarget);

            SequenceSkipUpdate?.ApplyUpdate(package, export, installingMod);
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
        [JsonProperty(@"propertyname")]
        public string PropertyName { get; set; }

        [JsonProperty(@"propertytype")]
        public string PropertyType { get; set; }

        [JsonProperty(@"propertyvalue")]
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
                case @"FloatProperty":
                    FloatProperty fp = new FloatProperty(float.Parse(PropertyValue, CultureInfo.InvariantCulture), propKeys.Last());
                    operatingCollection.AddOrReplaceProp(fp);
                    break;
                case @"IntProperty":
                    IntProperty ip = new IntProperty(int.Parse(PropertyValue), propKeys.Last());
                    operatingCollection.AddOrReplaceProp(ip);
                    break;
                case @"BoolProperty":
                    BoolProperty bp = new BoolProperty(bool.Parse(PropertyValue), propKeys.Last());
                    operatingCollection.AddOrReplaceProp(bp);
                    break;
                case @"NameProperty":
                    var index = 0;
                    var baseName = PropertyValue;
                    var indexIndex = PropertyValue.IndexOf(@"|", StringComparison.InvariantCultureIgnoreCase);
                    if (indexIndex > 0)
                    {
                        baseName = baseName.Substring(0, indexIndex);
                        index = int.Parse(baseName.Substring(indexIndex + 1));
                    }

                    NameProperty np = new NameProperty(new NameReference(baseName, index), PropertyName);
                    operatingCollection.AddOrReplaceProp(np);
                    break;
                case @"ObjectProperty":
                    // This does not support porting in, only relinking existing items
                    ObjectProperty op = new ObjectProperty(0, PropertyName);
                    if (PropertyValue != null)
                    {
                        var entry = package.FindEntry(PropertyValue);
                        if (entry == null)
                            throw new Exception(M3L.GetString(M3L.string_interp_mergefile_failedToUpdateObjectPropertyItemNotInPackage, PropertyName, PropertyValue, PropertyValue, package.FilePath));
                        op.Value = entry.UIndex;
                    }
                    operatingCollection.AddOrReplaceProp(op);
                    break;
                default:
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_unsupportedPropertyType, PropertyType));
            }
            return true;
        }
    }

    public class AssetUpdate1
    {
        /// <summary>
        /// Name of asset file
        /// </summary>
        [JsonProperty(@"assetname")]
        public string AssetName { get; set; }

        /// <summary>
        /// Entry in the asset to use as porting source
        /// </summary>
        [JsonProperty(@"entryname")]
        public string EntryName { get; set; }

        [JsonIgnore] public MergeFileChange1 Parent;
        [JsonIgnore] public MergeMod1 OwningMM => Parent.OwningMM;

        public bool ApplyUpdate(IMEPackage package, ExportEntry targetExport, Mod installingMod)
        {
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
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_cannotFindAssetEntryInAssetPackage, AssetName, EntryName));
            }

            var resultst = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular,
                sourceEntry, targetExport.FileRef, targetExport, true, out _,
                errorOccuredCallback: x => throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorMergingAssetsX, x)),
                importExportDependencies: true);
            if (resultst.Any())
            {
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorsOccurredMergingAsset, AssetName, EntryName, string.Join('\n', resultst.Select(x => x.Message))));
            }

            return true;
        }
    }

    public class ScriptUpdate1
    {
        /// <summary>
        /// Name of text file containing the script
        /// </summary>
        [JsonProperty(@"scriptfilename")]
        public string ScriptFileName { get; set; }

        [JsonProperty(@"scripttext")]
        public string ScriptText { get; set; }

        [JsonIgnore] public MergeFileChange1 Parent;
        [JsonIgnore] public MergeMod1 OwningMM => Parent.OwningMM;

        public bool ApplyUpdate(IMEPackage package, ExportEntry targetExport, MergeAssetCache1 assetsCache, Mod installingMod, GameTarget gameTarget)
        {
            FileLib fl;
            if (!assetsCache.FileLibs.TryGetValue(package.FilePath, out fl))
            {
                fl = new FileLib(package);
                bool initialized = fl.Initialize(new RelativePackageCache() { RootPath = M3Directories.GetBioGamePath(gameTarget) }, gameTarget.TargetPath).Result;
                if (!initialized)
                {
                    Log.Error($@"FileLib loading failed for package {targetExport.InstancedFullPath} ({targetExport.FileRef.FilePath}):");
                    foreach (var v in fl.InitializationLog.AllErrors)
                    {
                        Log.Error(v.Message);
                    }
                    throw new Exception($@"FileLib for script update could not initialize, cannot merge script to {targetExport.InstancedFullPath}");
                }

                assetsCache.FileLibs[package.FilePath] = fl;
            }


            (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(targetExport, ScriptText, fl);
            if (log.AllErrors.Any())
            {
                Log.Error($@"Error compiling function {targetExport.InstancedFullPath}:");
                foreach (var l in log.AllErrors)
                {
                    Log.Error(l.Message);
                }

                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorCompilingFunction, targetExport, string.Join(Environment.NewLine, log.AllErrors)));
            }

            return true;
        }
    }

    public class SequenceSkipUpdate1
    {
        /// <summary>
        /// The MD5 of the target entry. This is to ensure this doesn't apply to a modified object as this could easily break the game.
        /// This limits functionality of this feature
        /// </summary>
        [JsonProperty(@"entrymd5")]
        public string EntryMD5 { get; set; }

        /// <summary>
        /// What outbound link to set as the one to skip through to
        /// </summary>
        [JsonProperty(@"outboundlinknametouse")]
        public string OutboundLinkNameToUse { get; set; }

        [JsonIgnore] public MergeFileChange1 Parent;
        [JsonIgnore] public MergeMod1 OwningMM => Parent.OwningMM;

        public bool ApplyUpdate(IMEPackage package, ExportEntry targetExport, Mod installingMod)
        {
            if (Utilities.CalculateMD5(new MemoryStream(targetExport.Data)) == EntryMD5)
            {
                Log.Information($@"Applying sequence skip: Skipping {targetExport.InstancedFullPath} through on link {OutboundLinkNameToUse}");
                SeqTools.SkipSequenceElement(targetExport, outboundLinkName: OutboundLinkNameToUse);
            }
            else
            {
                Log.Warning(@"Target export MD5 is incorrect. This may be the wrong target export, or it may be already patched. We are reporting that the mod installed, in the event the target was updated.");
            }

            return true;
        }
    }
}
