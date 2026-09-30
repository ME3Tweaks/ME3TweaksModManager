using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LegendaryExplorerCore.Coalesced;
using ME3TweaksModManager.modmaker.editor;
using ME3TweaksModManager.modmaker.generator;
using ME3TweaksModManager.modmaker.project;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.compiler
{
    /// <summary>How much a compile problem matters. An error means no manifest is produced.</summary>
    public enum CompileProblemSeverity
    {
        Warning,
        Error,
    }

    public record CompileProblem(CompileProblemSeverity Severity, string Where, string Message)
    {
        public override string ToString() => $@"{Severity}: {Where} - {Message}";
    }

    /// <summary>
    /// What a compile produced: a manifest, or the reasons there is not one.
    ///
    /// A refused compile returns no manifest at all rather than a partial one. Half a mod is worse
    /// than none here - it installs, it looks built, and the missing half is silent.
    /// </summary>
    public class CompileResult
    {
        public BuildManifest Manifest { get; init; }

        public List<CompileProblem> Problems { get; } = new();

        public bool Succeeded => Manifest != null;

        public IEnumerable<CompileProblem> Errors =>
            Problems.Where(p => p.Severity == CompileProblemSeverity.Error);

        public IEnumerable<CompileProblem> Warnings =>
            Problems.Where(p => p.Severity == CompileProblemSeverity.Warning);

        /// <summary>Rows that were written. Zero with no errors means a mod that changes nothing.</summary>
        public int WavelistRowsWritten { get; internal set; }

        /// <summary>
        /// Category rows written. One per (level, enemy) touched, not one per stat: a category is a
        /// single array entry, so twenty edits to one enemy are one replacement.
        /// </summary>
        public int EnemyRowsWritten { get; internal set; }

        /// <summary>Map entries and availability changes written. Both are one config write each.</summary>
        public int MaplistWritesWritten { get; internal set; }

        /// <summary>Objective values and wave entries written.</summary>
        public int ObjectiveWritesWritten { get; internal set; }

        /// <summary>Weapon stat overrides written.</summary>
        public int WeaponWritesWritten { get; internal set; }

        /// <summary>
        /// Properties written from the generic value surface. One per PROPERTY, not per leaf: a
        /// property is written whole, so twenty edits inside one struct are one write.
        /// </summary>
        public int ValuePropertiesWritten { get; internal set; }

        /// <summary>Everything written, which is what "did this mod do anything" turns on.</summary>
        public int RowsWritten =>
            WavelistRowsWritten + EnemyRowsWritten + MaplistWritesWritten + ValuePropertiesWritten
            + ObjectiveWritesWritten + WeaponWritesWritten;

        /// <summary>Edits whose composition matched what shipped, and so were not written.</summary>
        public int UnchangedEditsSkipped { get; internal set; }
    }

    /// <summary>
    /// What the compiler needs from the machine it is running on.
    ///
    /// Everything here is read from the USER'S OWN INSTALL at compile time, never from the project.
    /// That is the rule that makes an override survive a Milan update: the project stores what the
    /// user wants, and the text to subtract in order to get there is whatever is installed today. A
    /// project carrying its own copy of the shipped text would pin the mod to the build it was
    /// authored against, and the failure would be silent - array subtraction that does not match is
    /// a no-op, not an error.
    /// </summary>
    public class CompileInputs
    {
        /// <summary>The wavelists as installed. Required whenever the project has wavelist edits.</summary>
        public WavelistCatalogue Waves { get; init; }

        /// <summary>The difficulty tables as installed. Required whenever the project has enemy edits.</summary>
        public DifficultyCatalogue Difficulty { get; init; }

        /// <summary>The map roster as installed. Required whenever the project has maplist edits.</summary>
        public MapListCatalogue Maps { get; init; }

        /// <summary>The objectives as installed. Required whenever the project has objective edits.</summary>
        public ObjectiveCatalogue Objectives { get; init; }

        /// <summary>The weapons as installed. Required whenever the project has weapon edits.</summary>
        public WeaponCatalogue Weapons { get; init; }

        /// <summary>The merged config, indexed - for the coalesced sub-file each section lives in.</summary>
        public SectionIndex Sections { get; init; }

        /// <summary>
        /// The scanned classes, by class name. Required for value edits to properties the config does
        /// not set.
        ///
        /// A leaf id carries a property name lowercased, and the install's own spelling is normally
        /// recovered from the section that sets it. A property whose value comes from the class's
        /// COMPILED default is not in any section, so the only place its real spelling exists is the
        /// class definition it was declared in.
        /// </summary>
        public IReadOnlyDictionary<string, CatalogueClass> Classes { get; init; }

        /// <summary>
        /// The per-object classes as installed. Required only for value edits that name an instance.
        ///
        /// An instance's section is "&lt;instance&gt; &lt;class token&gt;", and the class token is the
        /// class's NAME while a value key carries its SECTION - the two are different strings and one
        /// cannot be derived from the other, so the mapping has to be read from the install.
        /// </summary>
        public InstanceCatalogue Instances { get; init; }

        /// <summary>Game identity written into the manifest. LMP is Milan's own.</summary>
        public string Game { get; init; } = @"LMP";

        /// <summary>
        /// Mount priority for the generated DLC. Must be above Project Milan's, or the mod's config
        /// merges underneath the thing it is trying to override and nothing happens.
        /// </summary>
        public int? MountPriority { get; init; }

        /// <summary>
        /// Mount flags for the generated DLC, overriding the rule in <see cref="InstallMount.Flags"/>.
        ///
        /// Normally left null: the flags follow from the project's own "require all players" setting,
        /// and a caller passing its own would be a second place for that decision to live.
        /// </summary>
        public int? MountFlags { get; init; }

        /// <summary>String ref for the DLC's display name. Fixed; there is only one ModMaker DLC.</summary>
        public int TlkId { get; init; } = ModMakerCatalogue.CompilerTlkId;

        /// <summary>Provenance for the manifest's generator block. Diagnostic only.</summary>
        public string App { get; init; }
        public string MilanVersion { get; init; }
    }

    /// <summary>
    /// Turns a project into a build manifest: the resolution half of the compiler.
    ///
    /// This is where every rule that needs the installed game gets applied, and it is deliberately
    /// separate from the half that writes files. A manifest can be produced, inspected and tested
    /// without touching a DLC folder, which matters because the mistakes this code can make - a
    /// subtraction that does not match, a section written into the wrong coalesced sub-file - do not
    /// throw. They produce a mod that builds and misbehaves.
    /// </summary>
    public static class ProjectCompiler
    {
        /// <summary>The wavelist array property, and the one thing about it that is not read from config.</summary>
        private const string EnemiesProperty = @"Enemies";

        /// <summary>
        /// The difficulty array property for one level. The number comes from the handler, never from
        /// counting: the singleplayer table ships five of these inside this same game.
        /// </summary>
        private static string DifficultyProperty(int level) => $@"Level{level}DifficultyData";

        /// <summary>The two map properties. Both live in the match-settings section, which is anchored.</summary>
        /// <summary>
        /// The number the coalesced format stores for a parse action.
        ///
        /// There are TWO numberings and they disagree. `CoalesceParseAction` is a C# enum whose
        /// ordinals are the order the members happen to be declared in - Add 0, AddUnique 1, New 2,
        /// None 3, Remove 4, RemoveProperty 5 - and the format's own numbering is New 0,
        /// RemoveProperty 1, Add 2, AddUnique 3, Remove 4. <see cref="PropertyWrite.ValueType"/> is
        /// the format's, because <see cref="ModEmitter"/> hands it to the int overload of
        /// `CoalesceValue`, which stores it verbatim.
        ///
        /// Casting the enum was wrong at every site but one, and wrong in ways that look like nothing:
        /// `New` went out as 2, which is Add, so an overwrite appended instead; `AddUnique` went out
        /// as 1, which is RemoveProperty, so adding one value to a list deleted the list.
        /// `Remove` is 4 in both numberings, which is why the half of the map editor that subtracts
        /// worked and the half that adds did not.
        ///
        /// Everything goes through here so there is one place to be wrong in, and it is not a cast.
        /// </summary>
        private static int Wire(CoalesceParseAction action) => CoalesceValue.GetValueType(action);

        private const string AvailableMapsProperty = @"AvailableMaps";

        /// <summary>The lobby's enemy picker. A faction is offered exactly when this list holds its entry.</summary>
        private const string EnemyTypesProperty = @"EnemyTypes";

        /// <summary>The lobby's difficulty picker, the same way.</summary>
        private const string ChallengeTypesProperty = @"ChallengeTypes";
        private const string MasterMapListProperty = @"MasterMapList";

        public static CompileResult Compile(ModMakerProject project, CompileInputs inputs)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            var problems = new List<CompileProblem>();
            void Error(string where, string message) =>
                problems.Add(new CompileProblem(CompileProblemSeverity.Error, where, message));
            void Warn(string where, string message) =>
                problems.Add(new CompileProblem(CompileProblemSeverity.Warning, where, message));

            var manifest = new BuildManifest
            {
                Game = inputs.Game ?? (string)project.Bundle[@"game"],
                Generator = new BuildGenerator
                {
                    App = inputs.App,
                    BundleVersion = (string)project.Bundle[@"version"],
                    MilanVersion = inputs.MilanVersion,
                    GeneratedAt = DateTime.UtcNow.ToString(@"o"),
                },
                Mod = new BuildMod
                {
                    Name = project.Name,
                    Author = (string)project.Meta[@"author"],
                    Description = (string)project.Meta[@"description"],
                    Version = ((int?)project.Meta[@"revision"] ?? 1).ToString(),
                    // Fixed, not chosen. A ModMaker mod is a single DLC, and building a second one
                    // replaces the first - which is also what lets the generator exclude its own
                    // output from every scan by name.
                    DlcFolder = ModMakerCatalogue.CompilerDLCFolderName,
                    Mount = new BuildMount
                    {
                        Priority = inputs.MountPriority,
                        // The project's setting, not the caller's: a mod that forces every player
                        // to install it is a decision the author made and saved, not a compile option.
                        Flags = inputs.MountFlags ?? InstallMount.Flags(project.RequireAllPlayers),
                        TlkId = inputs.TlkId,
                    },
                },
            };

            var result = new CompileResult { Manifest = manifest };

            RefuseWhatCannotBeResolvedYet(project, Error);
            CompileValues(project, inputs, manifest, result, Error);
            CompileWavelists(project, inputs, manifest, result, Error, Warn);
            CompileEnemyStats(project, inputs, manifest, result, Error, Warn);
            CompileMaplist(project, inputs, manifest, result, Error, Warn);
            CompileObjectives(project, inputs, manifest, result, Error, Warn);
            CompileWeapons(project, inputs, manifest, result, Error, Warn);

            if (problems.Any(p => p.Severity == CompileProblemSeverity.Error))
            {
                var refused = new CompileResult();
                refused.Problems.AddRange(problems);
                return refused;
            }

            if (result.RowsWritten == 0)
                Warn(@"project", @"nothing was written; this mod would install and change nothing");

            result.Problems.AddRange(problems);
            return result;
        }

        /// <summary>
        /// Refuses content this build cannot turn into config, instead of dropping it.
        ///
        /// The alternative is the worst outcome available: a mod that compiles cleanly, installs, and
        /// is missing exactly the edits the compiler did not understand. A user cannot tell that from
        /// a mod that works, so the compile stops and says which part it could not do.
        /// </summary>
        private static void RefuseWhatCannotBeResolvedYet(ModMakerProject project, Action<string, string> error)
        {
            foreach (var id in project.CollectionIds)
            {
                if (id.Equals(WavelistEditDocument.CollectionId, StringComparison.OrdinalIgnoreCase)) continue;
                if (id.Equals(EnemyEditDocument.CollectionId, StringComparison.OrdinalIgnoreCase)) continue;
                if (id.Equals(MaplistEditDocument.CollectionId, StringComparison.OrdinalIgnoreCase)) continue;
                error($@"collections.{id}",
                    @"this build has no compiler for that collection; its edits would be silently omitted");
            }
        }

        /// <summary>
        /// The generic value surface: sparse leaf overrides keyed "&lt;class&gt;[@&lt;instance&gt;]/&lt;leaf&gt;".
        ///
        /// Everything needed to resolve one is read from the INSTALL, never from the project or the
        /// bundle the project was authored against. That includes the leaf id scheme itself: whether a
        /// property's leaves are rooted at "prop" or at "prop.0" depends on how many values the config
        /// sets for it, so a property that gained a second value since the edit was made produces a
        /// leaf that no longer resolves - and that is reported rather than written somewhere plausible.
        /// </summary>
        private static void CompileValues(
            ModMakerProject project, CompileInputs inputs, BuildManifest manifest, CompileResult result,
            Action<string, string> error)
        {
            if (project.Document[@"values"] is not JObject values || !values.HasValues) return;

            if (inputs.Sections == null)
            {
                error(@"values", @"the installed game's config was not read, so these edits cannot be resolved");
                return;
            }

            // Grouped by the property they land in, because a property is written WHOLE. Two edits to
            // two leaves of one struct are one write; emitted separately, the second would overwrite
            // the first and the user would lose an edit without being told.
            var byProperty = new Dictionary<string, PropertyEdits>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in values.Properties())
            {
                if (!TryResolveValueKey(entry.Name, inputs, out var sectionKey, out var classId,
                                        out var propertyName, out var leafId, out var why))
                {
                    error($@"values.{entry.Name}", why);
                    continue;
                }

                var groupKey = $@"{sectionKey}\{propertyName}";
                if (!byProperty.TryGetValue(groupKey, out var group))
                {
                    byProperty[groupKey] = group = new PropertyEdits
                    {
                        SectionKey = sectionKey,
                        ClassId = classId,
                        PropertyName = propertyName,
                    };
                }

                group.Edits.Add(new LeafEdit(leafId, entry.Value));
            }

            foreach (var group in byProperty.Values)
            {
                CompileValueProperty(group, inputs, manifest, result, error);
            }
        }

        private static void CompileValueProperty(
            PropertyEdits group, CompileInputs inputs, BuildManifest manifest, CompileResult result,
            Action<string, string> error)
        {
            var where = $@"values.{group.SectionKey}/{group.PropertyName}";

            // Where to write. The merged config is the first authority - it says which sub-file the
            // section is ALREADY in - but a section nothing has ever written to is not in it at all,
            // and that is the normal state for a class whose values all come from compiled defaults.
            // The class itself knows: `config(Game)` on the class declaration is what puts its section
            // in BioGame, whether or not anyone has written a line of it yet.
            var asset = inputs.Sections.AssetOf(group.SectionKey)
                        ?? inputs.Sections.AssetOf(group.ClassId)
                        ?? ClassForSection(inputs, group.ClassId)?.ConfigFile;

            if (string.IsNullOrWhiteSpace(asset))
            {
                error(where, $@"'{group.SectionKey}' is not in the merged config and its class does not " +
                             @"name a config file, so the coalesced sub-file to write it into is unknown");
                return;
            }

            var live = inputs.Sections.Values(group.SectionKey, group.PropertyName).Select(v => v.Value).ToList();
            var idBase = group.PropertyName.ToLowerInvariant();

            if (live.Count == 0)
            {
                // The config does not set this property, so its value comes from the class's compiled
                // default. There is no shipped text to splice into - but for a bare scalar there is
                // nothing to splice: the leaf IS the whole value, so it can simply be written. A leaf
                // deeper than that would need a structure the config never wrote down.
                string why = null;
                if (group.Edits.Count == 1 && group.Edits[0].LeafId == idBase
                    && ValueWriter.TryRenderBare(group.Edits[0].Value, out var written, out why))
                {
                    manifest.Coalesced().File(asset).Section(group.SectionKey).Properties.Add(new PropertyWrite
                    {
                        Name = group.PropertyName,
                        Value = written,
                        ValueType = Wire(CoalesceParseAction.New),
                        Source = where,
                    });
                    result.ValuePropertiesWritten++;
                    return;
                }

                error(where, $@"the installed game's config does not set this property, so only the " +
                             $@"property itself can be set, not a value inside it{(why == null ? string.Empty : $@" ({why})")}");
                return;
            }

            if (live.Count == 1)
            {
                if (!ValueWriter.TryApply(live[0], idBase, group.Edits, out var updated, out var problem))
                {
                    error(where, problem);
                    return;
                }

                // An edit that sets a value to what it already was is not written. It is not an error
                // either - a user is allowed to type the shipped number - but a mod that rewrites a
                // property to its own current text is a diff with nothing in it.
                if (string.Equals(updated, live[0], StringComparison.Ordinal))
                {
                    result.UnchangedEditsSkipped += group.Edits.Count;
                    return;
                }

                manifest.Coalesced().File(asset).Section(group.SectionKey).Properties.Add(new PropertyWrite
                {
                    Name = group.PropertyName,
                    Value = updated,
                    ValueType = Wire(CoalesceParseAction.New),
                    Source = where,
                });
                result.ValuePropertiesWritten++;
                return;
            }

            CompileValueArray(group, inputs, manifest, result, error, asset, live, idBase, where);
        }

        /// <summary>
        /// A property the config sets more than once, which in coalesced terms is an array.
        ///
        /// Replaced wholesale rather than spliced entry by entry - see docs/02-data-model.md §0.1. The
        /// entries nobody edited are re-emitted from their VERBATIM shipped text, never from the parsed
        /// tree, so an edit to one entry cannot quietly reformat the other forty.
        /// </summary>
        private static void CompileValueArray(
            PropertyEdits group, CompileInputs inputs, BuildManifest manifest, CompileResult result,
            Action<string, string> error, string asset, List<string> live, string idBase, string where)
        {
            var perEntry = new List<List<LeafEdit>>(live.Count);
            for (int i = 0; i < live.Count; i++) perEntry.Add([]);

            var prefix = idBase + @".";
            foreach (var edit in group.Edits)
            {
                if (!edit.LeafId.StartsWith(prefix, StringComparison.Ordinal))
                {
                    error(where, $@"'{edit.LeafId}' names the property itself, but the installed game sets it " +
                                 $@"{live.Count} times, so every leaf belongs to a numbered entry");
                    return;
                }

                var rest = edit.LeafId[prefix.Length..];
                var dot = rest.IndexOf('.');
                var indexText = dot < 0 ? rest : rest[..dot];

                if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                    || index < 0 || index >= live.Count)
                {
                    error(where, $@"'{edit.LeafId}' refers to entry {indexText}, and the installed game has " +
                                 $@"{live.Count}; entry ids are positional, so an entry added or removed since " +
                                 @"this edit was made shifts them");
                    return;
                }

                perEntry[index].Add(edit);
            }

            var rebuilt = new List<string>(live);
            var changed = false;

            for (int i = 0; i < live.Count; i++)
            {
                if (perEntry[i].Count == 0) continue;

                if (!ValueWriter.TryApply(live[i], $@"{idBase}.{i}", perEntry[i], out var updated, out var problem))
                {
                    error(where, problem);
                    return;
                }

                if (string.Equals(updated, live[i], StringComparison.Ordinal)) continue;
                rebuilt[i] = updated;
                changed = true;
            }

            if (!changed)
            {
                result.UnchangedEditsSkipped += group.Edits.Count;
                return;
            }

            var section = manifest.Coalesced().File(asset).Section(group.SectionKey);

            // Clear, then add. RemoveProperty (1) takes no value and drops every entry the game has;
            // each entry then goes back in. A New (0) would work for the FIRST entry only - it sets
            // the property to a one-item list - and using it here would make the first entry
            // structurally different from the rest for no reason.
            section.Properties.Add(new PropertyWrite
            {
                Name = group.PropertyName,
                Value = string.Empty,
                ValueType = Wire(CoalesceParseAction.RemoveProperty),
                Source = where,
            });

            foreach (var text in rebuilt)
            {
                section.Properties.Add(new PropertyWrite
                {
                    Name = group.PropertyName,
                    Value = text,
                    // AddUnique (3), not Add (2). This mod is a DLC, and shipped DLC uses 3 for array
                    // additions - 2 is what the BASEGAME coalesced uses. The clear above means nothing
                    // can be duplicated either way, so this is about writing what every other DLC in
                    // the game writes rather than about the uniqueness check, which is also why the
                    // library's known AddUnique bug cannot bite here. The dedicated editors already
                    // emit 3 for their replacements.
                    ValueType = Wire(CoalesceParseAction.AddUnique),
                    Source = where,
                });
            }

            result.ValuePropertiesWritten++;
        }

        /// <summary>
        /// Turns one value key into the section and property it addresses, or says why it does not
        /// address one.
        /// </summary>
        private static bool TryResolveValueKey(
            string key, CompileInputs inputs, out string sectionKey, out string classId,
            out string propertyName, out string leafId, out string problem)
        {
            sectionKey = classId = propertyName = leafId = null;
            problem = null;

            var slash = key.IndexOf('/');
            if (slash <= 0 || slash == key.Length - 1)
            {
                problem = @"a value key reads '<class>[@<instance>]/<leaf>'; this one does not";
                return false;
            }

            var head = key[..slash];

            // Lowercased because that is how the generator mints leaf ids, and a key that differs only
            // in case is the same leaf. Matching it case-sensitively would report a leaf as missing
            // from a value that plainly contains it.
            leafId = key[(slash + 1)..].ToLowerInvariant();

            // A "[dimension]" suffix selects one of a property's repeated variants - per difficulty
            // level, say. Which variants exist is bundle data the compiler does not read, so rather
            // than let the bracket fall through and fail as a missing leaf, it is named.
            if (leafId.IndexOf('[') >= 0)
            {
                problem = @"this key selects a dimension, which this build cannot compile yet";
                return false;
            }

            classId = head;
            string instance = null;
            var at = head.IndexOf('@');
            if (at >= 0)
            {
                classId = head[..at];
                instance = head[(at + 1)..];
            }

            if (classId.Length == 0)
            {
                problem = @"the key names no class";
                return false;
            }

            if (instance == null) sectionKey = classId;
            else
            {
                if (instance.Length == 0)
                {
                    problem = @"the key has an '@' with no instance after it";
                    return false;
                }

                if (inputs.Instances == null)
                {
                    problem = @"the installed game's per-object classes were not read, so an instance cannot be resolved";
                    return false;
                }

                if (!inputs.Instances.BySection.TryGetValue(classId, out var perObject))
                {
                    problem = $@"'{classId}' is not a per-object class in the installed game, so '{instance}' names nothing";
                    return false;
                }

                sectionKey = $@"{instance} {perObject.ClassToken}";
            }

            // The leaf id's first segment is the property name as the generator lowercased it. Config
            // property names never contain a dot, so the first dot is the boundary.
            var dot = leafId.IndexOf('.');
            var wanted = dot < 0 ? leafId : leafId[..dot];

            // The section is the first place to look, because a property the config sets carries the
            // install's own spelling right there.
            var section = inputs.Sections.Find(sectionKey);
            if (section != null)
            {
                foreach (var candidate in section)
                {
                    if (!candidate.Key.Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
                    propertyName = candidate.Key;
                    return true;
                }
            }

            // Otherwise the property is one the config never sets, so its effective value is the
            // class's compiled default and its real spelling exists only in the class that declared
            // it. Writing the lowercased id instead would work - the game matches property names
            // case-insensitively - and would put a name into the file that appears nowhere else in
            // the game's own config, which is not a thing to do to someone reading a diff.
            var declared = DeclaredPropertyName(inputs, classId, wanted);
            if (declared != null)
            {
                propertyName = declared;
                return true;
            }

            problem = section == null
                ? $@"'{sectionKey}' is not a section in the installed game's config"
                : $@"'{sectionKey}' has no property '{wanted}' in the installed game's config or in its class";
            return false;
        }

        /// <summary>
        /// The declared spelling of a property, found by the section its class configures.
        ///
        /// Walks the SuperClass chain, because a property is editable on every class that inherits it
        /// and the declaration lives on whichever ancestor introduced it.
        /// </summary>
        private static string DeclaredPropertyName(CompileInputs inputs, string classSection, string lowercased)
        {
            var cls = ClassForSection(inputs, classSection);

            var guard = 0;
            while (cls != null && guard++ < 64)
            {
                foreach (var property in cls.OwnProperties)
                {
                    if (property.Name != null
                        && property.Name.Equals(lowercased, StringComparison.OrdinalIgnoreCase))
                        return property.Name;
                }

                if (cls.SuperClass == null || !inputs.Classes.TryGetValue(cls.SuperClass, out var parent)) break;
                cls = parent;
            }

            return null;
        }

        /// <summary>The class configured by a section, or null.</summary>
        private static CatalogueClass ClassForSection(CompileInputs inputs, string section) =>
            inputs.Classes?.Values.FirstOrDefault(
                c => c.Section != null && c.Section.Equals(section, StringComparison.OrdinalIgnoreCase));

        /// <summary>Every leaf edit landing in one property of one section.</summary>
        private sealed class PropertyEdits
        {
            public string SectionKey { get; init; }

            /// <summary>
            /// The class-path section, which is the key's class half. Same as <see cref="SectionKey"/>
            /// unless the key named an instance - and then this is the only one that finds the class.
            /// </summary>
            public string ClassId { get; init; }

            public string PropertyName { get; init; }
            public List<LeafEdit> Edits { get; } = [];
        }

        private static void CompileWavelists(
            ModMakerProject project, CompileInputs inputs, BuildManifest manifest, CompileResult result,
            Action<string, string> error, Action<string, string> warn)
        {
            var edits = project.WavelistEdits;
            // Rows, not Count. This collection also carries the faction toggles, and those are a write
            // to the match settings section - compiled and checked with the maps, where that section
            // is. A project that only turned a faction off has nothing for this to do, and must not be
            // told the wavelists could not be read.
            if (edits.Rows.Count == 0) return;

            if (inputs.Waves == null)
            {
                error(@"collections.wavelist", @"the installed game's wavelists were not read, so the edits cannot be resolved");
                return;
            }

            // The same checks the editor runs, run again here against the install being compiled
            // against - which is not necessarily the one the project was authored on.
            foreach (var problem in WavelistEditValidator.Validate(edits, inputs.Waves))
            {
                if (problem.Severity == WavelistProblemSeverity.Error) error(problem.Cell, problem.Message);
                else warn(problem.Cell, problem.Message);
            }

            foreach (var row in edits.Rows)
            {
                var where = $@"{row.InstanceId} wave {row.WaveIndex} {row.Difficulty}";

                var faction = inputs.Waves.Factions.FirstOrDefault(
                    f => f.Section.Equals(row.Section, StringComparison.OrdinalIgnoreCase));
                var wave = faction?.Waves.FirstOrDefault(
                    w => string.Equals(w.InstanceId, row.InstanceId, StringComparison.OrdinalIgnoreCase));
                if (wave == null) continue; // Already reported by the validator.

                if (string.IsNullOrWhiteSpace(wave.SectionKey))
                {
                    error(where, @"this wave has no config section, so there is nothing to write to");
                    continue;
                }

                // More than one shipped row for a difficulty makes an exact-text subtraction
                // ambiguous: removing one leaves the others, and which the game uses is unverified.
                // Refused rather than guessed - see docs/05-wavelists.md on the Collector wave 5 row.
                var shipped = wave.Rows
                    .Where(r => string.Equals(r.Difficulty, row.Difficulty, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (shipped.Count == 0)
                {
                    error(where, @"the installed game has no row for this difficulty, so there is nothing to replace");
                    continue;
                }

                if (shipped.Count > 1)
                {
                    error(where,
                        $@"the installed game has {shipped.Count} rows for this difficulty; removing one would leave the others, " +
                        @"and which one the game uses is unverified. Correct the config before editing this wave.");
                    continue;
                }

                var original = shipped[0].RawValue;
                if (string.IsNullOrEmpty(original))
                {
                    error(where, @"the shipped row's original text was not captured, so it cannot be subtracted");
                    continue;
                }

                var replacement = WaveRowWriter.Write(row);
                if (string.Equals(original, replacement, StringComparison.Ordinal))
                {
                    // Composed back to exactly what shipped. Writing Remove-then-Add of identical
                    // text is a no-op with a chance of going wrong, so it is not written at all.
                    result.UnchangedEditsSkipped++;
                    continue;
                }

                var asset = inputs.Sections?.AssetOf(wave.SectionKey);
                if (string.IsNullOrWhiteSpace(asset))
                {
                    error(where,
                        $@"'{wave.SectionKey}' was not found in the merged config, so the coalesced sub-file to write it into is unknown");
                    continue;
                }

                manifest.Coalesced()
                        .File(asset)
                        .Section(wave.SectionKey)
                        .Properties.Add(new PropertyWrite
                        {
                            Name = PropertyNameAsShipped(inputs.Sections, wave.SectionKey, EnemiesProperty),
                            Source = $@"wavelist/{row.InstanceId}/{row.Difficulty}",
                            Replace = new ValueReplacement { Remove = original, Add = replacement },
                        });

                result.WavelistRowsWritten++;
            }
        }

        /// <summary>
        /// Resolves enemy stat edits into category-row replacements.
        ///
        /// The unit of emission is the CATEGORY ROW, not the stat: a category is one entry of
        /// Level&lt;n&gt;DifficultyData, so an edit to one of the Collector Captain's 41 stats means
        /// removing all 41 by exact text and adding them back with one number changed. That is why the
        /// edits are grouped here before anything is written - twenty edits to one enemy must produce
        /// one replacement, not twenty that each subtract text the previous one already removed.
        /// </summary>
        private static void CompileEnemyStats(
            ModMakerProject project, CompileInputs inputs, BuildManifest manifest, CompileResult result,
            Action<string, string> error, Action<string, string> warn)
        {
            var edits = project.EnemyEdits;
            if (edits.Count == 0) return;

            if (inputs.Difficulty == null)
            {
                error($@"collections.{EnemyEditDocument.CollectionId}",
                    @"the installed game's difficulty tables were not read, so the edits cannot be resolved");
                return;
            }

            foreach (var problem in EnemyEditValidator.Validate(edits, inputs.Difficulty))
            {
                if (problem.Severity == EnemyProblemSeverity.Error) error(problem.Where, problem.Message);
                else warn(problem.Where, problem.Message);
            }

            foreach (var group in edits.ByRow())
            {
                var first = group.First();
                var where = $@"{first.Category} level {first.Level}";

                var table = inputs.Difficulty.Table(first.Section);
                var category = table?.Categories.FirstOrDefault(
                    c => c.Name.Equals(first.Category, StringComparison.OrdinalIgnoreCase));
                var shipped = category?.Levels.FirstOrDefault(l => l.Level == first.Level);
                if (shipped == null) continue; // Already reported by the validator.

                var original = shipped.RawValue;
                if (string.IsNullOrEmpty(original))
                {
                    error(where, @"the shipped row's original text was not captured, so it cannot be subtracted");
                    continue;
                }

                string replacement;
                try
                {
                    replacement = CategoryRowWriter.Write(shipped, group);
                }
                catch (FormatException e)
                {
                    // Thrown rather than returned, because these are all "the install is not shaped
                    // the way this row was read", which cannot be recovered from by writing something
                    // else - only by not writing.
                    error(where, e.Message);
                    continue;
                }

                if (string.Equals(original, replacement, StringComparison.Ordinal))
                {
                    result.UnchangedEditsSkipped++;
                    continue;
                }

                var asset = inputs.Sections?.AssetOf(table.Section);
                if (string.IsNullOrWhiteSpace(asset))
                {
                    error(where,
                        $@"'{table.Section}' was not found in the merged config, so the coalesced sub-file to write it into is unknown");
                    continue;
                }

                manifest.Coalesced()
                        .File(asset)
                        .Section(table.Section)
                        .Properties.Add(new PropertyWrite
                        {
                            Name = PropertyNameAsShipped(inputs.Sections, table.Section,
                                                         DifficultyProperty(first.Level)),
                            Source = $@"enemy/{first.Category}/level{first.Level}",
                            Replace = new ValueReplacement { Remove = original, Add = replacement },
                        });

                result.EnemyRowsWritten++;
            }
        }

        /// <summary>
        /// Resolves maplist edits into config writes.
        ///
        /// Two shapes, because maps are two lists. Availability is a plain string list, so turning a
        /// map on or off is a bare AddUnique or Remove - no pairing, and nothing to subtract for an
        /// add. Presentation lives in a MasterMapList struct, so editing it is the same exact-text
        /// replacement every other editor emits.
        ///
        /// The removal text for BOTH comes from the install at compile time and never from the
        /// project. For the struct that is the usual rule; for the string list it matters just as much
        /// and is easier to get wrong, because a package name looks like something you could simply
        /// write out - and a value the config happens to store with a space around it would then never
        /// match, leaving the map available and the mod apparently doing nothing.
        /// </summary>
        /// <summary>
        /// Objective edits: rule values on their own sections, and which waves carry an objective.
        ///
        /// The values are ordinary property writes. The section travels IN the edit precisely because
        /// only the catalogue knows whether a property belongs to one variant or to a whole family,
        /// and those are two different sections - deriving it here would be guessing at the one thing
        /// that decides whether the compiled mod does anything at all.
        ///
        /// The waves are not ordinary. OperationWaves is an array the shipped config already writes
        /// twice over, so an Add would leave three entries for one wave and an AddUnique would
        /// suppress nothing - it only matches byte-identical text. The array is rewritten whole
        /// instead: one RemoveProperty, then one AddUnique per surviving entry, which is the same
        /// shape the wavelist arrays use and the only one that ends with the array the user asked for.
        /// </summary>
        /// <summary>
        /// Weapon stat edits: ordinary property overwrites on the weapon's own section.
        ///
        /// Always the weapon's own section, even when the value being changed was inherited. Most of
        /// what a weapon shows is inherited - 8945 values against 3766 of its own on LMP - and
        /// writing back to the ancestor a value came from would change every weapon under it: raising
        /// the Hornet's damage by editing SFXWeapon_SMG_Base raises it for every SMG in the game.
        /// The section travels in the edit for exactly that reason, so this cannot get it wrong by
        /// deriving it.
        /// </summary>
        private static void CompileWeapons(
            ModMakerProject project, CompileInputs inputs, BuildManifest manifest, CompileResult result,
            Action<string, string> error, Action<string, string> warn)
        {
            var edits = WeaponEditDocument.FromJson(project.Document[WeaponEditDocument.CollectionId] as JObject);
            if (edits.Count == 0) return;

            if (inputs.Weapons == null)
            {
                error($@"collections.{WeaponEditDocument.CollectionId}",
                    @"the installed game's weapons were not read, so the edits cannot be resolved");
                return;
            }

            foreach (var problem in WeaponEditValidator.Validate(edits, inputs.Weapons))
            {
                if (problem.Severity == WeaponProblemSeverity.Error) error(problem.Where, problem.Message);
                else warn(problem.Where, problem.Message);
            }

            foreach (var edit in edits.Values.Values.OrderBy(e => e.Section, StringComparer.OrdinalIgnoreCase)
                                                    .ThenBy(e => e.Property, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(edit.Section) || string.IsNullOrWhiteSpace(edit.Property)) continue;

                var asset = inputs.Sections?.AssetOf(edit.Section);
                if (string.IsNullOrWhiteSpace(asset))
                {
                    error($@"weapons.{edit.Section}",
                        $@"'{edit.Section}' was not found in the merged config, so the coalesced sub-file to write it into is unknown");
                    continue;
                }

                manifest.Coalesced().File(asset).Section(edit.Section).Properties.Add(new PropertyWrite
                {
                    Name = edit.Property,
                    Value = edit.Value,
                    // An overwrite. A weapon stat is a scalar, and an Add would leave the shipped
                    // value in place beside the new one.
                    ValueType = CoalesceValue.GetValueType(CoalesceParseAction.New),
                    Source = $@"weapons.{edit.Section}",
                });
                result.WeaponWritesWritten++;
            }
        }

        private static void CompileObjectives(
            ModMakerProject project, CompileInputs inputs, BuildManifest manifest, CompileResult result,
            Action<string, string> error, Action<string, string> warn)
        {
            var edits = ObjectiveEditDocument.FromJson(project.Document[ObjectiveEditDocument.CollectionId] as JObject);
            if (edits.Count == 0) return;

            if (inputs.Objectives == null)
            {
                error($@"collections.{ObjectiveEditDocument.CollectionId}",
                    @"the installed game's objectives were not read, so the edits cannot be resolved");
                return;
            }

            foreach (var problem in ObjectiveEditValidator.Validate(edits, inputs.Objectives))
            {
                if (problem.Severity == ObjectiveProblemSeverity.Error) error(problem.Where, problem.Message);
                else warn(problem.Where, problem.Message);
            }

            foreach (var edit in edits.Values.Values.OrderBy(e => e.Section, StringComparer.OrdinalIgnoreCase)
                                                    .ThenBy(e => e.Property, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(edit.Section) || string.IsNullOrWhiteSpace(edit.Property)) continue;

                var asset = inputs.Sections?.AssetOf(edit.Section);
                if (string.IsNullOrWhiteSpace(asset))
                {
                    error($@"objectives.{edit.Section}",
                        $@"'{edit.Section}' was not found in the merged config, so the coalesced sub-file to write it into is unknown");
                    continue;
                }

                manifest.Coalesced().File(asset).Section(edit.Section).Properties.Add(new PropertyWrite
                {
                    Name = edit.Property,
                    Value = edit.Value,
                    ValueType = Wire(CoalesceParseAction.New),
                    Source = $@"objectives.{edit.Section}",
                });
                result.ObjectiveWritesWritten++;
            }

            CompileObjectiveWaves(edits, inputs, manifest, result, error);
        }

        private static void CompileObjectiveWaves(
            ObjectiveEditDocument edits, CompileInputs inputs, BuildManifest manifest, CompileResult result,
            Action<string, string> error)
        {
            if (edits.Waves.Count == 0) return;

            var section = inputs.Objectives.CoordinatorSection;
            if (string.IsNullOrWhiteSpace(section))
            {
                error(@"objectives.waves", @"the wave coordinator was not found in the installed game");
                return;
            }

            var asset = inputs.Sections?.AssetOf(section);
            if (string.IsNullOrWhiteSpace(asset))
            {
                error(@"objectives.waves",
                    $@"'{section}' was not found in the merged config, so the coalesced sub-file to write it into is unknown");
                return;
            }

            // Built from the SHIPPED entries and edited in place, so a wave nobody touched keeps the
            // text the game wrote it with - including the duplicates, which are not ours to tidy.
            var rebuilt = new List<string>();
            foreach (var wave in inputs.Objectives.Waves)
            {
                var change = edits.Waves.GetValueOrDefault(wave.WaveIndex.ToString(CultureInfo.InvariantCulture));
                if (change is { Enabled: false }) continue;

                if (change == null || SameScale(change.CreditScale, wave.CreditScale))
                {
                    rebuilt.Add(wave.Raw);
                    continue;
                }

                rebuilt.Add(WaveEntry(wave.WaveNumber, change.CreditScale));
            }

            // A wave the config does not name is an objective wave being ADDED, which is a feature
            // rather than an error - the validator warns about it rather than refusing it.
            foreach (var (index, change) in edits.Waves)
            {
                if (!change.Enabled) continue;
                if (!int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out var waveIndex)) continue;
                if (inputs.Objectives.Waves.Any(w => w.WaveIndex == waveIndex)) continue;

                // Back to the config's own zero-based numbering. The one place that is undone.
                rebuilt.Add(WaveEntry(waveIndex - 1, change.CreditScale));
            }

            // In wave order, so an added wave lands where a reader expects it rather than after the
            // extraction. A STABLE sort: entries for the same wave keep the order the config had
            // them in, which is the order that decides which one the game reads first.
            rebuilt = rebuilt
                .Select((text, position) => (text, position))
                .OrderBy(e => WaveNumberOf(e.text))
                .ThenBy(e => e.position)
                .Select(e => e.text)
                .ToList();

            var file = manifest.Coalesced().File(asset);
            file.Section(section).Properties.Add(new PropertyWrite
            {
                Name = ObjectiveCatalogue.OperationWavesProperty,
                Value = string.Empty,
                ValueType = Wire(CoalesceParseAction.RemoveProperty),
                Source = @"objectives.waves",
            });

            foreach (var entry in rebuilt)
            {
                file.Section(section).Properties.Add(new PropertyWrite
                {
                    Name = ObjectiveCatalogue.OperationWavesProperty,
                    Value = entry,
                    // AddUnique, matching the DLC convention the wavelist arrays follow. The clear
                    // above is what makes it safe: nothing can duplicate against an empty property.
                    ValueType = Wire(CoalesceParseAction.AddUnique),
                    Source = @"objectives.waves",
                });
                result.ObjectiveWritesWritten++;
            }
        }

        /// <summary>
        /// The WaveNumber an entry names, for ordering. Entries that cannot be read sort last rather
        /// than throwing - they are still written, because they are the game's own text.
        /// </summary>
        private static int WaveNumberOf(string entry)
        {
            if (!ConfigStructReader.TryReadStructRaw(entry, out var map)) return int.MaxValue;
            var text = map.GetValueOrDefault(@"WaveNumber")?.Trim();
            if (string.IsNullOrEmpty(text)) return int.MaxValue;
            var n = ValueTreeBuilder.NumericPrefixLength(text);
            return n > 0 && int.TryParse(text.AsSpan(0, n), NumberStyles.Integer, CultureInfo.InvariantCulture,
                                         out var value)
                ? value
                : int.MaxValue;
        }

        private static string WaveEntry(int waveNumber, double? creditScale) =>
            creditScale is { } scale
                ? $@"(WaveNumber={waveNumber.ToString(CultureInfo.InvariantCulture)}, CreditScale={RenderScale(scale)})"
                : $@"(WaveNumber={waveNumber.ToString(CultureInfo.InvariantCulture)})";

        private static string RenderScale(double value)
        {
            var text = value.ToString(@"R", CultureInfo.InvariantCulture);
            return text.Contains('.') || text.Contains('E') ? text : text + @".0";
        }

        private static bool SameScale(double? a, double? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return Math.Abs(a.Value - b.Value) < 1e-9;
        }

        private static void CompileMaplist(
            ModMakerProject project, CompileInputs inputs, BuildManifest manifest, CompileResult result,
            Action<string, string> error, Action<string, string> warn)
        {
            var edits = project.MaplistEdits;

            // The faction toggles are edited from the wavelist page and stored in its collection, but
            // they are still a write to the match settings section - so a project that touched no map
            // at all can still have work to do here, and this is where they are checked as well as
            // written.
            var pickers = project.WavelistEdits.Pickers;
            if (edits.Count == 0 && pickers.Count == 0) return;

            if (inputs.Maps == null)
            {
                error($@"collections.{MaplistEditDocument.CollectionId}",
                    @"the installed game's map roster was not read, so the edits cannot be resolved");
                return;
            }

            foreach (var problem in MaplistEditValidator.Validate(edits, inputs.Maps)
                         .Concat(MaplistEditValidator.ValidateFactionToggles(pickers, inputs.Maps)))
            {
                if (problem.Severity == MaplistProblemSeverity.Error) error(problem.Where, problem.Message);
                else warn(problem.Where, problem.Message);
            }

            var section = inputs.Maps.SettingsSection;
            if (string.IsNullOrWhiteSpace(section))
            {
                error(@"collections.maplist", @"the match settings section was not found in the installed game");
                return;
            }

            var asset = inputs.Sections?.AssetOf(section);
            if (string.IsNullOrWhiteSpace(asset))
            {
                error(@"collections.maplist",
                    $@"'{section}' was not found in the merged config, so the coalesced sub-file to write it into is unknown");
                return;
            }

            var file = manifest.Coalesced().File(asset);
            CompileAvailability(edits, inputs, file, section, result);
            CompileFactions(edits, pickers, inputs, file, section, result);
            CompileMapEntries(edits, inputs, file, section, result, error);
        }

        private static void CompileAvailability(
            MaplistEditDocument edits, CompileInputs inputs, CoalescedFile file, string section,
            CompileResult result)
        {
            foreach (var edit in edits.Availability)
            {
                if (string.IsNullOrWhiteSpace(edit.Package)) continue; // Already reported.

                // CoalesceValue is a STRUCT, so FirstOrDefault() hands back a default instance
                // rather than null and `!= null` is always true. WavelistScanner already carries a
                // note about this; the consequence here would have been an availability edit that
                // subtracted an empty string - a mod that installs and changes nothing.
                var matches = inputs.Sections?.Values(section, AvailableMapsProperty)
                                    .Where(v => string.Equals(v.Value?.Trim(), edit.Package,
                                                              StringComparison.OrdinalIgnoreCase))
                                    .ToList() ?? [];
                var shipped = matches.Count > 0 ? matches[0] : (CoalesceValue?)null;

                if (edit.Available)
                {
                    // Already available: an AddUnique here would be a no-op at best, and given the
                    // library's known AddUnique bug it would be a duplicate at worst.
                    if (shipped != null) { result.UnchangedEditsSkipped++; continue; }

                    file.Section(section).Properties.Add(new PropertyWrite
                    {
                        Name = PropertyNameAsShipped(inputs.Sections, section, AvailableMapsProperty),
                        Value = edit.Package,
                        ValueType = Wire(CoalesceParseAction.AddUnique),
                        Source = $@"maplist/available/{edit.Package}",
                    });
                    result.MaplistWritesWritten++;
                    continue;
                }

                // Not there to begin with, so there is nothing to subtract.
                if (shipped == null) { result.UnchangedEditsSkipped++; continue; }

                file.Section(section).Properties.Add(new PropertyWrite
                {
                    Name = PropertyNameAsShipped(inputs.Sections, section, AvailableMapsProperty),
                    // The install's own bytes, not the package name as the project spells it.
                    Value = shipped.Value.Value,
                    ValueType = Wire(CoalesceParseAction.Remove),
                    Source = $@"maplist/unavailable/{edit.Package}",
                });
                result.MaplistWritesWritten++;
            }
        }

        /// <summary>
        /// Match-settings choices turned off in the lobby.
        ///
        /// `EnemyTypes` and `ChallengeTypes` are lists of structs and a choice is offered exactly when
        /// its entry is in one, so this subtracts the entry rather than setting a flag - the same
        /// shape as <see cref="CompileAvailability"/>, one list up. Factions and difficulties are the
        /// same operation on different properties, which is why they are one path and not two.
        ///
        /// Removal matches on EXACT VALUE TEXT, which is why the install's own bytes are written back
        /// rather than the struct rebuilt from what the catalogue parsed out of it: a rebuilt entry
        /// that differs by a space matches nothing and the choice stays in the picker.
        /// </summary>
        private static void CompileFactions(
            MaplistEditDocument edits, IReadOnlyCollection<PickerOptionEdit> factions, CompileInputs inputs,
            CoalescedFile file, string section, CompileResult result)
        {
            foreach (var picker in MatchPickers.All)
            {
                var property = PickerProperty(picker);
                var offered = MaplistEditValidator.OptionsOf(inputs.Maps, picker);

                // Two homes, one write. The factions are edited from the wavelist page and stored in
                // its collection; the difficulties are stored here. A project written before that
                // move still carries factions here, so the two lists are JOINED rather than chosen
                // between, and an id named in both is written once.
                var toggles = edits.For(picker).ToList();
                if (picker == MatchPickers.Factions)
                    toggles = factions.Where(e => string.Equals(e.Picker, picker, StringComparison.OrdinalIgnoreCase))
                                      .Concat(toggles).DistinctBy(e => e.Key).ToList();

                foreach (var edit in toggles)
                {
                    if (edit.Id == null) continue; // Already reported.

                    var shipped = offered.FirstOrDefault(o => o.Id == edit.Id);
                    if (shipped?.RawValue is not { Length: > 0 } raw || edit.Enabled)
                    {
                        // Nothing to subtract, or nothing to put back - being in the picker IS being
                        // enabled, and the catalogue only has this entry because the picker has it.
                        // Counted rather than dropped, so the build says what it skipped.
                        result.UnchangedEditsSkipped++;
                        continue;
                    }

                    file.Section(section).Properties.Add(new PropertyWrite
                    {
                        Name = PropertyNameAsShipped(inputs.Sections, section, property),
                        Value = raw,
                        ValueType = Wire(CoalesceParseAction.Remove),
                        Source = $@"maplist/{picker}-off/{edit.Id}",
                    });
                    result.MaplistWritesWritten++;
                }
            }
        }

        /// <summary>The config property behind one of ModMaker's picker names.</summary>
        private static string PickerProperty(string picker) =>
            string.Equals(picker, MatchPickers.Factions, StringComparison.OrdinalIgnoreCase)
                ? EnemyTypesProperty
                : ChallengeTypesProperty;

        private static void CompileMapEntries(
            MaplistEditDocument edits, CompileInputs inputs, CoalescedFile file, string section,
            CompileResult result, Action<string, string> error)
        {
            foreach (var edit in edits.Entries)
            {
                var where = edit.Package ?? $@"map {edit.Id}";

                var shipped = (edit.Id != null ? inputs.Maps.Maps.FirstOrDefault(m => m.Id == edit.Id) : null)
                              ?? inputs.Maps.Maps.FirstOrDefault(m => m.PackageName != null
                                    && m.PackageName.Equals(edit.Package, StringComparison.OrdinalIgnoreCase));
                if (shipped == null || shipped.IsPlaceholder) continue; // Already reported.

                var original = shipped.RawValue;
                if (string.IsNullOrEmpty(original))
                {
                    error(where, @"the shipped entry's original text was not captured, so it cannot be subtracted");
                    continue;
                }

                string replacement;
                try
                {
                    replacement = MapEntryWriter.Write(shipped, edit);
                }
                catch (FormatException e)
                {
                    error(where, e.Message);
                    continue;
                }

                if (string.Equals(original, replacement, StringComparison.Ordinal))
                {
                    result.UnchangedEditsSkipped++;
                    continue;
                }

                file.Section(section).Properties.Add(new PropertyWrite
                {
                    Name = PropertyNameAsShipped(inputs.Sections, section, MasterMapListProperty),
                    Source = $@"maplist/entry/{edit.Id?.ToString(CultureInfo.InvariantCulture) ?? edit.Package}",
                    Replace = new ValueReplacement { Remove = original, Add = replacement },
                });
                result.MaplistWritesWritten++;
            }
        }

        /// <summary>
        /// The property name spelled as the installed config spells it.
        ///
        /// Config lookups are case-insensitive, so this changes nothing the game does - but a
        /// generated file that reads like the one it merges over is far easier to diff against by
        /// hand, and hand-diffing is how an exact-text subtraction gets debugged.
        /// </summary>
        private static string PropertyNameAsShipped(SectionIndex index, string sectionKey, string property)
        {
            if (index?.Find(sectionKey) is not { } section) return property;
            foreach (var candidate in section)
            {
                if (candidate.Key.Equals(property, StringComparison.OrdinalIgnoreCase)) return candidate.Key;
            }
            return property;
        }
    }
}
