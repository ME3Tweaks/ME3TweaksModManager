using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Targets;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>
    /// One config class as it exists in the installed game.
    /// </summary>
    public class CatalogueClass
    {
        /// <summary>Bare class name, e.g. SFXWave_Horde_Cerberus.</summary>
        public string Name { get; init; }

        /// <summary>
        /// Coalesced section name, "package.class", lowercased at comparison time. This - not the
        /// instanced full path - is the identity the whole catalogue keys on, because it is how the
        /// config addresses a class and how a superclass import lines up with a superclass export.
        /// </summary>
        public string Section { get; init; }

        /// <summary>Section of the superclass, or null at the root of the chain.</summary>
        public string SuperClass { get; init; }

        /// <summary>Coalesced sub-file this class's config lives in: BioGame, BioWeapon, BioAI, ...</summary>
        public string ConfigFile { get; init; }

        /// <summary>Package the class was read from. Diagnostic only.</summary>
        public string SourceFile { get; init; }

        /// <summary>True if the class carries EClassFlags.PerObjectConfig - see docs 02 §1.6.</summary>
        public bool PerObjectConfig { get; init; }

        /// <summary>Config properties DECLARED by this class. Inherited ones are resolved separately.</summary>
        public List<CatalogueProperty> OwnProperties { get; } = new();

        /// <summary>
        /// Scalar values from this class's Default__ object, by property name. Null when the class has
        /// no default object.
        ///
        /// These are what the game uses for any property the merged config does not set - config
        /// REPLACES defaults rather than layering on them - so without them a property the config
        /// never mentions has no known starting value. A subclass restates only what it changes, so
        /// use <see cref="ClassDefaults.Effective"/> rather than reading this directly.
        /// </summary>
        public Dictionary<string, string> Defaults { get; set; }
    }

    /// <summary>
    /// One config property declared on a class.
    /// </summary>
    public class CatalogueProperty
    {
        public string Name { get; init; }

        /// <summary>The UProperty subclass: FloatProperty, ArrayProperty, StructProperty, ...</summary>
        public string DeclaredType { get; init; }

        /// <summary>
        /// True for `globalconfig` rather than plain `config`.
        ///
        /// Recorded, but do NOT use it to decide where a per-object class's property is written. That
        /// was the obvious hypothesis and it is wrong: exactly one of the 638 config properties on
        /// LMP's 47 per-object classes carries this flag, and nothing sets it. Scope is read from
        /// which section shape the shipped config used. See docs/02-data-model.md §1.6.
        /// </summary>
        public bool GlobalConfig { get; init; }

        public int ArraySize { get; init; }

        /// <summary>UnrealScript category, where one is declared.</summary>
        public string Category { get; init; }
    }

    /// <summary>
    /// Reads every config class out of the installed game.
    ///
    /// This is the data layer's foundation: the classes, their config properties, and the inheritance
    /// chain that decides what each page can edit. Nothing here knows the name of any specific piece
    /// of content - see docs/12-generation-and-modded-content.md.
    /// </summary>
    public static class ClassScanner
    {
        /// <summary>
        /// Deserialize Class exports AND property exports.
        ///
        /// Loading only Class data is not enough and fails silently: the config flags live on the
        /// child property exports, and a partial load leaves their data null, so the walk finds ZERO
        /// config properties game-wide while reporting success.
        /// </summary>
        private static bool IsClassOrProperty(ExportEntry e) =>
            // Parents: Children of class
            // Parent Parent: ArrayProperty's type object
            e.ClassName == @"Class" || e.Parent?.ClassName == @"Class" || e.Parent?.Parent?.ClassName == @"Class"
            || ClassDefaults.IsDefaultObject(e);

        /// <summary>
        /// Scans the target's packages. Keyed by section, first occurrence wins: a class cooked into
        /// several packages produces a class export in each, up to 30 copies, and accumulating into a
        /// list rather than a dictionary roughly doubles every count downstream.
        /// </summary>
        /// <param name="progress">Called with (filesDone, filesTotal) periodically.</param>
        public static Dictionary<string, CatalogueClass> Scan(GameTarget target, Action<int, int> progress = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var bySection = new Dictionary<string, CatalogueClass>(StringComparer.OrdinalIgnoreCase);

            var files = MELoadedFiles.GetFilesLoadedInGame(target.Game, forceReload: true, gameRootOverride: target.TargetPath)
                                     .Where(f => !ModMakerCatalogue.IsInCompilerOutput(f.Value))
                                     .ToList();

            int done = 0, failed = 0;
            foreach (var f in files)
            {
                if (++done % 100 == 0) progress?.Invoke(done, files.Count);

                try
                {
                    using var pack = MEPackageHandler.UnsafePartialLoad(f.Value, IsClassOrProperty);
                    var propertiesByOwner = IndexPropertiesByOwner(pack);
                    foreach (var classExport in pack.Exports.Where(x => x.ClassName == @"Class"))
                    {
                        var section = ToSectionName(classExport.InstancedFullPath, f.Value);
                        if (bySection.ContainsKey(section)) continue;

                        UClass uclass;
                        try { uclass = ObjectBinary.From<UClass>(classExport); }
                        catch { failed++; continue; }

                        var cc = new CatalogueClass
                        {
                            Name = classExport.ObjectName.Name,
                            Section = section,
                            SuperClass = ResolveSuperSection(pack, uclass.SuperClass, f.Value),
                            ConfigFile = ToConfigFileName(uclass.ClassConfigName.Name),
                            SourceFile = Path.GetFileName(f.Value),
                            PerObjectConfig = uclass.ClassFlags.Has(UnrealFlags.EClassFlags.PerObjectConfig),
                        };
                        cc.OwnProperties.AddRange(CollectConfigProperties(propertiesByOwner, classExport));
                        cc.Defaults = ClassDefaults.Read(pack, uclass.Defaults);

                        // Every class is recorded, including those declaring nothing of their own: a
                        // subclass that declares no config still inherits its parent's and is still a
                        // page. SFXWave_Horde_Cerberus declares nothing at all.
                        bySection[section] = cc;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            progress?.Invoke(files.Count, files.Count);
            if (failed > 0) MLog.Information($@"ModMaker catalogue: {failed} package(s) could not be read during the class scan");
            return bySection;
        }

        /// <summary>
        /// Config properties of a class plus everything it inherits, nearest declaration winning.
        /// Returns (property, declaringSection) pairs so the editor can show where a value comes from.
        /// </summary>
        public static List<(CatalogueProperty Property, string DeclaredBy)> EffectiveProperties(
            string section, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var byName = new Dictionary<string, (CatalogueProperty, string)>(StringComparer.OrdinalIgnoreCase);
            foreach (var cls in Chain(section, classes))
            {
                foreach (var p in cls.OwnProperties)
                    byName.TryAdd(p.Name, (p, cls.Section));
            }
            return byName.Values.Select(v => (v.Item1, v.Item2)).ToList();
        }

        /// <summary>
        /// A class and its ancestors, nearest first. Cycle-safe and depth-capped: a malformed chain
        /// should degrade, not hang the catalogue build.
        /// </summary>
        public static IEnumerable<CatalogueClass> Chain(string section, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cur = section;
            int guard = 0;
            while (cur != null && guard++ < 64 && seen.Add(cur) && classes.TryGetValue(cur, out var cls))
            {
                yield return cls;
                cur = cls.SuperClass;
            }
        }

        /// <summary>
        /// Every class descending from the given base, which is how content is discovered instead of
        /// listed. Passing SFXWave_Horde returns all eight faction classes on LMP, including Milan's
        /// own unbuilt one and anything a user's mod adds.
        /// </summary>
        public static List<CatalogueClass> Descendants(string baseSection, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            return classes.Values
                          .Where(c => !c.Section.Equals(baseSection, StringComparison.OrdinalIgnoreCase)
                                   && Chain(c.Section, classes).Skip(1)
                                        .Any(a => a.Section.Equals(baseSection, StringComparison.OrdinalIgnoreCase)))
                          .OrderBy(c => c.Section, StringComparer.OrdinalIgnoreCase)
                          .ToList();
        }

        /// <summary>
        /// Finds a class by bare name, for resolving an anchor like "SFXWave_Horde" that is written
        /// without its package. Returns null when the anchor is missing, which the caller must report
        /// as a named warning rather than as absent content.
        /// </summary>
        public static CatalogueClass FindByName(string bareName, IReadOnlyDictionary<string, CatalogueClass> classes) =>
            classes.Values.FirstOrDefault(c => c.Name.Equals(bareName, StringComparison.OrdinalIgnoreCase));

        // ------------------------------------------------------------------ internals

        /// <summary>
        /// The coalesced addresses classes as "package.class". A class nested under a package export
        /// already has that shape in its instanced full path; a root-level export does not, so it is
        /// prefixed with the package file name. Keying on the raw instanced full path instead
        /// silently fails to resolve most of the hierarchy.
        /// </summary>
        private static string ToSectionName(string instancedFullPath, string packageFile) =>
            instancedFullPath.Contains('.')
                ? instancedFullPath
                : $@"{Path.GetFileNameWithoutExtension(packageFile)}.{instancedFullPath}";

        private static string ResolveSuperSection(IMEPackage pack, int uindex, string packageFile)
        {
            if (uindex == 0) return null;
            var entry = pack.GetEntry(uindex);
            if (entry == null) return null;
            // An import's path is already package-qualified; an export in this package may not be.
            return entry is ImportEntry ? entry.InstancedFullPath : ToSectionName(entry.InstancedFullPath, packageFile);
        }

        private static string ToConfigFileName(string classConfigName) =>
            string.IsNullOrWhiteSpace(classConfigName) || classConfigName == @"None" ? null : @"Bio" + classConfigName;

        /// <summary>
        /// Property exports grouped by the class that owns them, built once per package.
        ///
        /// Ownership is the relationship being used, and it is an engine invariant: a variable declared
        /// in a class has that class as its outer, in any package, whatever order it was emitted in and
        /// whoever wrote it. Nothing here reads a position or a sequence.
        ///
        /// The alternative is UStruct.Children, a linked list threaded through UField.Next, and it is
        /// what LEC walks itself (ScriptObjectToASTConverter.ConvertClass). It cannot be used from a
        /// partial load: the list also threads through the class's functions, states, enums and script
        /// structs, this scan loads none of those, and an export with no data throws when deserialized
        /// - taking Next down with it, so the walk cannot continue past one.
        ///
        /// The first version walked it anyway and swallowed the throw with `catch { break; }`, cutting
        /// each class off at its first function. That must not be mistaken for a near miss that
        /// happened to work: it was correct on this data only because these classes list their
        /// variables before their functions, which is a compiler habit and not a rule the engine cares
        /// about. A hand-written class that interleaves them would have lost properties silently. It
        /// also threw and caught 2,768 exceptions per scan, stopping the debugger on each.
        ///
        /// That the two enumerations agree is verified rather than assumed:
        /// ModMakerCatalogueTests.PropertyEnumerationMatchesTheScriptChain walks the chain as the
        /// oracle - with a partial load wide enough to complete it - and asserts an exact match for
        /// every class in the game.
        /// </summary>
        private static Dictionary<int, List<ExportEntry>> IndexPropertiesByOwner(IMEPackage pack)
        {
            var byOwner = new Dictionary<int, List<ExportEntry>>();
            foreach (var e in pack.Exports)
            {
                if (!e.ClassName.EndsWith(@"Property", StringComparison.Ordinal)) continue;
                if (e.idxLink == 0) continue;
                if (!byOwner.TryGetValue(e.idxLink, out var list)) byOwner[e.idxLink] = list = new List<ExportEntry>();
                list.Add(e);
            }
            return byOwner;
        }

        /// <summary>
        /// The config properties a class declares.
        ///
        /// Flags come off the deserialized UProperty, NOT ExportEntry.GetPropertyFlags(): the latter
        /// only works when the export HasStack, which is true for almost no property exports, and
        /// using it finds only bools - 85 properties in SFXGame.pcc instead of 1,455.
        /// </summary>
        private static List<CatalogueProperty> CollectConfigProperties(
            Dictionary<int, List<ExportEntry>> propertiesByOwner, ExportEntry classExport)
        {
            var result = new List<CatalogueProperty>();
            if (!propertiesByOwner.TryGetValue(classExport.UIndex, out var children)) return result;

            foreach (var child in children)
            {
                // Checked, not caught. An export the partial load skipped has no data and throws when
                // deserialized; a try/catch would be correct at runtime and still stop the debugger on
                // every occurrence, so the condition is tested for instead.
                if (child.DataReadOnly.Length == 0)
                {
                    MLog.Warning($@"ModMaker catalogue: {child.InstancedFullPath} has no loaded data; skipped");
                    continue;
                }

                if (ObjectBinary.From(child) is not UProperty prop) continue;

                bool isGlobal = prop.PropertyFlags.Has(UnrealFlags.EPropertyFlags.GlobalConfig);
                if (!isGlobal && !prop.PropertyFlags.Has(UnrealFlags.EPropertyFlags.Config)) continue;

                result.Add(new CatalogueProperty
                {
                    Name = child.ObjectName.Name,
                    DeclaredType = child.ClassName,
                    GlobalConfig = isGlobal,
                    ArraySize = prop.ArraySize,
                    Category = prop.Category.Name,
                });
            }

            return result;
        }
    }
}
