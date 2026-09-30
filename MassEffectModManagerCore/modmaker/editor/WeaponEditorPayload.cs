using System;
using System.Collections.Generic;
using System.Linq;
using ME3TweaksModManager.modmaker.generator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>
    /// The weapons editor's data, shaped for the page.
    ///
    /// Two top-level groups rather than one list: the weapons a player carries, and the weapons
    /// enemies fire. They are different surfaces even where they share a name - the AI Hornet is not
    /// the Hornet with a flag set, it is a separate class with its own numbers - and mixing them
    /// would put two rows called "Hornet" next to each other with no way to tell which is which.
    ///
    /// Within each group, the game's own bases give the categories. Nothing here is a list someone
    /// typed: the categories come from descent, so a weapon Milan adds is filed correctly the day it
    /// appears rather than falling into "Other" until someone updates a roster.
    /// </summary>
    public static class WeaponEditorPayload
    {
        /// <summary>
        /// The stats worth a labelled control, grouped the way an author thinks about them.
        ///
        /// A curated list rather than "every property", and the reason is measured: a weapon's class
        /// chain declares far more than it uses, and 442 of those resolve to nothing at all even
        /// after type zeros are applied (docs/16-weapon-defaults.md). Rendering every declared
        /// property would bury `damage` under a thousand blank rows. Everything not named here is
        /// still reachable through the generic Values editor.
        /// </summary>
        private static readonly (string Group, string Name, string Label, string Unit, string Help)[] Knobs =
        [
            (@"Damage and fire rate", @"Damage", @"Damage", null,
                @"Damage per shot, before difficulty and enemy resistances."),
            (@"Damage and fire rate", @"RateOfFire", @"Rate of fire", @"rpm", null),
            (@"Damage and fire rate", @"RoundsPerBurst", @"Rounds per burst", null, null),
            (@"Damage and fire rate", @"DamageHench", @"Damage (squadmate)", null, null),

            (@"Ammunition", @"MagSize", @"Magazine size", @"rounds", null),
            (@"Ammunition", @"MaxSpareAmmo", @"Spare ammo", @"rounds", null),
            (@"Ammunition", @"ReloadDuration", @"Reload time", @"seconds", null),
            (@"Ammunition", @"bInfiniteAmmo", @"Infinite ammo", null, null),
            (@"Ammunition", @"EncumbranceWeight", @"Weight", null,
                @"What the weapon costs against the loadout's weight budget."),

            (@"Accuracy", @"MinAimError", @"Aim error, minimum", null, null),
            (@"Accuracy", @"MaxAimError", @"Aim error, maximum", null, null),
            (@"Accuracy", @"AccFirePenalty", @"Accuracy penalty per shot", null, null),
            (@"Accuracy", @"AccFireInterpSpeed", @"Accuracy recovery speed", null, null),
            (@"Accuracy", @"MinZoomAimError", @"Aim error, zoomed minimum", null, null),
            (@"Accuracy", @"MaxZoomAimError", @"Aim error, zoomed maximum", null, null),
            (@"Accuracy", @"ZoomAccFirePenalty", @"Zoomed penalty per shot", null, null),
            (@"Accuracy", @"ZoomAccFireInterpSpeed", @"Zoomed recovery speed", null, null),

            (@"Recoil", @"Recoil", @"Recoil", null, null),
            (@"Recoil", @"ZoomRecoil", @"Recoil, zoomed", null, null),
            (@"Recoil", @"RecoilYawScale", @"Horizontal recoil scale", null, null),
            (@"Recoil", @"RecoilMinFade", @"Recoil fade, minimum", null, null),

            // Kept in their own group and named for what they are. These are the numbers the AI fires
            // by, and they exist on player weapons too - which is why they are not the AI SECTION.
            (@"AI firing behaviour", @"RateOfFireAI", @"Rate of fire (AI)", @"rpm",
                @"What the AI fires this weapon at, which is not the player's rate of fire."),
            (@"AI firing behaviour", @"AI_AccCone_Min", @"Accuracy cone, minimum", @"degrees", null),
            (@"AI firing behaviour", @"AI_AccCone_Max", @"Accuracy cone, maximum", @"degrees", null),
            (@"AI firing behaviour", @"AI_BurstFireCount", @"Shots per burst", null, null),
            (@"AI firing behaviour", @"AI_BurstFireDelay", @"Delay between bursts", @"seconds", null),
            (@"AI firing behaviour", @"AI_AimDelay", @"Aim delay", @"seconds", null),

            // Last, and labelled as display-only, because they are the single most misleading thing
            // on this surface - see Groups below.
            (@"Stat bars (display only)", @"StatBarDamage", @"Damage bar", null, null),
            (@"Stat bars (display only)", @"StatBarRateOfFire", @"Rate of fire bar", null, null),
            (@"Stat bars (display only)", @"StatBarAccuracy", @"Accuracy bar", null, null),
        ];

        /// <summary>
        /// A note attached to a whole group, where the group needs one.
        ///
        /// The stat bars earn theirs. They are what the loadout screen DRAWS, not what the weapon
        /// does, and nothing keeps them in step with the real values - the old ModMaker wrote
        /// `damage` and `statbardamage` from one variable for 126 weapons (docs/04 §Multi-property
        /// drivers), which is how a mod ends up with a gun that hits for twice what its bar claims.
        /// Said here rather than left for the author to discover from a bug report.
        /// </summary>
        private static readonly Dictionary<string, string> GroupNotes = new(StringComparer.OrdinalIgnoreCase)
        {
            [@"Stat bars (display only)"] =
                @"What the loadout screen draws, not what the weapon does. Nothing keeps these in " +
                @"step with the real values - change one and the other does not follow.",
            [@"AI firing behaviour"] =
                @"How the AI fires this weapon. Present on player weapons too, and ignored while a " +
                @"player is holding it.",
        };

        public static JObject Build(WeaponCatalogue weapons, SectionIndex sections,
                                    IReadOnlyDictionary<string, CatalogueClass> classes,
                                    GameStrings strings = null, BannerLibrary cards = null,
                                    TextLayer text = null)
        {
            if (weapons == null) throw new ArgumentNullException(nameof(weapons));
            if (sections == null) throw new ArgumentNullException(nameof(sections));
            if (classes == null) throw new ArgumentNullException(nameof(classes));

            return new JObject
            {
                [@"format"] = 1,
                [@"groups"] = new JArray(
                    Group(@"player", @"Player weapons", weapons.PlayerWeapons, weapons, sections, classes, strings, cards, text),
                    Group(@"ai", @"Enemy weapons", weapons.AIWeapons, weapons, sections, classes, strings, cards, text)),
                [@"knobGroups"] = new JArray(Knobs.Select(k => k.Group).Distinct()
                    .Select(g => new JObject
                    {
                        [@"title"] = g,
                        [@"note"] = GroupNotes.GetValueOrDefault(g),
                    })),
                [@"warnings"] = new JArray(weapons.Warnings),
            };
        }

        public static string ToJson(WeaponCatalogue weapons, SectionIndex sections,
                                    IReadOnlyDictionary<string, CatalogueClass> classes,
                                    GameStrings strings = null, BannerLibrary cards = null,
                                    TextLayer text = null, bool indented = true) =>
            Build(weapons, sections, classes, strings, cards, text).ToString(indented ? Formatting.Indented : Formatting.None);

        private static JObject Group(string id, string title, IEnumerable<WeaponDefinition> members,
                                     WeaponCatalogue weapons, SectionIndex sections,
                                     IReadOnlyDictionary<string, CatalogueClass> classes, GameStrings strings,
                                     BannerLibrary cards, TextLayer text)
        {
            var list = members.ToList();
            return new JObject
            {
                [@"id"] = id,
                [@"title"] = title,
                [@"count"] = list.Count,
                // Categories carried as an ordered list rather than derived on the page, so the
                // sections appear in the same order every time regardless of how the weapons sort.
                [@"categories"] = new JArray(list.Select(w => w.Category).Distinct()
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)),
                // Ordered as a TREE, depth-first, so a weapon always follows the weapon it actually
                // descends from. Sorting by depth and indenting is not enough: it puts every depth-2
                // weapon after whichever depth-1 weapon happens to sort before it, which reads as a
                // parent-child link that is not there.
                [@"weapons"] = new JArray(Ordered(list, classes)
                    .Select(e => Weapon(e.Weapon, e.Parent, e.Depth, weapons, sections, classes, strings, cards, text))),
            };
        }

        /// <summary>One weapon placed in the tree: who it hangs off, and how deep that is.</summary>
        private readonly record struct Placed(WeaponDefinition Weapon, WeaponDefinition Parent, int Depth);

        /// <summary>
        /// Arranges a group's weapons as a forest, depth-first, one tree per category.
        ///
        /// A weapon's parent is the nearest ancestor in the SAME group and the SAME category, because
        /// that is the only relationship the roster can actually draw: it renders one indented list
        /// per category, so a parent filed elsewhere is a parent with no row above the child to hang
        /// off. The literal superclass is reported separately on the detail pane, where it can be
        /// named rather than implied by position.
        ///
        /// So `SFXWeapon_Pistol_Base` heads Pistol even though it descends from `SFXWeapon`, and an AI
        /// weapon is never indented under the player weapon it varies - the two sides are deliberately
        /// separate lists, and the pairing is stated instead.
        /// </summary>
        private static List<Placed> Ordered(List<WeaponDefinition> group,
                                            IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var result = new List<Placed>();

            foreach (var category in group.Select(w => w.Category).Distinct()
                                          .OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            {
                var members = group.Where(w => string.Equals(w.Category, category, StringComparison.Ordinal)).ToList();
                var inCategory = members.ToDictionary(w => w.Section, w => w, StringComparer.OrdinalIgnoreCase);

                WeaponDefinition ParentOf(WeaponDefinition weapon)
                {
                    foreach (var cls in ClassScanner.Chain(weapon.SuperSection, classes))
                        if (inCategory.TryGetValue(cls.Section, out var found)) return found;
                    return null;
                }

                var parents = members.ToDictionary(w => w.Section, ParentOf, StringComparer.OrdinalIgnoreCase);
                var children = members.GroupBy(w => parents[w.Section]?.Section ?? string.Empty,
                                               StringComparer.OrdinalIgnoreCase)
                                      .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                void Emit(WeaponDefinition weapon, WeaponDefinition parent, int depth)
                {
                    // A class chain cannot contain a cycle, but a malformed one must not hang the editor.
                    if (!seen.Add(weapon.Section)) return;
                    result.Add(new Placed(weapon, parent, depth));

                    if (!children.TryGetValue(weapon.Section, out var kids)) return;
                    foreach (var kid in kids.OrderBy(k => k.ClassName, StringComparer.OrdinalIgnoreCase))
                        Emit(kid, weapon, depth + 1);
                }

                // Bases first: a base is what everything below it inherits from, so it heads its
                // category rather than sitting alphabetically in the middle of it.
                foreach (var root in members.Where(w => parents[w.Section] == null)
                                            .OrderBy(w => IsBase(w) ? 0 : 1)
                                            .ThenBy(w => w.ClassName, StringComparer.OrdinalIgnoreCase))
                    Emit(root, null, 0);
            }

            return result;
        }

        /// <summary>A class other weapons inherit from rather than one anybody carries.</summary>
        private static bool IsBase(WeaponDefinition weapon) =>
            string.Equals(weapon.Section, weapon.BaseSection, StringComparison.OrdinalIgnoreCase);

        private static JObject Weapon(WeaponDefinition weapon, WeaponDefinition parent, int depth,
                                      WeaponCatalogue weapons, SectionIndex sections,
                                      IReadOnlyDictionary<string, CatalogueClass> classes, GameStrings strings,
                                      BannerLibrary cards, TextLayer text)
        {
            var name = weapons.Effective(weapon, @"PrettyName", sections, classes);

            var entry = new JObject
            {
                [@"section"] = weapon.Section,
                [@"className"] = weapon.ClassName,
                [@"title"] = Title(weapon, name, strings),
                [@"category"] = weapon.Category,
                [@"isAI"] = weapon.IsAI,
                [@"base"] = weapon.BaseSection,
                [@"asset"] = sections.AssetOf(weapon.Section),
                // Said even in the segmented view: knowing the AI Hornet is a variant of the Hornet
                // is what tells an author the two are meant to be compared.
                [@"paired"] = weapon.PairedSection,
                [@"pairedTitle"] = weapon.PairedSection == null
                    ? null
                    : weapons.Find(weapon.PairedSection)?.ClassName,
                [@"stats"] = new JArray(Knobs.Select(k => Stat(weapon, k, weapons, sections, classes))),
                [@"isBase"] = IsBase(weapon),
                // Tree depth, and the weapon directly above it. The roster indents by this, so it is
                // only ever meaningful alongside the parent it was computed against.
                [@"depth"] = depth,
                [@"parent"] = parent?.Section,
                [@"parentTitle"] = parent?.ClassName,
                // What the class ACTUALLY inherits from, whether or not that is a weapon the editor
                // lists. Stated on the detail pane, because the roster's tree can only show links
                // between weapons it is showing and that is not the whole truth.
                [@"inheritsFrom"] = InheritsFrom(weapon, classes),
                // The superclass as a SECTION as well as a name, so the page can offer it as a link.
                // Often it is not a weapon the editor lists - an intermediate class, or SFXWeapon
                // itself - and the page decides that by looking, which is why both are sent.
                [@"inheritsFromSection"] = weapon.SuperSection,
                [@"description"] = Description(weapon, weapons, sections, classes, strings, text),
                [@"rarity"] = weapon.Rarity,
                [@"cardPath"] = weapon.CardTexture,
                [@"cardPackage"] = weapon.CardPackage,
            };

            if (weapon.CardTexture != null && cards?.For(weapon.CardTexture) is { } art)
            {
                if (art.DataUri is { } uri) entry[@"card"] = uri;
                // A path that resolved to nothing is a reason rather than an absence - the same
                // distinction the maplist draws. Where the engine's own mapping names the package,
                // say THAT instead: "this install does not ship SFXNotificationImages_AssaultRifles"
                // is something an author can act on, and "no Texture2D with this path exists" is not.
                else if (weapon.CardPackage is { } package)
                    entry[@"cardProblem"] = $@"the art is in {package}, which this install does not ship";
                else if (art.Problem is { } problem) entry[@"cardProblem"] = problem;
            }

            return entry;
        }

        /// <summary>
        /// What the game shows under this weapon's name, and where the text came from.
        ///
        /// Three cases, and the page needs to be able to tell them apart:
        ///
        /// - The weapon sets its own `ShortDescription`. 64 of the 102 do; this is the real answer.
        /// - It inherits one from another weapon. 14 do, and the game genuinely shows that text - the
        ///   AI Mattock reads as the Mattock - so it is shown, attributed to where it came from.
        /// - It inherits the root `SFXGame.SFXWeapon` default, which is the developers' own
        ///   placeholder ("Damage: MassRiffic Range: MassTacular"). 24 do, mostly turrets and
        ///   enemy-only guns. That is not a description, so it is not offered as one.
        ///
        /// The third case is detected by WHERE the value came from, not by what it says: matching the
        /// placeholder's wording would break the moment anyone edited it, and the section it is
        /// inherited from is the durable fact.
        ///
        /// A description authored in the text layer overrides all three, which is the only way an
        /// enemy-only weapon gets one - the game has no string for it, and this editor is not the
        /// place to be minting TLK entries.
        /// </summary>
        private static JObject Description(WeaponDefinition weapon, WeaponCatalogue weapons, SectionIndex sections,
                                           IReadOnlyDictionary<string, CatalogueClass> classes,
                                           GameStrings strings, TextLayer text)
        {
            var value = weapons.Effective(weapon, WeaponCatalogue.DescriptionProperty, sections, classes);

            var reference = value?.Value;
            var resolved = string.IsNullOrWhiteSpace(reference) || reference == @"0"
                ? null
                : strings?.Resolve(reference);
            // Resolve hands back the number itself when it has no string for it, which is not text.
            if (string.IsNullOrWhiteSpace(resolved) || resolved == reference) resolved = null;

            var placeholder = value?.FromSection != null && string.Equals(
                value.FromSection, WeaponCatalogue.WeaponRootClass, StringComparison.OrdinalIgnoreCase);
            if (placeholder) resolved = null;

            var custom = text?.ClassText != null
                         && text.ClassText.TryGetValue(weapon.Section, out var authored)
                ? authored?.Description
                : null;
            if (string.IsNullOrWhiteSpace(custom)) custom = null;

            var inherited = value?.FromSection != null && !placeholder
                            && !string.Equals(value.FromSection, weapon.Section, StringComparison.OrdinalIgnoreCase);

            return new JObject
            {
                [@"property"] = WeaponCatalogue.DescriptionProperty,
                [@"text"] = custom ?? resolved,
                [@"source"] = custom != null ? @"custom" : resolved != null ? @"game" : @"none",
                [@"ref"] = reference,
                // Named so the page can say "inherited from the Mattock" and link it, rather than
                // presenting another weapon's copy as though it were written for this one.
                [@"fromSection"] = inherited ? value.FromSection : null,
                [@"fromTitle"] = inherited ? weapons.Find(value.FromSection)?.ClassName ?? value.FromSection : null,
                // True where the only thing the game offers is its own placeholder. Reported even
                // when a custom description covers it, because it is why the custom one exists.
                [@"placeholder"] = placeholder,
            };
        }

        /// <summary>
        /// The class this weapon immediately descends from, by name.
        ///
        /// The literal superclass, not the nearest one the editor happens to list. An author asking
        /// what a weapon inherits from wants the answer the config would give, including the
        /// intermediate classes the roster has no row for.
        /// </summary>
        private static string InheritsFrom(WeaponDefinition weapon,
                                           IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            if (string.IsNullOrWhiteSpace(weapon.SuperSection)) return null;

            var super = classes.Values.FirstOrDefault(c =>
                string.Equals(c.Section, weapon.SuperSection, StringComparison.OrdinalIgnoreCase));
            return super?.Name ?? weapon.SuperSection;
        }

        private static JObject Stat(WeaponDefinition weapon,
                                    (string Group, string Name, string Label, string Unit, string Help) knob,
                                    WeaponCatalogue weapons, SectionIndex sections,
                                    IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            var value = weapons.Effective(weapon, knob.Name, sections, classes);

            var stat = new JObject
            {
                [@"group"] = knob.Group,
                [@"name"] = knob.Name,
                [@"label"] = knob.Label,
                [@"value"] = value.Value,
                // The whole reason this editor is workable. 8945 of a weapon's values are inherited
                // against 3766 of its own, so without this every control would look identical and an
                // author could not tell what this weapon actually sets from what it merely receives.
                [@"scope"] = value.Scope.ToString().ToLowerInvariant(),
                [@"from"] = value.FromSection,
                [@"type"] = value.DeclaredType,
            };

            if (knob.Unit != null) stat[@"unit"] = knob.Unit;
            if (knob.Help != null) stat[@"help"] = knob.Help;
            return stat;
        }

        /// <summary>
        /// What to call a weapon: its TLK name where it has one, else the class name made readable.
        ///
        /// The class name is kept alongside rather than replaced, because it is the thing an author
        /// searches by and the thing that appears in a compiled mod. "Hornet" is friendlier;
        /// "SFXWeapon_SMG_Hornet" is what the config says.
        /// </summary>
        private static string Title(WeaponDefinition weapon, WeaponValue prettyName, GameStrings strings)
        {
            if (prettyName?.Value is { } reference && strings?.Resolve(reference) is { } resolved
                && !string.IsNullOrWhiteSpace(resolved))
                return resolved;

            var text = weapon.ClassName;
            foreach (var prefix in new[] { @"SFXWeapon_AI_", @"SFXWeapon_" })
            {
                if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                text = text[prefix.Length..];
                break;
            }

            // The category is already the group heading, so "SMG_Hornet" reads as "Hornet" there.
            foreach (var family in new[] { @"AssaultRifle_", @"Shotgun_", @"Pistol_", @"SniperRifle_", @"SMG_", @"Heavy_" })
            {
                if (!text.StartsWith(family, StringComparison.OrdinalIgnoreCase)) continue;
                text = text[family.Length..];
                break;
            }

            text = text.Replace('_', ' ');

            // A base is not a gun anyone carries, and "Base" on its own tells a reader nothing. Named
            // for the category it heads, and marked, because it IS editable - changing it changes
            // every weapon beneath it, which the validator warns about when someone does.
            if (weapon.ClassName.EndsWith(@"_Base", StringComparison.OrdinalIgnoreCase))
                return $@"{weapon.Category} (shared base)";

            return text;
        }
    }
}
