using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>Where a weapon's effective value for one property actually comes from.</summary>
    public enum WeaponValueScope
    {
        /// <summary>
        /// Nothing in the chain sets it, there is no compiled default, and the property's type has
        /// no zero we are willing to claim. Shown as "not set anywhere" rather than as a zero.
        /// </summary>
        Unset,

        /// <summary>
        /// Nothing sets it, but the type's own zero is what the game uses - false, 0, or "".
        ///
        /// Separate from <see cref="Default"/> because it is a DIFFERENT claim: Default means we read
        /// a value out of a compiled Default__ object, TypeDefault means we read nothing and are
        /// telling you what the absence means. Collapsing the two would make a deduction
        /// indistinguishable from a measurement. See docs/16-weapon-defaults.md.
        /// </summary>
        TypeDefault,

        /// <summary>This weapon's own config section sets it.</summary>
        Own,

        /// <summary>An ancestor's config section sets it and this weapon does not.</summary>
        Inherited,

        /// <summary>No config section in the chain sets it; the value is the compiled class default.</summary>
        Default,
    }

    /// <summary>One property as a weapon actually sees it, with the reason it has that value.</summary>
    public class WeaponValue
    {
        public string Name { get; init; }
        public string Value { get; init; }
        public WeaponValueScope Scope { get; init; }

        /// <summary>
        /// The section the value came from. Equal to the weapon's own section when
        /// <see cref="Scope"/> is Own; the ancestor's when Inherited; null otherwise.
        ///
        /// Carried because an edit has to be written to the weapon's OWN section even when the value
        /// being changed was inherited - editing the ancestor would change every weapon under it.
        /// The editor needs to say which case it is before the edit, not after.
        /// </summary>
        public string FromSection { get; init; }

        /// <summary>The declared type, where the chain declares one. Diagnostic, and what decides
        /// whether an absence resolves to a type zero.</summary>
        public string DeclaredType { get; init; }
    }

    /// <summary>
    /// One weapon: a class descending from SFXWeapon, with the config it sets and the config it
    /// inherits.
    /// </summary>
    public class WeaponDefinition
    {
        /// <summary>Coalesced section, "package.class". What an edit writes to.</summary>
        public string Section { get; init; }

        /// <summary>Bare class name, e.g. SFXWeapon_SMG_Hornet.</summary>
        public string ClassName { get; init; }

        /// <summary>Section of the immediate superclass.</summary>
        public string SuperSection { get; init; }

        /// <summary>Coalesced sub-file the section lives in - BioWeapon for nearly all of them.</summary>
        public string ConfigFile { get; init; }

        /// <summary>
        /// The base that decides which category this weapon is filed under - the nearest ancestor
        /// that is one of the shipped `*_Base` classes, or SFXWeapon/SFXHeavyWeapon at the root.
        /// </summary>
        public string BaseSection { get; internal set; }

        /// <summary>Assault Rifle, Shotgun, Pistol, Sniper Rifle, SMG, Heavy Weapon, or Other.</summary>
        public string Category { get; internal set; }

        /// <summary>True for the SFXWeapon_AI_* classes the enemies use.</summary>
        public bool IsAI { get; init; }

        /// <summary>
        /// The player weapon this AI weapon is a variant OF, or null when there is none.
        ///
        /// Derived from the superclass chain rather than by matching "AI_Hornet" to "Hornet",
        /// because only 11 of the 20 AI weapons are variants of a player weapon at all. The other
        /// nine - Harvester, Ravager, the Scion cannon, the Geth Prime rifle and so on - descend
        /// straight from a base or from SFXWeapon and have no player counterpart to be a variant of.
        /// Name-matching would invent pairings for those and miss that they are their own weapons.
        /// </summary>
        public string PairedSection { get; internal set; }

        /// <summary>Values this weapon's own section sets, verbatim, by property name.</summary>
        public Dictionary<string, string> Own { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Object path of the weapon's store-card art, or null where it has no card.
        ///
        /// From the reinforcement manager's `CardData`, matched on the card's `UniqueName` - which is
        /// the SECTION path, not the bare class name. 65 of LMP's 80 player weapons have one; the
        /// rest are bases and cut content, which correctly have no card because nobody can be dealt
        /// one.
        /// </summary>
        public string CardTexture { get; internal set; }

        /// <summary>The card's rarity token, e.g. Rarity_UltraRare. Null where there is no card.</summary>
        public string Rarity { get; internal set; }

        /// <summary>
        /// The package file the card art lives in, from BioEngine's `DynamicLoadMapping`.
        ///
        /// The game does not find `GUI_Icons.Weapons.asl_avenger_256x128` by searching for it - it
        /// looks the object up in that mapping and loads the seek-free package the mapping names.
        /// Carried because it is the difference between "this texture does not exist" and "this
        /// texture lives in SFXNotificationImages_AssaultRifles, which this install does not ship" -
        /// and on LMP the second is true of 43 of the 62 weapon cards.
        /// </summary>
        public string CardPackage { get; internal set; }
    }

    /// <summary>
    /// The weapons surface: every class descending from SFXWeapon, grouped the way the game groups
    /// them, with the player and AI sides kept apart.
    ///
    /// Found by DESCENT, never by the SFXWeapon name prefix. The prefix also catches SFXWeaponFactory,
    /// SFXWeaponNode, SFXWeaponUseModule, the SFXWeaponGameEffect_* pair and all 43 weapon MODS - 56
    /// classes that are not weapons and whose properties have nothing to do with weapon stats. On LMP
    /// the prefix finds 156 classes and descent finds 100.
    ///
    /// Deliberately NOT here: SFXWeaponMod and its 43 descendants. They are a sibling of SFXWeapon,
    /// not a kind of it, and they carry bonuses rather than stats. Worth an editor; not this one.
    /// </summary>
    public class WeaponCatalogue
    {
        public const string WeaponRootClass = @"SFXGame.SFXWeapon";
        public const string HeavyWeaponClass = @"SFXGame.SFXHeavyWeapon";
        public const string AIPrefix = @"SFXWeapon_AI_";

        /// <summary>Where the store cards live, and the property that holds them.</summary>
        public const string CardSection = @"sfxgamempcontent.sfxgawreinforcementmanager";
        public const string CardProperty = @"CardData";

        /// <summary>
        /// The one-line description the loadout screen shows under a weapon's name.
        ///
        /// A string ref, so it is already localised. Named here rather than written inline because it
        /// is the property the editor's description line stands on, and a rename in the game would
        /// otherwise be a silent blank rather than a compile error.
        /// </summary>
        public const string DescriptionProperty = @"ShortDescription";

        /// <summary>Where the engine records which seek-free package an object is loaded from.</summary>
        public const string DynamicLoadSection = @"sfxgame.sfxengine";
        public const string DynamicLoadProperty = @"DynamicLoadMapping";

        /// <summary>
        /// The shipped bases, and the words for them. Ordered: the FIRST match walking up from a
        /// weapon wins, so the specific bases have to come before the two roots.
        /// </summary>
        private static readonly (string Section, string Category)[] Bases =
        [
            (@"SFXGame.SFXWeapon_AssaultRifle_Base", @"Assault Rifle"),
            (@"SFXGame.SFXWeapon_Shotgun_Base", @"Shotgun"),
            (@"SFXGame.SFXWeapon_Pistol_Base", @"Pistol"),
            (@"SFXGame.SFXWeapon_SniperRifle_Base", @"Sniper Rifle"),
            (@"SFXGame.SFXWeapon_SMG_Base", @"SMG"),
            (HeavyWeaponClass, @"Heavy Weapon"),
            (WeaponRootClass, @"Other"),
        ];

        public List<WeaponDefinition> Weapons { get; } = new();

        public List<string> Warnings { get; } = new();

        /// <summary>The player-side weapons, which is what most of the editor is about.</summary>
        public IEnumerable<WeaponDefinition> PlayerWeapons => Weapons.Where(w => !w.IsAI);

        /// <summary>The weapons enemies fire. Segmented, because their stats are not the player's.</summary>
        public IEnumerable<WeaponDefinition> AIWeapons => Weapons.Where(w => w.IsAI);

        public WeaponDefinition Find(string section) =>
            Weapons.FirstOrDefault(w => string.Equals(w.Section, section, StringComparison.OrdinalIgnoreCase));

        public static WeaponCatalogue Build(MergedGameConfig config,
                                            IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (classes == null) throw new ArgumentNullException(nameof(classes));

            var result = new WeaponCatalogue();
            result.Read(config.BuildSectionIndex(), classes);
            return result;
        }

        /// <summary>
        /// One property as this weapon sees it: its own config, then each ancestor's config, then the
        /// compiled default.
        ///
        /// The order is the whole point of a weapons editor. Weapons inherit heavily - SFXWeapon alone
        /// declares 135 properties - so most of what a weapon "has" is set somewhere above it, and an
        /// editor that showed only a weapon's own section would show a Hornet with almost nothing on
        /// it. Config before defaults, because config REPLACES the compiled default rather than
        /// layering on it (docs/02 §1.6).
        /// </summary>
        public WeaponValue Effective(WeaponDefinition weapon, string property, SectionIndex index,
                                     IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            if (weapon == null) return null;

            // Resolved once for every branch below. The type is not only the fallback's business:
            // the page uses it to decide how to draw a control, so a value that came from config
            // needs it just as much as one that did not.
            var declared = DeclaredTypeOf(weapon.Section, property, classes);

            foreach (var cls in ClassScanner.Chain(weapon.Section, classes))
            {
                var values = index.Values(cls.Section, property).ToList();
                if (values.Count == 0) continue;

                var own = string.Equals(cls.Section, weapon.Section, StringComparison.OrdinalIgnoreCase);
                return new WeaponValue
                {
                    Name = property,
                    // The last value wins: a later DLC's write is what the game ends up with.
                    Value = values[^1].Value,
                    Scope = own ? WeaponValueScope.Own : WeaponValueScope.Inherited,
                    FromSection = cls.Section,
                    DeclaredType = declared,
                };
            }

            if (ClassDefaults.Effective(weapon.Section, property, classes) is { } fallback)
            {
                return new WeaponValue
                {
                    Name = property, Value = fallback,
                    Scope = WeaponValueScope.Default, DeclaredType = declared,
                };
            }

            // Nothing anywhere. For the types whose zero is settled this is still an answer; for the
            // rest it is genuinely unknown and saying "0" would be inventing one. Which types are
            // which, and why, is docs/16-weapon-defaults.md.
            return new WeaponValue
            {
                Name = property,
                Value = TypeZero(declared),
                Scope = TypeZero(declared) == null ? WeaponValueScope.Unset : WeaponValueScope.TypeDefault,
                DeclaredType = declared,
            };
        }

        /// <summary>
        /// Every property this weapon could be given a value for: the union of what its class chain
        /// declares. Sorted, so the editor's order does not depend on dictionary iteration.
        /// </summary>
        public static IEnumerable<string> PropertyNames(WeaponDefinition weapon,
                                                        IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var seen = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cls in ClassScanner.Chain(weapon.Section, classes))
                foreach (var prop in cls.OwnProperties)
                    seen.Add(prop.Name);
            return seen;
        }

        /// <summary>
        /// The zero a property of this type takes when nothing sets it, or null where that is not
        /// established.
        ///
        /// Deliberately partial. Bool, float, string and string-ref are confirmed; struct, array and
        /// int are not, and `WeaponAcquiredID` is exactly the kind of identifier where the zero could
        /// as easily be -1. Returning a value for those would put a guess where every consumer would
        /// read a measurement. docs/16-weapon-defaults.md carries the evidence and the open questions.
        /// </summary>
        public static string TypeZero(string declaredType) => declaredType switch
        {
            @"BoolProperty" => @"False",
            @"FloatProperty" => @"0.0",
            @"StrProperty" => string.Empty,
            @"StringRefProperty" => @"0",
            _ => null,
        };

        /// <summary>The type the chain declares this property with, or null if nothing declares it.</summary>
        public static string DeclaredTypeOf(string section, string property,
                                            IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            foreach (var cls in ClassScanner.Chain(section, classes))
            {
                var declared = cls.OwnProperties.FirstOrDefault(pr =>
                    pr.Name.Equals(property, StringComparison.OrdinalIgnoreCase));
                if (declared != null) return declared.DeclaredType;
            }
            return null;
        }

        // ------------------------------------------------------------------ internals

        private void Read(SectionIndex index, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            foreach (var cls in classes.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!Descends(cls.Section, WeaponRootClass, classes)) continue;

                var weapon = new WeaponDefinition
                {
                    Section = cls.Section,
                    ClassName = cls.Name,
                    SuperSection = cls.SuperClass,
                    ConfigFile = cls.ConfigFile,
                    IsAI = cls.Name.StartsWith(AIPrefix, StringComparison.OrdinalIgnoreCase),
                };

                if (index.Find(cls.Section) is { } section)
                {
                    foreach (var prop in section)
                    {
                        var live = MergedGameConfig.Live(prop.Value).ToList();
                        if (live.Count > 0) weapon.Own[prop.Key] = live[^1].Value;
                    }
                }

                Categorise(weapon, classes);
                Weapons.Add(weapon);
            }

            if (Weapons.Count == 0)
            {
                Warnings.Add($@"No class descends from {WeaponRootClass}; the weapons editor has nothing to show.");
                return;
            }

            PairAIWeapons(classes);
            ReadCards(index);
        }

        /// <summary>
        /// Attaches each weapon's store-card art from the reinforcement manager.
        ///
        /// Matched on the card's `UniqueName`, which is the weapon's full SECTION path rather than
        /// its class name - the two look interchangeable in a list of 289 cards and are not, and
        /// matching on the class name finds nothing at all.
        /// </summary>
        private void ReadCards(SectionIndex index)
        {
            var cards = index.Values(CardSection, CardProperty).ToList();
            if (cards.Count == 0)
            {
                Warnings.Add($@"{CardSection} sets no {CardProperty}; no weapon card art will be shown.");
                return;
            }

            foreach (var card in cards)
            {
                if (!ConfigStructReader.TryReadStructRaw(card.Value, out var fields)) continue;

                var unique = Unquote(fields.GetValueOrDefault(@"UniqueName"));
                if (string.IsNullOrWhiteSpace(unique)) continue;

                var weapon = Find(unique);
                if (weapon == null) continue;   // Most cards are characters, powers and consumables.

                weapon.CardTexture = Unquote(fields.GetValueOrDefault(@"GUITextureRef"));
                weapon.Rarity = Unquote(fields.GetValueOrDefault(@"Rarity"));
            }

            AttachCardPackages(index);
        }

        /// <summary>
        /// Resolves each card texture to the seek-free package that holds it.
        ///
        /// This is how the engine itself finds them: an object named in config is looked up in
        /// `DynamicLoadMapping` and loaded from the `SeekFreePackageName` it gives, rather than being
        /// searched for. Doing the same here means a missing card can say WHICH package is absent.
        /// </summary>
        private void AttachCardPackages(SectionIndex index)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in index.Values(DynamicLoadSection, DynamicLoadProperty))
            {
                if (!ConfigStructReader.TryReadStructRaw(entry.Value, out var fields)) continue;
                var name = Unquote(fields.GetValueOrDefault(@"ObjectName"));
                var package = Unquote(fields.GetValueOrDefault(@"SeekFreePackageName"));
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(package))
                    mapping[name] = package;
            }

            if (mapping.Count == 0)
            {
                Warnings.Add($@"{DynamicLoadSection} sets no {DynamicLoadProperty}; " +
                             @"a missing weapon card cannot say which package it needed.");
                return;
            }

            foreach (var weapon in Weapons.Where(w => w.CardTexture != null))
            {
                if (mapping.TryGetValue(weapon.CardTexture, out var package)) weapon.CardPackage = package;
            }
        }

        private static string Unquote(string text) => text?.Trim().Trim('"');

        /// <summary>Every card texture path worth loading, for handing to the art loader.</summary>
        public IEnumerable<string> CardTextures =>
            Weapons.Select(w => w.CardTexture).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct();

        private void Categorise(WeaponDefinition weapon, IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            foreach (var cls in ClassScanner.Chain(weapon.Section, classes))
            {
                foreach (var (section, category) in Bases)
                {
                    if (!string.Equals(cls.Section, section, StringComparison.OrdinalIgnoreCase)) continue;

                    // A base is not a weapon a player carries, but it IS the thing every weapon under
                    // it inherits from, so it is kept and marked rather than hidden.
                    weapon.BaseSection = section;
                    weapon.Category = category;
                    return;
                }
            }

            weapon.Category = @"Other";
            Warnings.Add($@"{weapon.ClassName} descends from {WeaponRootClass} but from none of the known bases.");
        }

        /// <summary>
        /// Links each AI weapon to the player weapon it is a variant of, where there is one.
        ///
        /// Walks the superclass chain and takes the first ancestor that is a player-side weapon and
        /// not a base. Nine of LMP's twenty AI weapons find nothing, correctly: they descend straight
        /// from a base or from SFXWeapon and are enemy-only weapons rather than variants.
        /// </summary>
        private void PairAIWeapons(IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            foreach (var weapon in Weapons.Where(w => w.IsAI))
            {
                foreach (var cls in ClassScanner.Chain(weapon.SuperSection, classes))
                {
                    if (cls.Name.StartsWith(AIPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    if (Bases.Any(b => string.Equals(b.Section, cls.Section, StringComparison.OrdinalIgnoreCase))) break;

                    if (Find(cls.Section) is { IsAI: false }) { weapon.PairedSection = cls.Section; break; }
                }
            }
        }

        private static bool Descends(string section, string root,
                                     IReadOnlyDictionary<string, CatalogueClass> classes) =>
            ClassScanner.Chain(section, classes)
                        .Any(c => string.Equals(c.Section, root, StringComparison.OrdinalIgnoreCase));
    }
}
