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
using ME3TweaksCore.Targets;
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
        [JsonProperty(@"addtoclassorreplace")] public AddToClassOrReplace1 AddToClassOrReplace { get; set; }

        [JsonIgnore] public MergeFile1 Parent;
        [JsonIgnore] public MergeMod1 OwningMM => Parent.OwningMM;

        public void ApplyChanges(IMEPackage package, MergeAssetCache1 assetsCache, Mod installingMod, GameTarget gameTarget)
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
                    pu.ApplyUpdate(package, props, export, assetsCache, gameTarget);
                }
                export.WriteProperties(props);
            }

            // APPLY ASSET UPDATE
            AssetUpdate?.ApplyUpdate(package, export, installingMod);

            // APPLY SCRIPT UDPATE
            ScriptUpdate?.ApplyUpdate(package, export, assetsCache, gameTarget);

            // APPLY SEQUENCE SKIP UPDATE
            SequenceSkipUpdate?.ApplyUpdate(package, export, installingMod);

            // APPLY ADD TO CLASS OR REPLACE
            AddToClassOrReplace?.ApplyUpdate(package, export, assetsCache, gameTarget);

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
            AddToClassOrReplace?.Validate();
        }

        public static FileLib GetFileLibForMerge(IMEPackage package, ExportEntry targetExport, MergeAssetCache1 assetsCache, GameTarget gameTarget)
        {
            if (assetsCache.FileLibs.TryGetValue(package.FilePath, out FileLib fl))
            {
                ReInitializeFileLib(targetExport, fl);
            }
            else
            {
                fl = new FileLib(package);
                bool initialized = fl.Initialize(new RelativePackageCache { RootPath = M3Directories.GetBioGamePath(gameTarget) }, gameTarget.TargetPath);
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
            return fl;
        }

        public static void ReInitializeFileLib(ExportEntry targetExport, FileLib fl)
        {
            bool reInitialized = fl.ReInitializeFile();
            if (!reInitialized)
            {
                M3Log.Error($@"FileLib re-initialization failed for package {targetExport.InstancedFullPath} ({targetExport.FileRef.FilePath}):");
                foreach (var v in fl.InitializationLog.AllErrors)
                {
                    M3Log.Error(v.Message);
                }

                throw new Exception(M3L.GetString(M3L.string_interp_fileLibInitMergeMod1Script, targetExport.InstancedFullPath, string.Join(Environment.NewLine, fl.InitializationLog.AllErrors)));
            }
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

        [JsonProperty(@"propertyasset")]
        public string PropertyAsset { get; set; }

        public bool ApplyUpdate(IMEPackage package, PropertyCollection properties, ExportEntry targetExport, MergeAssetCache1 assetsCache, GameTarget gameTarget)
        {
            var propKeys = PropertyName.Split('.');

            PropertyCollection operatingCollection = properties;

            for (int i = 0; i < propKeys.Length - 1; i++)
            {
                (NameReference propNameRef, int arrayIdx) = ParsePropName(propKeys[i]);

                if (operatingCollection.GetProp<StructProperty>(propNameRef, arrayIdx) is StructProperty sp)
                {
                    operatingCollection = sp.Properties;
                }
                else
                {
                    throw new Exception($"Property not found: {string.Join('.', propKeys[..(i + 1)])}");
                }
            }

            M3Log.Information($@"Applying property update: {PropertyName} -> {PropertyValue}");
            (NameReference propName, int propArrayIdx) = ParsePropName(propKeys[^1]);
            switch (PropertyType)
            {
                case @"FloatProperty":
                    var fp = new FloatProperty(float.Parse(PropertyValue, CultureInfo.InvariantCulture), propName){ StaticArrayIndex = propArrayIdx };
                    operatingCollection.AddOrReplaceProp(fp);
                    break;
                case @"IntProperty":
                    var ip = new IntProperty(int.Parse(PropertyValue), propName) { StaticArrayIndex = propArrayIdx };
                    operatingCollection.AddOrReplaceProp(ip);
                    break;
                case @"BoolProperty":
                    var bp = new BoolProperty(bool.Parse(PropertyValue), propName) { StaticArrayIndex = propArrayIdx };
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
                    var np = new NameProperty(new NameReference(baseName, index), propName) { StaticArrayIndex = propArrayIdx };
                    operatingCollection.AddOrReplaceProp(np);
                    break;
                case @"ObjectProperty":
                    // This does not support porting in, only relinking existing items
                    var op = new ObjectProperty(0, propName) { StaticArrayIndex = propArrayIdx };
                    if (PropertyValue != null && PropertyValue != @"M3M_NULL") //M3M_NULL is a keyword for setting it to null to satisfy the schema
                    {
                        IEntry entry = package.FindEntry(PropertyValue);
                        if (entry == null)
                            throw new Exception(M3L.GetString(M3L.string_interp_mergefile_failedToUpdateObjectPropertyItemNotInPackage, PropertyName, PropertyValue, PropertyValue, package.FilePath));
                        op.Value = entry.UIndex;
                    }
                    operatingCollection.AddOrReplaceProp(op);
                    break;
                case @"EnumProperty":
                    string[] enumInfo = PropertyValue.Split('.');
                    var ep = new EnumProperty(NameReference.FromInstancedString(enumInfo[0]), gameTarget.Game, propName)
                    {
                        Value = NameReference.FromInstancedString(enumInfo[1]),
                        StaticArrayIndex = propArrayIdx
                    };
                    operatingCollection.AddOrReplaceProp(ep);
                    break;
                case @"StrProperty":
                    var sp = new StrProperty(PropertyValue, propName) { StaticArrayIndex = propArrayIdx };
                    operatingCollection.AddOrReplaceProp(sp);
                    break;
                case @"StringRefProperty":
                    ReadOnlySpan<char> strRefPropValue = PropertyValue;
                    if (strRefPropValue.Length > 0 && strRefPropValue[0] == '$')
                    {
                        strRefPropValue = strRefPropValue[1..];
                    }
                    var srp = new StringRefProperty(int.Parse(strRefPropValue), propName) { StaticArrayIndex = propArrayIdx };
                    operatingCollection.AddOrReplaceProp(srp);
                    break;
                case @"ArrayProperty":
                {
                    FileLib fl = MergeFileChange1.GetFileLibForMerge(package, targetExport, assetsCache, gameTarget);
                    var log = new MessageLog();
                    Property prop = UnrealScriptCompiler.CompileProperty(PropertyName, PropertyValue, targetExport, fl, log);
                    if (prop is null || log.HasErrors)
                    {
                        M3Log.Error($@"Error compiling property '{PropertyName}' in {targetExport.InstancedFullPath}:");
                        foreach (var l in log.AllErrors)
                        {
                            M3Log.Error(l.Message);
                        }
                        throw new Exception($"Error compiling property '{PropertyName}' in {targetExport.InstancedFullPath}:\n{string.Join(Environment.NewLine, log.AllErrors)}");
                    }
                    operatingCollection.AddOrReplaceProp(prop);
                    break;
                }
                default:
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_unsupportedPropertyType, PropertyType));
            }
            return true;

            static (NameReference propNameString, int arrayIdx) ParsePropName(string unparsed)
            {
                string propNameString = unparsed;
                int arrayIdx = 0;
                int openbracketIdx = propNameString.IndexOf('[');
                if (openbracketIdx != -1)
                {
                    if (propNameString[^1] is ']')
                    {
                        ReadOnlySpan<char> indexSpan = propNameString.AsSpan()[(openbracketIdx + 1)..^1];
                        arrayIdx = int.Parse(indexSpan);
                        propNameString = propNameString[..openbracketIdx];
                    }
                    else
                    {
                        throw new Exception($"Incomplete static array index in propertyname: {unparsed}");
                    }
                }
                return (NameReference.FromInstancedString(propNameString), arrayIdx);
            }
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
            string sourcePath = null;
            if (OwningMM.Assets[AssetName].AssetBinary != null)
            {
                binaryStream = new MemoryStream(OwningMM.Assets[AssetName].AssetBinary);
            }
            else
            {
                sourcePath = FilesystemInterposer.PathCombine(installingMod.IsInArchive, installingMod.ModPath, Mod.MergeModFolderName, OwningMM.MergeModFilename);
                using var fileS = File.OpenRead(sourcePath);
                fileS.Seek(OwningMM.Assets[AssetName].FileOffset, SeekOrigin.Begin);
                binaryStream = fileS.
                    ReadToMemoryStream(OwningMM.Assets[AssetName].FileSize);
            }

            using var sourcePackage = MEPackageHandler.OpenMEPackageFromStream(binaryStream, sourcePath);
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

        public bool ApplyUpdate(IMEPackage package, ExportEntry targetExport, MergeAssetCache1 assetsCache, GameTarget gameTarget)
        {
            FileLib fl = MergeFileChange1.GetFileLibForMerge(package, targetExport, assetsCache, gameTarget);

            (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(targetExport, ScriptText, fl);
            if (log.HasErrors)
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

    public class AddToClassOrReplace1
    {
        /// <summary>
        /// Name of text file containing the script
        /// </summary>
        [JsonProperty(@"scriptfilenames")]
        public string[] ScriptFileNames { get; set; }


        [JsonProperty(@"scripts")]
        public string[] Scripts { get; set; }

        [JsonIgnore] public MergeFileChange1 Parent;
        [JsonIgnore] public MergeMod1 OwningMM => Parent.OwningMM;

        public bool ApplyUpdate(IMEPackage package, ExportEntry targetExport, MergeAssetCache1 assetsCache, GameTarget gameTarget)
        {
            FileLib fl = MergeFileChange1.GetFileLibForMerge(package, targetExport, assetsCache, gameTarget);

            for (int i = 0; i < Scripts.Length; i++)
            {
                MessageLog log = UnrealScriptCompiler.AddOrReplaceInClass(targetExport, Scripts[i], fl);

                if (log.HasErrors)
                {
                    M3Log.Error($@"Error adding/replacing '{ScriptFileNames[i]}' to {targetExport.InstancedFullPath}:");
                    foreach (var l in log.AllErrors)
                    {
                        M3Log.Error(l.Message);
                    }
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorCompilingClassAfterEdit, targetExport.InstancedFullPath, ScriptFileNames[i], string.Join(Environment.NewLine, log.AllErrors)));
                }

                //we don't need the filelib again after the last iteration, but we still need to re-initialize it.
                //Doing so can catch errors that are caused if this class was changed in a way that breaks others that depend on it.
                try
                {
                    MergeFileChange1.ReInitializeFileLib(targetExport, fl);
                }
                catch
                {
                    M3Log.Error($@"Could not re-initialize FileLib after adding/replacing '{ScriptFileNames[i]}' to {targetExport.InstancedFullPath}.");
                    throw;
                }
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
