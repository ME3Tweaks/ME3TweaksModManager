using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCoreWPF;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.mod.merge.v1
{
    public class MergeFileChange1
    {
        [JsonProperty(@"entryname")] public string EntryName { get; set; }
        [JsonProperty(@"propertyupdates")] public List<PropertyUpdate1> PropertyUpdates { get; set; }
        [JsonProperty(@"disableconfigupdate")] public bool DisableConfigUpdate { get; set; }
        [JsonProperty(@"assetupdate")] public AssetUpdate1 AssetUpdate { get; set; }
        [JsonProperty(@"scriptupdate")] public ScriptUpdate1 ScriptUpdate { get; set; }
        [JsonProperty(@"sequenceskipupdate")] public SequenceSkipUpdate1 SequenceSkipUpdate { get; set; }

        [JsonIgnore] public MergeFile1 Parent;
        [JsonIgnore] public MergeMod1 OwningMM => Parent.OwningMM;

        public void ApplyChanges(IMEPackage package, MergeAssetCache1 assetsCache, Mod installingMod, GameTargetWPF gameTarget)
        {
            // APPLY PROPERTY UPDATES
            M3Log.Information($@"Merging changes into {EntryName}");
            var export = package.FindExport(EntryName);
            if (export == null)
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_couldNotFindExportInPackage, package.FilePath, EntryName));

            if (PropertyUpdates != null)
            {
                var props = export.GetProperties();
                foreach (var pu in PropertyUpdates)
                {
                    pu.ApplyUpdate(package, props, this);
                }
                export.WriteProperties(props);
            }

            // APPLY ASSET UPDATE
            AssetUpdate?.ApplyUpdate(package, export, installingMod);

            // APPLY SCRIPT UDPATE
            ScriptUpdate?.ApplyUpdate(package, export, assetsCache, installingMod, gameTarget);

            // APPLY SEQUENCE SKIP UPDATE
            SequenceSkipUpdate?.ApplyUpdate(package, export, installingMod);

            // APPLY CONFIG FLAG REMOVAL
            if (DisableConfigUpdate)
            {
                DisableConfigFlag(package, export, installingMod);
            }
        }

        private void DisableConfigFlag(IMEPackage package, ExportEntry export, Mod installingMod)
        {
            if (ObjectBinary.From(export) is UProperty ob)
            {
                M3Log.Information($@"Disabling config flag on {export.InstancedFullPath}");
                ob.PropertyFlags &= ~UnrealFlags.EPropertyFlags.Config;
                export.WriteBinary(ob);
            }
            else
            {
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_notPropertyExportConfigFlag, export.InstancedFullPath, package.FilePath));
            }
        }

        public void SetupParent(MergeFile1 parent)
        {
            Parent = parent;
            if (AssetUpdate != null)
                AssetUpdate.Parent = this;
        }

        public void Validate()
        {
            if (PropertyUpdates != null)
            {
                foreach (var pu in PropertyUpdates)
                {
                    pu.Validate();
                }
            }

            AssetUpdate?.Validate();
            ScriptUpdate?.Validate();
            SequenceSkipUpdate?.Validate();
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

        public bool ApplyUpdate(IMEPackage package, PropertyCollection properties, MergeFileChange1 mfc)
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

            M3Log.Information($@"Applying property update: {PropertyName} -> {PropertyValue}");
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
                    if (PropertyValue != null && PropertyValue != @"M3M_NULL") //M3M_NULL is a keyword for setting it to null to satisfy the schema
                    {
                        var entry = package.FindEntry(PropertyValue);
                        if (entry == null)
                            throw new Exception(M3L.GetString(M3L.string_interp_mergefile_failedToUpdateObjectPropertyItemNotInPackage, PropertyName, PropertyValue, PropertyValue, package.FilePath));
                        op.Value = entry.UIndex;
                    }
                    operatingCollection.AddOrReplaceProp(op);
                    break;
                case @"EnumProperty":
                    var enumInfo = PropertyValue.Split('.');
                    EnumProperty ep = new EnumProperty(enumInfo[0], mfc.OwningMM.Game, PropertyName);
                    ep.Value = NameReference.FromInstancedString(enumInfo[1]);
                    operatingCollection.AddOrReplaceProp(ep);
                    break;
                case @"StrProperty":
                    var sp = new StrProperty(PropertyValue, propKeys.Last());
                    operatingCollection.AddOrReplaceProp(sp);
                    break;
                default:
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_unsupportedPropertyType, PropertyType));
            }
            return true;
        }

        public void Validate()
        {
            if (PropertyType == @"EnumProperty")
            {
                if (PropertyValue.Split('.').Length != 2)
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_invalidEnumPropertyValue));
            }
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
                sourceEntry, targetExport.FileRef, targetExport, true, new RelinkerOptionsPackage()
                {
                    ErrorOccurredCallback = x => throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorMergingAssetsX, x)),
                    ImportExportDependencies = true // I don't think this is actually necessary...
                }, out _);
            if (resultst.Any())
            {
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorsOccurredMergingAsset, AssetName, EntryName, string.Join('\n', resultst.Select(x => x.Message))));
            }

            return true;
        }

        public void Validate()
        {

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

        public bool ApplyUpdate(IMEPackage package, ExportEntry targetExport, MergeAssetCache1 assetsCache, Mod installingMod, GameTargetWPF gameTarget)
        {
            FileLib fl;
            if (!assetsCache.FileLibs.TryGetValue(package.FilePath, out fl))
            {
                fl = new FileLib(package);
                bool initialized = fl.Initialize(new RelativePackageCache() { RootPath = M3Directories.GetBioGamePath(gameTarget) }, gameTarget.TargetPath);
                if (!initialized)
                {
                    M3Log.Error($@"FileLib loading failed for package {targetExport.InstancedFullPath} ({targetExport.FileRef.FilePath}):");
                    foreach (var v in fl.InitializationLog.AllErrors)
                    {
                        M3Log.Error(v.Message);
                    }

                    throw new Exception(M3L.GetString(M3L.string_interp_fileLibInitMergeMod1Script, targetExport.InstancedFullPath, string.Join(Environment.NewLine, fl.InitializationLog.AllErrors)));
                }

                assetsCache.FileLibs[package.FilePath] = fl;
            }


            (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(targetExport, ScriptText, fl);
            if (log.AllErrors.Any())
            {
                M3Log.Error($@"Error compiling function {targetExport.InstancedFullPath}:");
                foreach (var l in log.AllErrors)
                {
                    M3Log.Error(l.Message);
                }

                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorCompilingFunction, targetExport, string.Join(Environment.NewLine, log.AllErrors)));
            }

            return true;
        }

        public void Validate()
        {

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
            if (M3Utilities.CalculateMD5(new MemoryStream(targetExport.Data)) == EntryMD5)
            {
                M3Log.Information($@"Applying sequence skip: Skipping {targetExport.InstancedFullPath} through on link {OutboundLinkNameToUse}");
                SeqTools.SkipSequenceElement(targetExport, outboundLinkName: OutboundLinkNameToUse);
            }
            else
            {
                M3Log.Warning(@"Target export MD5 is incorrect. This may be the wrong target export, or it may be already patched. We are reporting that the mod installed, in the event the target was updated.");
            }

            return true;
        }

        public void Validate()
        {

        }
    }
}
