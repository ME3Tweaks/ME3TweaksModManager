using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.ME3Tweaks.ModManager;
using ME3TweaksCore.Misc;
using ME3TweaksModManager.modmanager.localizations;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Globalization;

namespace ME3TweaksModManager.modmanager.objects.mod.merge.v1
{
    /// <summary>
    /// Describes a change to a single export (or at least specified by a single target if it depends on that export)
    /// </summary>
    public class MergeFileChange1 : IMergeModCommentable
    {
        [JsonProperty(@"entryname")] public string ExportInstancedFullPath { get; set; }
        [JsonProperty(@"propertyupdates")] public List<PropertyUpdate1> PropertyUpdates { get; set; }
        [JsonProperty(@"disableconfigupdate")] public bool DisableConfigUpdate { get; set; }
        [JsonProperty(@"assetupdate")] public AssetUpdate1 AssetUpdate { get; set; }
        [JsonProperty(@"classupdate")] public ClassUpdate1 ClassUpdate { get; set; }
        [JsonProperty(@"scriptupdate")] public ScriptUpdate1 ScriptUpdate { get; set; }
        [JsonProperty(@"sequenceskipupdate")] public SequenceSkipUpdate1 SequenceSkipUpdate { get; set; }
        [JsonProperty(@"addtoclassorreplace")] public AddToClassOrReplace1 AddToClassOrReplace { get; set; }

        [JsonIgnore] public MergeFile1 Parent;
        [JsonIgnore] public MergeMod1 OwningMM => Parent.OwningMM;

        /// <summary>
        /// The comment on this field. Optional.
        /// </summary>
        [JsonProperty(@"comment")]
        public string Comment { get; set; }

        public void ApplyChanges(IMEPackage package, MergeAssetCache1 assetsCache, MergeModPackage mmp, Action<int> addMergeWeightCompletion)
        {
            // APPLY PROPERTY UPDATES
            M3Log.Information($@"Merging changes into {ExportInstancedFullPath}");
            var export = package.FindExport(ExportInstancedFullPath);

            // Mod MUST target 8.1 or higher to be able to use this functionality at all
            if (mmp.AssociatedMod.ModDescTargetVersion < ModDescConsts.MODDESC_VERSION_8_1 && export == null)
            {
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_couldNotFindExportInPackage, package.FilePath, ExportInstancedFullPath));
            }

            // APPLY ASSET UPDATE
            AssetUpdate?.ApplyUpdate(package, ref export, assetsCache, mmp, addMergeWeightCompletion);

            ClassUpdate?.ApplyUpdate(package, ref export, assetsCache, mmp.Target, addMergeWeightCompletion);
            // The below all require a target export so we enforce it here.
            if (export == null)
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_couldNotFindExportInPackage, package.FilePath, ExportInstancedFullPath));

            if (PropertyUpdates != null)
            {
                if (export == null)
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_couldNotFindExportInPackage, package.FilePath, ExportInstancedFullPath));

                var props = export.GetProperties();
                foreach (var pu in PropertyUpdates)
                {
                    pu.ApplyUpdate(package, props, export, assetsCache, mmp.Target, addMergeWeightCompletion);
                }
                export.WriteProperties(props);
            }

            // APPLY SCRIPT UDPATE
            ScriptUpdate?.ApplyUpdate(package, export, assetsCache, mmp.AssociatedMod, mmp.Target, addMergeWeightCompletion);

            // APPLY SEQUENCE SKIP UPDATE
            SequenceSkipUpdate?.ApplyUpdate(package, export, mmp.AssociatedMod, addMergeWeightCompletion);

            // APPLY ADD TO CLASS OR REPLACE
            AddToClassOrReplace?.ApplyUpdate(package, export, assetsCache, mmp.Target, addMergeWeightCompletion);

            // APPLY CONFIG FLAG REMOVAL
            if (DisableConfigUpdate)
            {
                DisableConfigFlag(package, export, mmp.AssociatedMod, addMergeWeightCompletion);
            }
        }

        [Obsolete(@"Use LE1 config merge feature instead")]
        private void DisableConfigFlag(IMEPackage package, ExportEntry export, Mod installingMod, Action<int> addMergeWeightCompleted)
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

            addMergeWeightCompleted?.Invoke(MergeFileChange1.WEIGHT_DISABLECONFIGUPDATE);
        }

        /// <summary>
        /// Links each update type to the proper parent for accessing variables in the merge mod
        /// </summary>
        /// <param name="parent"></param>
        public void SetupParent(MergeFile1 parent)
        {
            Parent = parent;

            if (PropertyUpdates != null)
            {
                foreach (var pu in PropertyUpdates)
                {
                    pu.Parent = this;
                }
            }
            if (SequenceSkipUpdate != null)
                SequenceSkipUpdate.Parent = this;
            if (AssetUpdate != null)
                AssetUpdate.Parent = this;
            if (ScriptUpdate != null)
                ScriptUpdate.Parent = this;
            if (ClassUpdate != null)
                ClassUpdate.Parent = this;
            if (AddToClassOrReplace != null)
                AddToClassOrReplace.Parent = this;
        }

        /// <summary>
        /// Performs basic validation of the merge types
        /// </summary>
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
            ClassUpdate?.Validate();
        }

        public static FileLib GetFileLibForMerge(IMEPackage package, string targetEntry, MergeAssetCache1 assetsCache, GameTarget gameTarget, UnrealScriptOptionsPackage usop)
        {
            if (assetsCache.FileLibs.TryGetValue(package.FilePath, out FileLib fl))
            {
                ReInitializeFileLib(package, fl, targetEntry, usop);
            }
            else
            {
                usop ??= new UnrealScriptOptionsPackage()
                {
                    Cache = new TargetPackageCache { RootPath = gameTarget.GetBioGamePath() },
                    GamePathOverride = gameTarget.TargetPath
                };

                fl = new FileLib(package);
                bool initialized = fl.Initialize(usop);
                if (!initialized)
                {
                    M3Log.Error($@"FileLib loading failed for package {package.FilePath}:");
                    foreach (var v in fl.InitializationLog.AllErrors)
                    {
                        M3Log.Error(v.Message);
                    }

                    throw new Exception(M3L.GetString(M3L.string_interp_fileLibInitMergeMod1Script, targetEntry, string.Join(Environment.NewLine, fl.InitializationLog.AllErrors)));
                }

                assetsCache.FileLibs[package.FilePath] = fl;
            }
            return fl;
        }

        public static void ReInitializeFileLib(IMEPackage package, FileLib fl, string targetExport, UnrealScriptOptionsPackage usop)
        {
            bool reInitialized = fl.ReInitializeFile(usop);
            if (!reInitialized)
            {
                M3Log.Error($@"FileLib re-initialization failed for package {package.FilePath}:");
                foreach (var v in fl.InitializationLog.AllErrors)
                {
                    M3Log.Error(v.Message);
                }

                throw new Exception(M3L.GetString(M3L.string_interp_fileLibInitMergeMod1Script, targetExport, string.Join(Environment.NewLine, fl.InitializationLog.AllErrors)));
            }
        }

        /// <summary>
        /// Get the weight of this merge change
        /// </summary>
        /// <returns></returns>
        public int GetMergeWeight()
        {
            if (AssetUpdate != null)
                return WEIGHT_ASSETUPDATE;
            if (ScriptUpdate != null)
                return WEIGHT_SCRIPTUPDATE;
            if (AddToClassOrReplace != null)
                return WEIGHT_SCRIPTUPDATE * AddToClassOrReplace.ScriptFileNames.Length;
            if (SequenceSkipUpdate != null)
                return WEIGHT_SEQSKIPUPDATE;
            if (PropertyUpdates != null)
                return WEIGHT_PROPERTYUPDATE * PropertyUpdates.Count;
            if (DisableConfigUpdate)
                return WEIGHT_DISABLECONFIGUPDATE;
            if (ClassUpdate != null)
                return WEIGHT_CLASSUPDATE;
            Debug.WriteLine(@"Merge weight not calculated: All merge variables were null, does this calculation need updated?");
            return 0;
        }

        internal const int WEIGHT_PROPERTYUPDATE = 2;

        internal const int WEIGHT_DISABLECONFIGUPDATE = 1;

        internal const int WEIGHT_SEQSKIPUPDATE = 2;

        internal const int WEIGHT_SCRIPTUPDATE = 15;

        internal const int WEIGHT_ASSETUPDATE = 7;

        internal const int WEIGHT_CLASSUPDATE = 25;
    }

    public class PropertyUpdate1 : MergeModUpdateBase
    {
        [JsonProperty(@"propertyname")]
        public string PropertyName { get; set; }

        [JsonProperty(@"propertytype")]
        public string PropertyType { get; set; }

        [JsonProperty(@"propertyvalue")]
        public string PropertyValue { get; set; }

        [JsonProperty(@"propertyasset")]
        public string PropertyAsset { get; set; }
        //         public bool ApplyUpdate(IMEPackage package, PropertyCollection properties, ExportEntry targetExport, MergeAssetCache1 assetsCache, GameTarget gameTarget)
        public bool ApplyUpdate(IMEPackage package, PropertyCollection properties, ExportEntry targetExport, MergeAssetCache1 assetCache, GameTarget gameTarget, Action<int> addMergeWeightCompleted)
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
                    // Print out the missing property not found by taking the first i+1 items in the key array.
                    throw new Exception(M3L.GetString(M3L.string_interp_propertyNotFoundX, string.Join('.', propKeys.Take(i + 1))));
                }
            }

            M3Log.Information($@"Applying property update: {PropertyName} -> {PropertyValue}");
            (NameReference propName, int propArrayIdx) = ParsePropName(propKeys[^1]);
            switch (PropertyType)
            {
                case @"FloatProperty":
                    var fp = new FloatProperty(float.Parse(PropertyValue, CultureInfo.InvariantCulture), propName) { StaticArrayIndex = propArrayIdx };
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
                        UnrealScriptOptionsPackage usop = new UnrealScriptOptionsPackage() { GamePathOverride = gameTarget.TargetPath };
                        FileLib fl = MergeFileChange1.GetFileLibForMerge(package, targetExport.InstancedFullPath, assetCache, gameTarget, usop);
                        var log = new MessageLog();
                        Property prop = UnrealScriptCompiler.CompileProperty(PropertyName, PropertyValue, targetExport, fl, log, usop);
                        if (prop is null || log.HasErrors)
                        {
                            M3Log.Error($@"Error compiling property '{PropertyName}' in {targetExport.InstancedFullPath}:");
                            foreach (var l in log.AllErrors)
                            {
                                M3Log.Error(l.Message);
                            }
                            throw new Exception(M3L.GetString(M3L.string_interp_errorCompilingPropertyXinYZ, PropertyName, targetExport.InstancedFullPath, string.Join(Environment.NewLine, log.AllErrors)));
                        }
                        operatingCollection.AddOrReplaceProp(prop);
                        break;
                    }
                default:
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_unsupportedPropertyType, PropertyType));
            }
            addMergeWeightCompleted?.Invoke(MergeFileChange1.WEIGHT_PROPERTYUPDATE);
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
                        throw new Exception(M3L.GetString(M3L.string_interp_incompleteStaticArrayIndex, unparsed));
                    }
                }
                return (NameReference.FromInstancedString(propNameString), arrayIdx);
            }
        }

        public override void Validate()
        {
            if (PropertyType == @"EnumProperty")
            {
                if (PropertyValue.Split('.').Length != 2)
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_invalidEnumPropertyValue));
            }
        }
    }

    /// <summary>
    /// Mod Manager 9: Allow adding new entire classes to files, using the class compiler. Does NOT allow compiling vanilla classes!
    /// </summary>
    public class ClassUpdate1 : MergeModUpdateBase
    {
        /// <summary>
        /// Name of the .uc file
        /// </summary>
        [JsonProperty(@"assetname")]
        public string AssetName { get; set; }

        public bool ApplyUpdate(IMEPackage package, ref ExportEntry outClass, MergeAssetCache1 assetsCache, GameTarget gameTarget, Action<int> addMergeWeightCompleted)
        {
            var classText = OwningMM.Assets[AssetName].AsString();
            var containingPackage = GetContainingPackage();
            IEntry container = null;
            if (containingPackage != null)
            {
                container = package.FindEntry(containingPackage, @"Package");
                if (container == null)
                {
                    // Create it
                    container = ExportCreator.CreatePackageExport(package, containingPackage, null);
                    (container as ExportEntry).ExportFlags |= UnrealFlags.EExportFlags.ForcedExport; // If a class is nested under a package the package must be forced export, at least in the official compiler
                }
            }

            UnrealScriptOptionsPackage usop = new UnrealScriptOptionsPackage() { GamePathOverride = gameTarget.TargetPath };
            FileLib fl = MergeFileChange1.GetFileLibForMerge(package, Parent.ExportInstancedFullPath, assetsCache, gameTarget, usop);

            (_, MessageLog log) = UnrealScriptCompiler.CompileClass(package, classText, fl, usop, export: package.FindExport(Parent.ExportInstancedFullPath, @"Class"), parent: container, intendedClassName: GetClassName());
            if (log.HasErrors)
            {
                M3Log.Error($@"Error compiling class {Parent.ExportInstancedFullPath}:");
                foreach (var l in log.AllErrors)
                {
                    M3Log.Error(l.Message);
                }

                // TODO: Update localization on this
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorCompilingFunction, Parent.ExportInstancedFullPath, string.Join(Environment.NewLine, log.AllErrors)));
            }

            outClass = package.FindExport(Parent.ExportInstancedFullPath, @"Class");
            addMergeWeightCompleted?.Invoke(MergeFileChange1.WEIGHT_CLASSUPDATE);
            return true;
        }

        public string GetClassName()
        {
            if (Parent.ExportInstancedFullPath.Contains('.'))
            {
                return Path.GetExtension(Parent.ExportInstancedFullPath); // Everything after the final .
            }

            // It's just the name.
            return Parent.ExportInstancedFullPath;
        }

        /// <summary>
        /// Returns the containing package name. If the specified name contains no package name, this returns null, as it will be linked to the root.
        /// </summary>
        /// <returns></returns>
        public string GetContainingPackage()
        {
            if (Parent.ExportInstancedFullPath.Contains('.'))
            {
                return Parent.ExportInstancedFullPath.Substring(0, Parent.ExportInstancedFullPath.IndexOf('.'));
            }

            return null;
        }


        /// <summary>
        /// Validates this merge object
        /// </summary>
        /// <returns></returns>
        public override void Validate()
        {
            // Cannot nest classes more than one package deep.
            if (Parent.ExportInstancedFullPath.Count(x => x == '.') > 1)
            {
                throw new Exception(M3L.GetString(M3L.string_interp_classUpdateCannotNestMoreThanOne, nameof(ClassUpdate1), Parent.ExportInstancedFullPath));
            }

            // Ensure not a vanilla class
            var className = GetClassName();

            if (VanillaClasses.IsVanillaClass(className, OwningMM.Game))
            {
                throw new Exception(M3L.GetString(M3L.string_interp_classUpdateCannotUpdateVanillaClass, nameof(ClassUpdate1), Parent.ExportInstancedFullPath));
            }
        }
    }

    /// <summary>
    /// Allows bringing in content from packages. Same as LEX Replace with References or Clone with References (if canmergeasnew is used) and destination does not exist.
    /// </summary>
    public class AssetUpdate1 : MergeModUpdateBase
    {
        /// <summary>
        /// Name of asset file
        /// </summary>
        [JsonProperty(@"assetname")]
        public string AssetName { get; set; }

        /// <summary>
        /// If the entry name can be null, indicating we must merge this asset into the file as new (Mod Manager 8.1)
        /// </summary>
        [JsonProperty(@"canmergeasnew")]
        public bool CanMergeAsNew { get; set; } = false;

        /// <summary>
        /// Entry in the asset to use as porting source
        /// </summary>
        [JsonProperty(@"entryname")]
        public string AssetExportInstancedFullPath { get; set; }

        public bool ApplyUpdate(IMEPackage package, ref ExportEntry targetExport, MergeAssetCache1 assetCache, MergeModPackage mmp, Action<int> addMergeWeightCompleted)
        {
            // Unsure if asset loading should be locked to prevent double load in race condition
            // Does it matter if same asset replaces another same asset?

            if (!assetCache.Packages.TryGetValue(AssetName, out var sourcePackage))
            {
                OwningMM.Assets[AssetName].EnsureAssetLoaded();
                var binaryStream = MEPackageHandler.CreateOptimizedLoadingMemoryStream(OwningMM.Assets[AssetName].AssetBinary);
                sourcePackage = MEPackageHandler.OpenMEPackageFromStream(binaryStream, AssetName);
                assetCache.Packages[AssetName] = sourcePackage;
            }

            // targetExport CAN BE NULL starting with ModDesc 8.1 mods!

            var sourceEntry = sourcePackage.FindExport(AssetExportInstancedFullPath);
            if (sourceEntry == null)
            {
                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_cannotFindAssetEntryInAssetPackage,
                    AssetName, AssetExportInstancedFullPath));
            }

            if (targetExport == null && CanMergeAsNew)
            {
                // We must port in the parents

                // Build the stack of all things that might need ported
                Stack<IEntry> parentStack = new Stack<IEntry>();
                IEntry entry = sourceEntry;
                while (entry.Parent != null)
                {
                    parentStack.Push(entry.Parent);
                    entry = entry.Parent;
                }

                // Create parents first
                IEntry parent = null;
                foreach (var pEntry in parentStack)
                {
                    var existingEntry = package.FindEntry(pEntry.InstancedFullPath);
                    if (existingEntry == null)
                    {
                        // Port it in
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, pEntry, package, parent, true, new RelinkerOptionsPackage()
                        {
                            ImportChildrenOfPackages = false, // As roots may be Package, do not port the children, we will do it ourselves
                            ErrorOccurredCallback = x => throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorMergingAssetsX, x)),
                            GamePathOverride = mmp.Target.TargetPath,
                        }, out parent);
                    }
                    else
                    {
                        parent = existingEntry;
                        break;
                    }
                }

                // Port in the actual content
                var resultst = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies,
                    sourceEntry, package, parent, true, new RelinkerOptionsPackage()
                    {
                        ErrorOccurredCallback = x =>
                            throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorMergingAssetsX, x)),
                        ImportExportDependencies = true, // I don't think this is actually necessary...
                        GamePathOverride = mmp.Target.TargetPath,
                    }, out var newEntry);
                if (resultst.Any())
                {
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorsOccurredMergingAsset, AssetName,
                        AssetExportInstancedFullPath, string.Join('\n', resultst.Select(x => x.Message))));
                }

                targetExport = newEntry as ExportEntry; // Update the reference target export.
            }
            else
            {
                // Replace the existing content - even if marked as new this might be updating an existing mod
                // 06/09/2024: Remove ImportExportDependencies, change to ReplaceWithRelink... that should be 
                // same still
                var resultst = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink,
                    sourceEntry, targetExport.FileRef, targetExport, true, new RelinkerOptionsPackage()
                    {
                        ErrorOccurredCallback = x =>
                            throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorMergingAssetsX, x)),
                        GamePathOverride = mmp.Target.TargetPath,
                        GenerateImportsForGlobalFiles = false,
                    }, out _);
                if (resultst.Any())
                {
                    throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorsOccurredMergingAsset, AssetName,
                        AssetExportInstancedFullPath, string.Join('\n', resultst.Select(x => x.Message))));
                }
            }

            addMergeWeightCompleted?.Invoke(MergeFileChange1.WEIGHT_ASSETUPDATE);
            return true;
        }
    }

    /// <summary>
    /// Single function compilation
    /// </summary>
    public class ScriptUpdate1 : MergeModUpdateBase
    {
        /// <summary>
        /// Name of text file containing the script
        /// </summary>
        [JsonProperty(@"scriptfilename")]
        public string ScriptFileName { get; set; }

        /// <summary>
        /// MERGE MOD V1 FORMAT ONLY - Script text, uncompressed
        /// </summary>
        [JsonProperty(@"scripttext")]
        public string ScriptText { get; set; }

        public bool ApplyUpdate(IMEPackage package, ExportEntry targetExport, MergeAssetCache1 assetsCache, Mod installingMod, GameTarget gameTarget, Action<int> addMergeWeightCompleted)
        {
            UnrealScriptOptionsPackage usop = new UnrealScriptOptionsPackage() { GamePathOverride = gameTarget.TargetPath };
            FileLib fl = MergeFileChange1.GetFileLibForMerge(package, targetExport.InstancedFullPath, assetsCache, gameTarget, usop);
            (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(targetExport, GetScriptText(), fl, usop);
            if (log.HasErrors)
            {
                M3Log.Error($@"Error compiling function {targetExport.InstancedFullPath}:");
                foreach (var l in log.AllErrors)
                {
                    M3Log.Error(l.Message);
                }

                throw new Exception(M3L.GetString(M3L.string_interp_mergefile_errorCompilingFunction, targetExport.InstancedFullPath, string.Join(Environment.NewLine, log.AllErrors)));
            }
            addMergeWeightCompleted?.Invoke(MergeFileChange1.WEIGHT_SCRIPTUPDATE);
            return true;
        }

        public string GetScriptText()
        {
            if (OwningMM.MergeModVersion >= 2)
            {
                return OwningMM.Assets[ScriptFileName].AsString();
            }

            // V1
            return ScriptText;
        }
    }

    /// <summary>
    /// Allows updating or extending single items on a class
    /// </summary>
    public class AddToClassOrReplace1 : MergeModUpdateBase
    {
        /// <summary>
        /// Name of text file containing the script
        /// </summary>
        [JsonProperty(@"scriptfilenames")]
        public string[] ScriptFileNames { get; set; }


        [Obsolete(@"This is only used in M3Mv1. Do not use in any v2 or newer code!")]
        [JsonProperty(@"scripts")]
        public string[] Scripts { get; set; }

        public bool ApplyUpdate(IMEPackage package, ExportEntry targetExport, MergeAssetCache1 assetsCache, GameTarget gameTarget, Action<int> addMergeWeightCompleted)
        {
            UnrealScriptOptionsPackage usop = new UnrealScriptOptionsPackage() { GamePathOverride = gameTarget.TargetPath };
            FileLib fl = MergeFileChange1.GetFileLibForMerge(package, targetExport.InstancedFullPath, assetsCache, gameTarget, usop);
            for (int i = 0; i < ScriptFileNames.Length; i++)
            {
                Debug.WriteLine(M3L.GetString(M3L.string_interp_updatingX, targetExport.InstancedFullPath));
                MessageLog log = UnrealScriptCompiler.AddOrReplaceInClass(targetExport, GetScriptText(ScriptFileNames[i]), fl, usop);

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
                    MergeFileChange1.ReInitializeFileLib(package, fl, targetExport.InstancedFullPath, usop);
                }
                catch
                {
                    M3Log.Error($@"Could not re-initialize FileLib after adding/replacing '{ScriptFileNames[i]}' to {targetExport.InstancedFullPath}.");
                    throw;
                }
                addMergeWeightCompleted?.Invoke(MergeFileChange1.WEIGHT_SCRIPTUPDATE);
            }

            return true;
        }

        public string GetScriptText(string scriptname)
        {
            if (OwningMM.MergeModVersion >= 2)
            {
                return OwningMM.Assets[scriptname].AsString();
            }

            // V1
            return Scripts[ScriptFileNames.IndexOf(scriptname)];
        }
    }

    public class SequenceSkipUpdate1 : MergeModUpdateBase
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

        public bool ApplyUpdate(IMEPackage package, ExportEntry targetExport, Mod installingMod, Action<int> addMergeWeightCompleted)
        {
            if (MUtilities.CalculateHash(new MemoryStream(targetExport.Data)) == EntryMD5)
            {
                M3Log.Information($@"Applying sequence skip: Skipping {targetExport.InstancedFullPath} through on link {OutboundLinkNameToUse}");
                KismetHelper.SkipSequenceElement(targetExport, outboundLinkName: OutboundLinkNameToUse);
            }
            else
            {
                M3Log.Warning(@"Target export MD5 is incorrect. This may be the wrong target export, or it may be already patched. We are reporting that the mod installed, in the event the target was updated.");
            }

            addMergeWeightCompleted?.Invoke(MergeFileChange1.WEIGHT_SEQSKIPUPDATE);
            return true;
        }
    }
}
