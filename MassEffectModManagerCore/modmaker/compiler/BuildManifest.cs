using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.compiler
{
    /// <summary>
    /// The compile contract: one mod, fully resolved, described by schema/build.schema.json.
    ///
    /// FULLY RESOLVED is the whole point. Everything the definition bundle, the project file and the
    /// installed game contribute has already been applied by the time a manifest exists - inheritance,
    /// defaults, dimensions, struct serialization and the exact text of anything being replaced. The
    /// emitter that turns this into a DLC folder reads nothing else, which is what makes a build
    /// reproducible and a bad build inspectable: the manifest is a file a person can read and diff.
    ///
    /// Modelled with typed classes rather than a JObject, unlike <see cref="project.ModMakerProject"/>.
    /// A project is a user's document that a newer build must not strip; a manifest is generated on
    /// every compile and never edited, so there is nothing to preserve and the types buy checking.
    /// </summary>
    public class BuildManifest
    {
        public const int CurrentFormat = 1;

        /// <summary>Magic string, so a stray .json is not mistaken for a build manifest.</summary>
        public const string KindString = @"me3tweaks.modmaker.build";

        [JsonProperty(@"format")]
        public int Format { get; set; } = CurrentFormat;

        [JsonProperty(@"kind")]
        public string Kind { get; set; } = KindString;

        /// <summary>Game identity - LMP for Project Milan, LE3 for a stock Legendary Edition install.</summary>
        [JsonProperty(@"game")]
        public string Game { get; set; }

        [JsonProperty(@"generator", NullValueHandling = NullValueHandling.Ignore)]
        public BuildGenerator Generator { get; set; }

        [JsonProperty(@"mod")]
        public BuildMod Mod { get; set; } = new();

        [JsonProperty(@"jobs")]
        public List<BuildJob> Jobs { get; set; } = new();

        /// <summary>The one coalesced job, created on first use. Every config write goes through it.</summary>
        public CoalescedJob Coalesced()
        {
            foreach (var job in Jobs)
            {
                if (job is CoalescedJob existing) return existing;
            }

            var created = new CoalescedJob();
            Jobs.Add(created);
            return created;
        }

        public JObject ToJson() => JObject.FromObject(this);

        public string ToJsonString(bool indented = true) =>
            ToJson().ToString(indented ? Formatting.Indented : Formatting.None);

        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, ToJsonString());
        }
    }

    /// <summary>Provenance. Diagnostic only: a build must never depend on it.</summary>
    public class BuildGenerator
    {
        [JsonProperty(@"app", NullValueHandling = NullValueHandling.Ignore)]
        public string App { get; set; }

        [JsonProperty(@"bundleVersion", NullValueHandling = NullValueHandling.Ignore)]
        public string BundleVersion { get; set; }

        [JsonProperty(@"milanVersion", NullValueHandling = NullValueHandling.Ignore)]
        public string MilanVersion { get; set; }

        [JsonProperty(@"generatedAt", NullValueHandling = NullValueHandling.Ignore)]
        public string GeneratedAt { get; set; }
    }

    /// <summary>Everything needed to write the moddesc and the DLC folder.</summary>
    public class BuildMod
    {
        [JsonProperty(@"name")]
        public string Name { get; set; }

        [JsonProperty(@"author", NullValueHandling = NullValueHandling.Ignore)]
        public string Author { get; set; }

        [JsonProperty(@"description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        /// <summary>
        /// The project's revision, as a bare number.
        ///
        /// Not a version the user types. The legacy compiler wrote the server-side revision counter
        /// straight into moddesc's ModVersion, and following it keeps a rebuilt mod recognisably
        /// newer than the copy already installed without asking the user to maintain a version.
        /// </summary>
        [JsonProperty(@"version", NullValueHandling = NullValueHandling.Ignore)]
        public string Version { get; set; }

        [JsonProperty(@"dlcFolder")]
        public string DlcFolder { get; set; }

        [JsonProperty(@"mount", NullValueHandling = NullValueHandling.Ignore)]
        public BuildMount Mount { get; set; }
    }

    /// <summary>mount.dlc contents. Must mount above Project Milan, or none of this overrides anything.</summary>
    public class BuildMount
    {
        [JsonProperty(@"priority", NullValueHandling = NullValueHandling.Ignore)]
        public int? Priority { get; set; }

        [JsonProperty(@"tlkId", NullValueHandling = NullValueHandling.Ignore)]
        public int? TlkId { get; set; }

        /// <summary>
        /// Game 3 mount flags, as the integer mount.dlc carries.
        ///
        /// A ModMaker DLC loads in multiplayer and, by default, lets a player into a lobby with people
        /// who do not have it. See <see cref="InstallMount.Flags"/>; the second half of that is the
        /// one a project can turn off.
        /// </summary>
        [JsonProperty(@"flags", NullValueHandling = NullValueHandling.Ignore)]
        public int? Flags { get; set; }

    }

    /// <summary>
    /// One item of work. A discriminated list, so package edits can return one day without
    /// disturbing the shape of what already exists.
    /// </summary>
    public abstract class BuildJob
    {
        [JsonProperty(@"type", Order = -1)]
        public abstract string Type { get; }
    }

    /// <summary>
    /// Write a coalesced into the generated DLC.
    ///
    /// Values are final config text. The emitter does not parse or interpret them, which is
    /// deliberate: every decision about what a value MEANS has already been made somewhere it could
    /// be tested against a real install.
    /// </summary>
    public class CoalescedJob : BuildJob
    {
        public override string Type => @"coalesced";

        /// <summary>Keyed by coalesced sub-file without extension: BioGame, BioWeapon, BioDifficulty...</summary>
        [JsonProperty(@"files")]
        public SortedDictionary<string, CoalescedFile> Files { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets a sub-file, adding it if this is the first write into it.</summary>
        public CoalescedFile File(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName)) throw new ArgumentNullException(nameof(assetName));
            if (!Files.TryGetValue(assetName, out var file)) Files[assetName] = file = new CoalescedFile();
            return file;
        }
    }

    public class CoalescedFile
    {
        [JsonProperty(@"sections")]
        public List<BuildSection> Sections { get; set; } = new();

        /// <summary>
        /// Gets a section, adding it if absent.
        ///
        /// Matched case-insensitively but stored with the spelling first written, because coalesced
        /// section names are matched case-insensitively by the game and written in whatever case
        /// BioWare felt like. Two sections differing only in case would merge into the game as one
        /// while looking like two here.
        /// </summary>
        public BuildSection Section(string name)
        {
            foreach (var section in Sections)
            {
                if (string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase)) return section;
            }

            var created = new BuildSection { Name = name };
            Sections.Add(created);
            return created;
        }
    }

    public class BuildSection
    {
        /// <summary>
        /// Coalesced section name, e.g. "sfxgamecontent.sfxpowercustomaction_pull".
        ///
        /// Real ME3/LE3 section names can contain spaces - per-object config sections are
        /// "&lt;instance&gt; &lt;class&gt;" - so nothing may split this on whitespace.
        /// </summary>
        [JsonProperty(@"name")]
        public string Name { get; set; }

        [JsonProperty(@"properties")]
        public List<PropertyWrite> Properties { get; set; } = new();
    }

    /// <summary>One property write. Either a plain <see cref="Value"/> or a <see cref="Replace"/>, never both.</summary>
    public class PropertyWrite
    {
        [JsonProperty(@"name")]
        public string Name { get; set; }

        /// <summary>Final config text for a plain write, already serialized. Excludes <see cref="Replace"/>.</summary>
        [JsonProperty(@"value", NullValueHandling = NullValueHandling.Ignore)]
        public string Value { get; set; }

        /// <summary>
        /// Coalesced parse action: 0 New/overwrite, 1 RemoveProperty, 2 Add, 3 AddUnique, 4 Remove.
        /// It matters because this coalesced MERGES over Milan's rather than replacing it.
        /// </summary>
        [JsonProperty(@"valueType", NullValueHandling = NullValueHandling.Ignore)]
        public int? ValueType { get; set; }

        [JsonProperty(@"arrayType", NullValueHandling = NullValueHandling.Ignore)]
        public string ArrayType { get; set; }

        [JsonProperty(@"matchOnType", NullValueHandling = NullValueHandling.Ignore)]
        public string MatchOnType { get; set; }

        /// <summary>Originating leaf id. Diagnostic: it lets a bad value in a built mod be traced to a control.</summary>
        [JsonProperty(@"source", NullValueHandling = NullValueHandling.Ignore)]
        public string Source { get; set; }

        [JsonProperty(@"replace", NullValueHandling = NullValueHandling.Ignore)]
        public ValueReplacement Replace { get; set; }
    }

    /// <summary>
    /// An array-entry override: a type-4 Remove of <see cref="Remove"/> followed by a type-3
    /// AddUnique of <see cref="Add"/>.
    ///
    /// Paired rather than left as two loose entries because the order is load-bearing and the removal
    /// is EXACT-TEXT-SENSITIVE. If the removal text differs from the shipped value in any way - a
    /// trailing 'f', a different float rendering, one more space - the subtraction silently does
    /// nothing and the game ends up holding both entries. The mod builds, installs, and is wrong.
    /// </summary>
    public class ValueReplacement
    {
        /// <summary>Verbatim original config text, byte for byte as the install holds it.</summary>
        [JsonProperty(@"remove")]
        public string Remove { get; set; }

        [JsonProperty(@"add")]
        public string Add { get; set; }
    }
}
