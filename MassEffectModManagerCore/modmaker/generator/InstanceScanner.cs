using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>
    /// Where a config property is written for a class configured per-object.
    /// See docs/02-data-model.md §1.6 - this is read from the shipped config, never derived.
    /// </summary>
    public enum ConfigScope
    {
        /// <summary>The shipped config sets it in neither shape, so which one the game reads is unknown.</summary>
        Unverified = 0,

        /// <summary>Set in a "package.class" section: one value for every instance.</summary>
        Shared = 1,

        /// <summary>Set in an "instance class" section: one value per instance.</summary>
        Instance = 2,

        /// <summary>
        /// Set in both shapes. Should never happen - the two halves are exactly disjoint in shipped
        /// data - but a mod could do it, and silently picking one would write the user's edit into a
        /// section the game may not read.
        /// </summary>
        Both = 3,
    }

    /// <summary>One named object the shipped config configures.</summary>
    public class PerObjectInstance
    {
        /// <summary>The instance half of the section key, verbatim. Never synthesised - see below.</summary>
        public string Id { get; init; }

        /// <summary>How many properties this instance's section sets. Diagnostic / ordering hint.</summary>
        public int PropertiesSet { get; init; }

        /// <summary>Position in the merged config, so the shipped order can be preserved.</summary>
        public int Order { get; init; }
    }

    /// <summary>
    /// A class flagged EClassFlags.PerObjectConfig, and the instances of it the config names.
    /// </summary>
    public class PerObjectClass
    {
        /// <summary>Coalesced section of the class itself, "package.class".</summary>
        public string Section { get; init; }

        /// <summary>
        /// The class half of an instance section key: the bare class name with no package.
        ///
        /// Carried separately from Section and from the instance ids because none of the three can be
        /// computed from the others. SFXWave_Horde_Cerberus2's instances are named
        /// sfxwave_horde_cerberus1..11 - the BASE faction's names, not its own - so
        /// "sfxwave_horde_cerberus7 sfxwave_horde_cerberus" and
        /// "sfxwave_horde_cerberus7 sfxwave_horde_cerberus2" are different waves of different factions
        /// that share an instance name. The pair is the key; the instance name alone is ambiguous.
        /// </summary>
        public string ClassToken { get; init; }

        /// <summary>Instances in the order the merged config named them.</summary>
        public List<PerObjectInstance> Instances { get; } = new();

        /// <summary>
        /// True when the shipped config names no instances at all. 25 flagged classes on LMP are like
        /// this: surface a mod can add to, not an error. They are still catalogued.
        /// </summary>
        public bool Unconfigured => Instances.Count == 0;
    }

    /// <summary>
    /// How one (class, property) pair is scoped, and the evidence behind it.
    ///
    /// Scope belongs to the PAIR, not to the property. WeightedChanceOfSelection is declared once on
    /// SFXWave_Operation and is instance-scoped on six SFXEngagement_* subclasses but shared on
    /// SFXEngagement_Extraction, which has one instance and so had no reason to split it. Six
    /// inherited properties disagree this way on LMP, which is why the bundle carries a sparse
    /// scopeByClass beside the declaration's scope.
    /// </summary>
    public class PropertyScopeInfo
    {
        public ConfigScope Scope { get; init; }

        /// <summary>Set in an instance section keyed by this class's own token.</summary>
        public bool SetInInstanceSection { get; init; }

        /// <summary>Set in this class's own "package.class" section.</summary>
        public bool SetOnOwnClassPath { get; init; }

        /// <summary>
        /// Section of the ancestor whose class-path section sets it, when this class has none of its
        /// own. SFXWave_Horde_Cerberus has no class-path section at all - its shared values come from
        /// SFXWave_Horde's and reach it by class inheritance. Ignoring that would report those
        /// properties as unverified and hide them from the editor.
        /// </summary>
        public string SharedInheritedFrom { get; init; }
    }

    /// <summary>Tallies of how the shipped config scopes the config surface of per-object classes.</summary>
    public class ScopeTally
    {
        public int InstanceOnly;
        public int SharedOnly;
        public int Both;
        public int Unverified;
        public int Total => InstanceOnly + SharedOnly + Both + Unverified;
        public override string ToString() =>
            $@"instance={InstanceOnly} shared={SharedOnly} both={Both} unverified={Unverified}";
    }

    /// <summary>
    /// The instance dimension of the catalogue: which classes are configured per named object, which
    /// objects those are, and which section shape carries each of their properties.
    ///
    /// A class flagged perobjectconfig has its config in sections keyed "&lt;instance&gt; &lt;class&gt;"
    /// rather than "&lt;package&gt;.&lt;class&gt;", so one class can back many editable pages -
    /// SFXWave_Horde_Cerberus is eleven of them, not one.
    ///
    /// Both shapes are live for the same class family and they are disjoint. The obvious explanation -
    /// that the globalconfig property flag marks the shared half - is WRONG: exactly one of LMP's 638
    /// config properties on flagged classes carries it, and it is set by neither shape. So scope is
    /// read from which shape BioWare used, never derived from a flag. See docs/02-data-model.md §1.6.
    /// </summary>
    public class InstanceCatalogue
    {
        /// <summary>Instance sections read "instance class"; class-default ones read "package.class".</summary>
        private static readonly Regex InstanceSectionRx =
            new(@"^(?<inst>[^\s.]+)\s+(?<cls>[^\s.]+)$", RegexOptions.Compiled);

        /// <summary>Per-object classes by their bare class token.</summary>
        public IReadOnlyDictionary<string, PerObjectClass> ByClassToken { get; }

        /// <summary>The same classes by coalesced section, which is what the rest of the catalogue keys on.</summary>
        public IReadOnlyDictionary<string, PerObjectClass> BySection { get; }

        /// <summary>
        /// Instance sections whose class half is not a flagged class. Either a class the package scan
        /// could not read or config for content that is not installed; reported rather than dropped,
        /// because a silently discarded section is indistinguishable from one that was never there.
        /// </summary>
        public IReadOnlyList<string> OrphanInstanceSections { get; }

        /// <summary>
        /// Bare class names that resolved to more than one section. Empty on LMP. A collision makes
        /// "instance class" keys genuinely ambiguous, so it is surfaced rather than resolved by
        /// picking one.
        /// </summary>
        public IReadOnlyList<string> AmbiguousClassTokens { get; }

        // token -> property names set in "<instance> <token>" sections
        private readonly Dictionary<string, HashSet<string>> _setInInstance;
        // section -> property names set in that "package.class" section
        private readonly Dictionary<string, HashSet<string>> _setOnClassPath;

        private InstanceCatalogue(
            Dictionary<string, PerObjectClass> byToken,
            Dictionary<string, PerObjectClass> bySection,
            List<string> orphans,
            List<string> ambiguous,
            Dictionary<string, HashSet<string>> setInInstance,
            Dictionary<string, HashSet<string>> setOnClassPath)
        {
            ByClassToken = byToken;
            BySection = bySection;
            OrphanInstanceSections = orphans;
            AmbiguousClassTokens = ambiguous;
            _setInInstance = setInInstance;
            _setOnClassPath = setOnClassPath;
        }

        /// <summary>
        /// Builds the instance dimension from an already-merged config and an already-scanned class
        /// set. Both inputs are required: the flag lives in the packages and the instance names live
        /// only in the config, and neither can be inferred from the other.
        /// </summary>
        public static InstanceCatalogue Build(MergedGameConfig config, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (classes == null) throw new ArgumentNullException(nameof(classes));

            var byToken = new Dictionary<string, PerObjectClass>(StringComparer.OrdinalIgnoreCase);
            var bySection = new Dictionary<string, PerObjectClass>(StringComparer.OrdinalIgnoreCase);
            var ambiguous = new List<string>();

            foreach (var cls in classes.Values.Where(c => c.PerObjectConfig))
            {
                var poc = new PerObjectClass { Section = cls.Section, ClassToken = cls.Name };
                if (!byToken.TryAdd(cls.Name, poc))
                {
                    if (!byToken[cls.Name].Section.Equals(cls.Section, StringComparison.OrdinalIgnoreCase))
                        ambiguous.Add(cls.Name);
                    continue;
                }
                bySection[cls.Section] = poc;
            }

            var orphans = new List<string>();
            var setInInstance = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var setOnClassPath = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var seenSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int order = 0;

            foreach (var section in config.Sections())
            {
                // The same section key can appear in more than one coalesced sub-file. Counting an
                // instance twice would double every wavelist.
                if (!seenSections.Add(section.Key)) continue;

                var live = section.Value.Where(p => MergedGameConfig.Live(p.Value).Any())
                                        .Select(p => p.Key).ToList();

                var m = InstanceSectionRx.Match(section.Key);
                if (m.Success)
                {
                    var token = m.Groups[@"cls"].Value;
                    if (!byToken.TryGetValue(token, out var poc))
                    {
                        orphans.Add(section.Key);
                        continue;
                    }

                    poc.Instances.Add(new PerObjectInstance
                    {
                        Id = m.Groups[@"inst"].Value,
                        PropertiesSet = live.Count,
                        Order = order++,
                    });
                    Bag(setInInstance, token).UnionWith(live);
                }
                else
                {
                    // Every class-path section is recorded, not just those of per-object classes: a
                    // per-object class inherits shared values from ancestors that are themselves
                    // ordinary classes, and ScopeOf walks up into exactly those.
                    Bag(setOnClassPath, section.Key).UnionWith(live);
                }
            }

            return new InstanceCatalogue(byToken, bySection, orphans, ambiguous, setInInstance, setOnClassPath);
        }

        /// <summary>
        /// How a property of a per-object class is scoped, with the evidence.
        ///
        /// Shared evidence is looked for up the inheritance chain, not just on the class itself,
        /// because config sections are inherited: a leaf with no class-path section of its own still
        /// receives its ancestor's values. The write target is always the leaf's own class-path
        /// section, which overrides the inherited value for that class alone.
        /// </summary>
        public PropertyScopeInfo ScopeOf(string classSection, string propertyName,
                                         IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            bool inInstance = BySection.TryGetValue(classSection, out var poc)
                              && _setInInstance.TryGetValue(poc.ClassToken, out var inst)
                              && inst.Contains(propertyName);

            bool onOwn = _setOnClassPath.TryGetValue(classSection, out var own) && own.Contains(propertyName);

            string inheritedFrom = null;
            if (!onOwn && classes != null)
            {
                foreach (var ancestor in ClassScanner.Chain(classSection, classes).Skip(1))
                {
                    if (_setOnClassPath.TryGetValue(ancestor.Section, out var a) && a.Contains(propertyName))
                    {
                        inheritedFrom = ancestor.Section;
                        break;
                    }
                }
            }

            // Evidence on the class itself decides, and inherited evidence only fills a gap. The more
            // specific section wins at runtime, so a leaf that BioWare configured per instance stays
            // instance-scoped even when an ancestor sets a shared baseline for the same property -
            // SFXEngagement_Assassination.OperationTimeLimit and SFXEngagement_Disarm.srEndWaveFailure
            // are both of those, and both are a baseline plus an override rather than a contradiction.
            //
            // So Both means what it should: the same class sets one property in both of ITS OWN
            // shapes, leaving no defensible write target. That is empty in shipped data, and
            // inheritance can no longer manufacture it.
            var scope = onOwn && inInstance ? ConfigScope.Both
                      : inInstance ? ConfigScope.Instance
                      : onOwn || inheritedFrom != null ? ConfigScope.Shared
                      : ConfigScope.Unverified;

            return new PropertyScopeInfo
            {
                Scope = scope,
                SetInInstanceSection = inInstance,
                SetOnOwnClassPath = onOwn,
                // Populated even when the scope is instance, so the editor can show the baseline the
                // per-instance values are overriding.
                SharedInheritedFrom = inheritedFrom,
            };
        }

        /// <summary>
        /// Tallies scope across every effective property of every per-object class.
        ///
        /// <paramref name="ownEvidenceOnly"/> reproduces the measurement the design was written from -
        /// evidence taken from the class itself, ignoring inherited class-path sections - which is the
        /// form the disjointness claim (0 in Both) was verified in. The default counts inherited
        /// shared evidence too, which is what the editor uses; it moves properties out of Unverified
        /// and into Shared, and must still never produce a Both.
        /// </summary>
        public ScopeTally Tally(IReadOnlyDictionary<string, CatalogueClass> classes, bool ownEvidenceOnly = false)
        {
            var tally = new ScopeTally();
            foreach (var poc in BySection.Values)
            {
                foreach (var (prop, _) in ClassScanner.EffectiveProperties(poc.Section, classes))
                {
                    var info = ScopeOf(poc.Section, prop.Name, ownEvidenceOnly ? null : classes);
                    switch (info.Scope)
                    {
                        case ConfigScope.Both: tally.Both++; break;
                        case ConfigScope.Instance: tally.InstanceOnly++; break;
                        case ConfigScope.Shared: tally.SharedOnly++; break;
                        default: tally.Unverified++; break;
                    }
                }
            }
            return tally;
        }

        /// <summary>
        /// Every (class, property) the shipped config sets in BOTH shapes once inherited class-path
        /// sections are counted.
        ///
        /// Empty on shipped data - the two halves are exactly disjoint, which is what makes scope safe
        /// to model at all. A mod could still produce one, and it has no defensible write target, so
        /// it is listed rather than silently resolved: writing a user's edit into the wrong shape is
        /// invisible, because the mod builds, installs, mounts and does nothing.
        /// </summary>
        public List<(string Section, string Property, string SharedFrom)> ScopeConflicts(
            IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var result = new List<(string, string, string)>();
            foreach (var poc in BySection.Values)
            {
                foreach (var (prop, _) in ClassScanner.EffectiveProperties(poc.Section, classes))
                {
                    var info = ScopeOf(poc.Section, prop.Name, classes);
                    if (info.Scope == ConfigScope.Both)
                        result.Add((poc.Section, prop.Name, info.SharedInheritedFrom ?? poc.Section));
                }
            }
            return result;
        }

        /// <summary>
        /// Orders instance ids the way a person reads them, so wave 10 does not sort before wave 2.
        /// Only for display - the shipped Order is what the bundle preserves.
        /// </summary>
        public static int CompareNatural(string a, string b)
        {
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    // Compared as text of equal length rather than parsed, so an id with a run of
                    // digits longer than long.MaxValue cannot throw.
                    var na = a[si..i].TrimStart('0');
                    var nb = b[sj..j].TrimStart('0');
                    if (na.Length != nb.Length) return na.Length - nb.Length;
                    int c = string.CompareOrdinal(na, nb);
                    if (c != 0) return c;
                }
                else
                {
                    int c = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                    if (c != 0) return c;
                    i++; j++;
                }
            }
            return (a.Length - i) - (b.Length - j);
        }

        private static HashSet<string> Bag(Dictionary<string, HashSet<string>> d, string key)
        {
            if (!d.TryGetValue(key, out var set)) d[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return set;
        }
    }
}
