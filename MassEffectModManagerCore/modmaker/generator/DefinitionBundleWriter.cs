using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>Everything the writer needs. Assembled by the caller so each scan stays independent.</summary>
    public class BundleInputs
    {
        public string BundleVersion { get; init; }
        public string Game { get; init; }
        public string TargetMilanVersion { get; init; }
        public CatalogueEnvironment Environment { get; init; }
        public IReadOnlyDictionary<string, CatalogueClass> Classes { get; init; }
        public InstanceCatalogue Instances { get; init; }
        public WavelistCatalogue Waves { get; init; }
        public DifficultyCatalogue Difficulty { get; init; }
        public MapListCatalogue Maps { get; init; }
        public MergedGameConfig Config { get; init; }
        public TextLayer Text { get; init; }

        /// <summary>
        /// Resolves a class to the game's own name for it, when the name scan has run, along with
        /// WHICH source found it - the TLK display name or the store card table. The schema records
        /// that distinction, so the caller has to supply it rather than have it guessed.
        /// </summary>
        public Func<CatalogueClass, (string Name, string Source)> GameName { get; init; }
    }

    /// <summary>What the writer produced, and what it could not.</summary>
    public class BundleBuildResult
    {
        public JObject Bundle { get; init; }
        public List<string> Warnings { get; } = new();

        public int ClassesEmitted;
        public int ClassesExcluded;

        /// <summary>
        /// Classes curation excluded that are in the bundle anyway, because an included class descends
        /// from them. They are model, not navigation: without them the child loses every inherited
        /// property, silently.
        /// </summary>
        public int ClassesEmittedAsAncestorOnly;
        public int ClassesUnrecognised;
        public int PropertiesEmitted;

        /// <summary>
        /// Struct or array properties the shipped config never sets, so there is no text to build a
        /// value tree from. Counted rather than emitted empty - an empty struct renders as a page of
        /// controls that edit nothing.
        /// </summary>
        public int PropertiesWithoutShippedValue;

        /// <summary>Editable leaves inside value trees.</summary>
        public int ValueTreeLeaves;

        /// <summary>
        /// Unquoted tokens preserved verbatim inside value trees - enum values, object paths, formula
        /// names. Not editable, because nothing here knows which values are legal for them.
        /// </summary>
        public int ValueTreeLiterals;

        /// <summary>Values that hit the nesting guard. Should be zero.</summary>
        public int ValueTreesTooDeep;

        /// <summary>
        /// Scalar properties whose default is the type's zero value, because neither the merged config
        /// nor any class in the chain sets them.
        ///
        /// Not a gap: that IS what the game uses. A UnrealScript default object serialises only what
        /// differs from the type's zero, so absence is the value rather than the absence of one.
        /// Counted because it is worth knowing how much of the surface is untouched by shipped data.
        /// </summary>
        public int PropertiesDefaultedToTypeZero;

        /// <summary>
        /// Scalars whose shipped text does not parse as their declared type - "0.05;" on a float.
        ///
        /// A data bug in the config rather than a condition to live with, so each one is named in
        /// <see cref="Warnings"/> as well as counted. The value is carried through as text: the game
        /// has what it has, and substituting a number here would hide the line that needs fixing.
        /// </summary>
        public int PropertiesWithUnparseableValue;

        /// <summary>How many properties had a compiled class default available.</summary>
        public int PropertiesWithCompiledDefault;
    }

    /// <summary>
    /// Serialises the generated catalogue plus the shipped text layer into a definition bundle.
    ///
    /// Built as JSON rather than as a mirrored C# object model on purpose: `definitions.schema.json`
    /// is the contract between generator, editor and compiler, and a parallel set of DTOs would be a
    /// second copy of it that drifts silently. The bundle is validated against the schema in the test
    /// suite instead, which is the only check that actually proves the contract is met.
    /// </summary>
    public static class DefinitionBundleWriter
    {
        public const int Format = 2;

        /// <summary>Property types whose value is a single editable leaf.</summary>
        private static readonly Dictionary<string, string> ScalarKinds = new(StringComparer.OrdinalIgnoreCase)
        {
            [@"FloatProperty"] = @"float",
            [@"IntProperty"] = @"int",
            [@"BoolProperty"] = @"bool",
            [@"ByteProperty"] = @"int",
            [@"StrProperty"] = @"string",
            [@"NameProperty"] = @"string",
            [@"StringRefProperty"] = @"int",
        };

        public static BundleBuildResult Build(BundleInputs inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (inputs.Classes == null) throw new ArgumentNullException(nameof(inputs.Classes));

            var join = new CatalogueJoin(inputs.Text);
            var result = new BundleBuildResult
            {
                Bundle = new JObject
                {
                    [@"format"] = Format,
                    [@"bundleVersion"] = inputs.BundleVersion ?? @"0.0.0",
                    [@"game"] = inputs.Game ?? @"LE3",
                },
            };

            if (!string.IsNullOrWhiteSpace(inputs.TargetMilanVersion))
                result.Bundle[@"targetMilanVersion"] = inputs.TargetMilanVersion;

            if (inputs.Environment != null)
                result.Bundle[@"environment"] = JObject.FromObject(inputs.Environment);

            // Section keys once, not once per class: whether a class has a class-path section at all
            // is asked for every class, and a per-object class legitimately has none.
            var sectionKeys = inputs.Config == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : inputs.Config.Sections().Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var classes = new JArray();
            var properties = new JArray();
            var groups = BuildGroups(inputs);

            // Curation decides what becomes a PAGE; it does not decide what exists. An excluded class
            // that an included class descends from still has to be in the bundle, or the chain the
            // editor walks is broken and the child silently loses every property it inherits - which
            // is most of them. SFXCustomAction is excluded wholesale and one of its subclasses is
            // included by name, so this is a real case rather than a hypothetical one.
            var pages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cls in inputs.Classes.Values)
            {
                if (join.ResolveCuration(cls).Decision != CurationDecision.Exclude) pages.Add(cls.Section);
            }

            var needed = new HashSet<string>(pages, StringComparer.OrdinalIgnoreCase);
            foreach (var section in pages)
            {
                foreach (var ancestor in ClassScanner.Chain(section, inputs.Classes).Skip(1))
                    needed.Add(ancestor.Section);
            }
            result.ClassesEmittedAsAncestorOnly = needed.Count - pages.Count;

            foreach (var cls in inputs.Classes.Values.OrderBy(c => c.Section, StringComparer.OrdinalIgnoreCase))
            {
                var curation = join.ResolveCuration(cls);
                bool isPage = curation.Decision != CurationDecision.Exclude;
                if (!isPage && !needed.Contains(cls.Section)) { result.ClassesExcluded++; continue; }
                if (isPage && !curation.Recognised) result.ClassesUnrecognised++;

                classes.Add(BuildClass(cls, curation, join, inputs, sectionKeys));
                result.ClassesEmitted++;

                // A class owned by a structured editor contributes no form properties: its content is
                // a collection with a bespoke editor, and emitting both would give the user two places
                // to edit the same value.
                if (curation.Decision == CurationDecision.Editor) continue;

                // An excluded class kept only so its descendants resolve still contributes the
                // properties they inherit. Skipping them here would defeat the reason it was kept.

                foreach (var (prop, declaredBy) in ClassScanner.EffectiveProperties(cls.Section, inputs.Classes))
                {
                    // Only properties DECLARED here become entries; inherited ones belong to the
                    // ancestor that declares them and cascade from there.
                    if (!declaredBy.Equals(cls.Section, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!curation.AllowsProperty(prop.Name)) continue;

                    var text = join.ResolveProperty(cls.Section, prop.Name);
                    if (text.Hidden) continue;

                    if (ScalarKinds.TryGetValue(prop.DeclaredType ?? string.Empty, out var kind))
                    {
                        properties.Add(BuildProperty(cls, prop, kind, text, inputs, result));
                        result.PropertiesEmitted++;
                        continue;
                    }

                    var structured = BuildStructuredProperty(cls, prop, text, inputs, result);
                    if (structured != null)
                    {
                        properties.Add(structured);
                        result.PropertiesEmitted++;
                    }
                }
            }

            result.Bundle[@"groups"] = groups;
            result.Bundle[@"classes"] = classes;
            result.Bundle[@"properties"] = properties;

            var descriptions = BuildDescriptions(inputs);
            if (descriptions.Count > 0) result.Bundle[@"descriptions"] = descriptions;

            var dimensions = BuildDimensions(inputs);
            if (dimensions.Count > 0) result.Bundle[@"dimensions"] = dimensions;

            var collections = BuildCollections(inputs, join);
            if (collections.Count > 0) result.Bundle[@"collections"] = collections;

            result.Warnings.AddRange(join.Warnings);
            if (result.PropertiesWithoutShippedValue > 0)
                result.Warnings.Add($@"{result.PropertiesWithoutShippedValue} struct or array properties are not in the bundle: the shipped config never sets them, so there is no value to build a tree from.");
            if (result.ValueTreesTooDeep > 0)
                result.Warnings.Add($@"{result.ValueTreesTooDeep} values nested deeper than the guard allows and were kept as literals.");


            return result;
        }

        // ------------------------------------------------------------------ pieces

        private static JObject BuildClass(CatalogueClass cls, ResolvedCuration curation, CatalogueJoin join,
                                         BundleInputs inputs, HashSet<string> sectionKeys)
        {
            var title = join.ResolveTitle(cls, inputs.GameName);

            var entry = new JObject
            {
                [@"id"] = ClassId(cls),
                // Null, not omitted: a per-object class can legitimately have NO class-path section,
                // and the schema distinguishes "no shared section" from "section unknown".
                [@"section"] = sectionKeys.Contains(cls.Section) ? cls.Section : JValue.CreateNull(),
                [@"title"] = title.Title,
                [@"titleSource"] = title.TitleSource,
                // Always written, never omitted. An editor showing three and a half thousand classes
                // needs somewhere to file each one, and a class the curation does not name is filed
                // under "unrecognised" rather than left for the page to invent a home for.
                [@"group"] = curation.Group ?? CatalogueJoin.UnrecognisedGroup,
                [@"recognised"] = curation.Recognised,
            };

            if (cls.SuperClass != null && inputs.Classes.ContainsKey(cls.SuperClass))
                entry[@"extends"] = ClassId(inputs.Classes[cls.SuperClass]);

            if (cls.ConfigFile != null) entry[@"file"] = cls.ConfigFile;

            if (inputs.Instances != null && inputs.Instances.BySection.TryGetValue(cls.Section, out var perObject))
            {
                var instances = new JArray();
                foreach (var inst in perObject.Instances)
                {
                    instances.Add(new JObject
                    {
                        [@"id"] = inst.Id,
                        [@"order"] = inst.Order,
                    });
                }

                entry[@"perObject"] = new JObject
                {
                    [@"classToken"] = perObject.ClassToken,
                    [@"instances"] = instances,
                    // A config section does not create an object: the game has to construct something
                    // with that name for the section to be read at all. So creating instances is
                    // authored per class, never inferred.
                    [@"canCreate"] = false,
                };
            }

            return entry;
        }

        private static JObject BuildProperty(
            CatalogueClass cls, CatalogueProperty prop, string kind, ResolvedText text,
            BundleInputs inputs, BundleBuildResult result)
        {
            var leaf = new JObject
            {
                [@"kind"] = kind,
                [@"id"] = prop.Name.ToLowerInvariant(),
            };

            // A TLK reference is an int and is edited as one, so the KIND stays "int" - the compiler
            // writes it the same way either way. This flag is the separate fact that the number means
            // something, which is what lets the editor show the words behind it.
            //
            // Top-level properties only. String refs also nest inside structs (docs/08 §"String refs
            // nest"), and a struct's field types are not in the class scan at all - a value tree is
            // built from config TEXT, where 348159 and 348159 are indistinguishable. Marking those
            // would mean guessing from field names, and a wrong guess here shows a real TLK string
            // beside a number that is not a string ref.
            if (string.Equals(prop.DeclaredType, @"StringRefProperty", StringComparison.OrdinalIgnoreCase))
                leaf[@"stringref"] = true;

            if (!string.IsNullOrWhiteSpace(text.Label)) leaf[@"label"] = text.Label;
            if (!string.IsNullOrWhiteSpace(text.Unit)) leaf[@"unit"] = text.Unit;
            if (!string.IsNullOrWhiteSpace(text.Description)) leaf[@"description"] = text.Description;

            // Rule 1: config REPLACES the compiled default, it does not merge with it. So the value the
            // game ships - and the one "revert" has to return to - is the config's where it sets the
            // property and the compiled default otherwise. Never a combination, and never the compiled
            // default when config has spoken.
            //
            // And when NEITHER sets it, the value is not unknown: UnrealScript gives the property its
            // type's zero value, and a default object serialises only what differs from that. So an
            // absent default IS the value, and omitting the field would report "we don't know" for a
            // property whose shipped value is precisely 0. Every scalar therefore has a default.
            var shipped = ShippedValue(cls.Section, prop.Name, inputs);
            var compiled = ClassDefaults.Effective(cls.Section, prop.Name, inputs.Classes);

            if (shipped != null) leaf[@"default"] = Note(Coerce(shipped, kind, out var why), why);
            else if (compiled != null) leaf[@"default"] = Note(Coerce(compiled, kind, out var whyc), whyc);
            else
            {
                leaf[@"default"] = ZeroValue(prop.DeclaredType, kind);
                result.PropertiesDefaultedToTypeZero++;
            }

            if (compiled != null) result.PropertiesWithCompiledDefault++;

            JToken Note(JToken value, string problem)
            {
                if (problem == null) return value;

                // Named, not just counted: each of these is one config line, and the only way to act
                // on it is to be told which one.
                result.Warnings.Add($@"{ClassId(cls)}.{prop.Name} is a {kind}: {problem}.");
                result.PropertiesWithUnparseableValue++;
                return value;
            }

            var entry = new JObject
            {
                [@"id"] = $@"{ClassId(cls)}.{prop.Name.ToLowerInvariant()}",
                [@"class"] = ClassId(cls),
                [@"name"] = prop.Name,
                [@"value"] = leaf,
            };

            if (cls.ConfigFile != null) entry[@"file"] = cls.ConfigFile;

            // NOTE: the schema's `valueType` is the coalesced parse action (New / Add / AddUnique /
            // Remove), not the UnrealScript property type. It is left at its default here; the
            // property's declared type is expressed through the leaf's `kind`.

            // Scope is only meaningful for a class configured per object, and it decides which section
            // the compiler writes to. Emitting it for an ordinary class would be noise; getting it
            // wrong for a per-object one produces a mod that builds, installs and does nothing.
            if (inputs.Instances != null && inputs.Instances.BySection.ContainsKey(cls.Section))
            {
                var scope = inputs.Instances.ScopeOf(cls.Section, prop.Name, inputs.Classes);
                entry[@"scope"] = scope.Scope switch
                {
                    ConfigScope.Instance => @"instance",
                    ConfigScope.Shared => @"shared",
                    _ => @"unverified",
                };
            }

            // NOTE: `originalValue` is deliberately NOT emitted here. It carries the verbatim config
            // text for an ARRAY property, where a clean override is an exact-text Remove followed by
            // an Add and any reserialisation breaks the match silently. A scalar is overwritten
            // outright, so the field would be meaningless on one - and a meaningless field invites
            // the compiler to match against it.
            return entry;
        }

        /// <summary>
        /// A struct or array property, as a value tree.
        ///
        /// Built from the shipped config text, because that is the only place a struct's value can be
        /// read: <see cref="ClassDefaults"/> reads scalars only, and a compiled struct default would
        /// have to be stringified back into config syntax to be comparable - a reserialisation this
        /// design avoids everywhere else precisely because it does not round-trip.
        ///
        /// So a struct property the config never sets has no tree, and is counted rather than emitted
        /// empty: an empty struct in the bundle would render as a page with controls on it that edit
        /// nothing.
        /// </summary>
        private static JObject BuildStructuredProperty(
            CatalogueClass cls, CatalogueProperty prop, ResolvedText text, BundleInputs inputs, BundleBuildResult result)
        {
            var values = inputs.Config?.LiveValues(cls.Section, prop.Name).Select(v => v.Value).ToList()
                         ?? new List<string>();
            if (values.Count == 0)
            {
                result.PropertiesWithoutShippedValue++;
                return null;
            }

            var idBase = prop.Name.ToLowerInvariant();
            var stats = new ValueTreeBuilder.BuildStats();

            JObject value;
            if (values.Count == 1)
            {
                value = ValueTreeBuilder.Build(values[0], idBase, stats);
            }
            else
            {
                // The property is set several times, which in coalesced terms IS an array: each entry
                // is one element. Represented as one, with positional ids.
                var items = new JArray();
                for (int i = 0; i < values.Count; i++)
                    items.Add(ValueTreeBuilder.Build(values[i], $@"{idBase}.{i}", stats));
                value = new JObject { [@"kind"] = @"array", [@"items"] = items };
            }

            result.ValueTreeLeaves += stats.Leaves;
            result.ValueTreeLiterals += stats.Literals;
            if (stats.TooDeep > 0) result.ValueTreesTooDeep += stats.TooDeep;

            var entry = new JObject
            {
                [@"id"] = $@"{ClassId(cls)}.{idBase}",
                [@"class"] = ClassId(cls),
                [@"name"] = prop.Name,
                [@"value"] = value,
            };

            if (cls.ConfigFile != null) entry[@"file"] = cls.ConfigFile;

            // An array edit REPLACES the whole array rather than splicing into it, so the compiler has
            // to emit every entry - including the ones the user never touched. Those must come back
            // out byte-for-byte as they shipped: regenerating them from the parsed tree would rewrite
            // float renderings, spacing and member order across the entire array, changing values
            // nobody edited. So every entry's verbatim text is carried.
            if (values.Count > 1
                || (prop.DeclaredType != null && prop.DeclaredType.Equals(@"ArrayProperty", StringComparison.OrdinalIgnoreCase)))
            {
                if (prop.DeclaredType != null) entry[@"arrayType"] = prop.DeclaredType;
                entry[@"originalValues"] = new JArray(values);
            }
            else
            {
                entry[@"originalValue"] = values[0];
            }

            if (inputs.Instances != null && inputs.Instances.BySection.ContainsKey(cls.Section))
            {
                var scope = inputs.Instances.ScopeOf(cls.Section, prop.Name, inputs.Classes);
                entry[@"scope"] = scope.Scope switch
                {
                    ConfigScope.Instance => @"instance",
                    ConfigScope.Shared => @"shared",
                    _ => @"unverified",
                };
            }

            return entry;
        }

        private static JArray BuildGroups(BundleInputs inputs)
        {
            var groups = new JArray();
            foreach (var g in inputs.Text?.Groups ?? new List<TextGroup>())
            {
                var entry = new JObject { [@"id"] = g.Id, [@"title"] = g.Title };
                if (!string.IsNullOrWhiteSpace(g.Description)) entry[@"blurb"] = g.Description;
                if (!string.IsNullOrWhiteSpace(g.Parent)) entry[@"parent"] = g.Parent;
                if (g.Order != 0) entry[@"order"] = g.Order;
                groups.Add(entry);
            }

            // The schema requires at least the catch-all to exist, and so does the join: unrecognised
            // content is grouped there rather than dropped, so it cannot be allowed to go missing just
            // because a text layer forgot it.
            if (!groups.Any(g => string.Equals((string)g[@"id"], CatalogueJoin.UnrecognisedGroup, StringComparison.OrdinalIgnoreCase)))
            {
                groups.Add(new JObject
                {
                    [@"id"] = CatalogueJoin.UnrecognisedGroup,
                    [@"title"] = @"Not Recognised",
                    [@"blurb"] = @"Found in your install but unknown to this version of ModMaker - usually content from another mod. Editable, but with no descriptions.",
                    [@"order"] = 900,
                });
            }

            return groups;
        }

        private static JObject BuildDescriptions(BundleInputs inputs)
        {
            var descriptions = new JObject();
            foreach (var (key, entry) in inputs.Text?.Descriptions ?? new Dictionary<string, DescriptionEntry>())
            {
                var o = new JObject();
                if (!string.IsNullOrWhiteSpace(entry.Label)) o[@"label"] = entry.Label;
                if (!string.IsNullOrWhiteSpace(entry.Unit)) o[@"unit"] = entry.Unit;
                if (!string.IsNullOrWhiteSpace(entry.Text)) o[@"text"] = entry.Text;
                descriptions[key] = o;
            }
            return descriptions;
        }

        /// <summary>
        /// The difficulty axis, read from the game's own difficulty levels rather than written down.
        ///
        /// Four today. The count is current data, not a constant - the singleplayer handler ships five
        /// inside the same game, and Milan may add one - so nothing counts to four.
        /// </summary>
        private static JObject BuildDimensions(BundleInputs inputs)
        {
            var dimensions = new JObject();
            var mp = inputs.Difficulty?.Table(@"SFXDifficultyHandlerMP");
            if (mp == null || mp.Levels.Count == 0) return dimensions;

            var values = new JArray();
            for (int i = 0; i < mp.Levels.Count; i++)
            {
                var level = mp.Levels[i];
                // The challenge banners are positional against DO_Level<n>: ID + 1 = level. That is a
                // convention declared nowhere in the game, so it is applied by index and only where
                // there is a banner to apply.
                var banner = inputs.Maps?.ChallengeTypes.FirstOrDefault(c => c.Id == level - 1);
                var value = new JObject
                {
                    [@"key"] = $@"level{level}",
                    [@"label"] = $@"Level {level}",
                };
                if (banner?.Image != null) value[@"image"] = banner.Image;
                values.Add(value);
            }

            dimensions[@"difficulty"] = new JObject { [@"label"] = @"Difficulty", [@"values"] = values };
            return dimensions;
        }

        /// <summary>
        /// The structured editors. Their payloads are editor-defined and large, so the bundle names
        /// them and the editors read the catalogues directly; what matters here is that a class marked
        /// "editor" by curation has somewhere to send the user.
        /// </summary>
        private static JArray BuildCollections(BundleInputs inputs, CatalogueJoin join)
        {
            var collections = new JArray();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var cls in inputs.Classes.Values)
            {
                var curation = join.ResolveCuration(cls);
                if (curation.Decision != CurationDecision.Editor) continue;
                if (string.IsNullOrWhiteSpace(curation.Editor)) continue;
                if (!seen.Add(curation.Editor)) continue;

                collections.Add(new JObject
                {
                    [@"id"] = curation.Editor,
                    [@"editor"] = curation.Editor,
                    [@"group"] = curation.Group ?? CatalogueJoin.UnrecognisedGroup,
                });
            }

            return collections;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// The lowercase SECTION, not the bare class name.
        ///
        /// A bare name is not unique - the same class name is cooked into several packages, and two
        /// genuinely different classes can share one. Project keys are "&lt;classId&gt;/&lt;leafId&gt;",
        /// so a collision makes two pages write to the same saved value. Deriving the id from the
        /// section instead is unique by construction and, more importantly, STABLE: an id that were
        /// only unique today would change the moment a mod introduced a clash, silently invalidating
        /// every project keyed against it. The schema documents this as the normal choice.
        /// </summary>
        private static string ClassId(CatalogueClass cls) => cls.Section.ToLowerInvariant();

        private static string ShippedValue(string section, string property, BundleInputs inputs)
        {
            if (inputs.Config == null) return null;
            var values = inputs.Config.LiveValues(section, property).ToList();
            // A property set more than once is an array, not a scalar; taking the first would present
            // one element as if it were the whole value.
            return values.Count == 1 ? values[0].Value : null;
        }

        /// <summary>
        /// The value UnrealScript gives a property that nothing sets.
        ///
        /// Keyed on the declared type rather than on the leaf kind, because the two disagree in one
        /// place that matters: a NameProperty's zero is the name "None", not an empty string, and
        /// emitting "" there would write a name the game does not have.
        /// </summary>
        private static JToken ZeroValue(string declaredType, string kind)
        {
            if (string.Equals(declaredType, @"NameProperty", StringComparison.OrdinalIgnoreCase))
                return new JValue(@"None");

            return kind switch
            {
                @"float" => new JValue(0.0),
                @"int" => new JValue(0),
                @"bool" => new JValue(false),
                _ => new JValue(string.Empty),
            };
        }

        private static JToken Coerce(string text, string kind) => Coerce(text, kind, out _);

        /// <summary>
        /// The number at the start of a config value, or null when it does not start with one.
        ///
        /// Unreal's own config readers are prefix parsers - appAtof("0.05;") is 0.05, and the float
        /// suffix in "0.5f" simply ends the number - so a value with trailing text is a value the GAME
        /// reads as a number. The length comes from <see cref="ValueTreeBuilder.NumericPrefixLength"/>
        /// so this and the value tree cannot disagree about what counts as one.
        /// </summary>
        private static double? LeadingNumber(string text)
        {
            var n = ValueTreeBuilder.NumericPrefixLength(text);
            if (n == 0) return null;

            return double.TryParse(text.AsSpan(0, n), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : null;
        }

        private static JToken Coerce(string text, string kind, out string problem)
        {
            problem = null;
            var t = text?.Trim();
            if (string.IsNullOrEmpty(t)) return JValue.CreateNull();

            switch (kind)
            {
                case @"float":
                    if (DifficultyCatalogue.ParseDouble(t) is { } f) return new JValue(f);
                    if (LeadingNumber(t) is { } lf)
                    {
                        problem = $@"the game's value is '{t}'; everything after the number is ignored, as the game ignores it";
                        return new JValue(lf);
                    }
                    problem = $@"the game's value is '{t}', which is not a number";
                    return new JValue(t);

                case @"int":
                    if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                        return new JValue(i);
                    if (LeadingNumber(t) is { } li)
                    {
                        problem = $@"the game's value is '{t}'; everything after the number is ignored, as the game ignores it";
                        return new JValue((int)li);
                    }
                    // An enum-backed ByteProperty lands here, and it is not a typo: the config writes
                    // the enum's NAME. Reported rather than mangled - a leaf like this wants `kind`
                    // "enum" with the declared type's options, which is separate work.
                    problem = $@"the game's value is '{t}', which is not a number";
                    return new JValue(t);

                case @"bool":
                    if (bool.TryParse(t, out var b)) return new JValue(b);
                    problem = $@"the game's value is '{t}', which is not true or false";
                    return new JValue(t);

                default:
                    return new JValue(t);
            }
        }
    }
}
