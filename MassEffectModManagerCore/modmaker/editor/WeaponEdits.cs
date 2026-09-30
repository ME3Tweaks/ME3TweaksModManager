using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ME3TweaksModManager.modmaker.generator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>One stat the user changed on one weapon.</summary>
    public class WeaponEdit
    {
        /// <summary>
        /// The weapon's OWN config section - never the ancestor a value was inherited from.
        ///
        /// This is the whole subtlety of the surface. Most of what a weapon shows is inherited
        /// (8945 values against 3766 of its own on LMP), and writing an edit back to where the value
        /// came from would change every weapon under that ancestor: raising the Hornet's damage by
        /// editing SFXWeapon_SMG_Base raises it for every SMG in the game. An edit always lands on
        /// the weapon, which is also what makes it an override rather than a shared change.
        /// </summary>
        [JsonProperty(@"section")]
        public string Section { get; set; }

        [JsonProperty(@"property")]
        public string Property { get; set; }

        /// <summary>The new value, as config text.</summary>
        [JsonProperty(@"value")]
        public string Value { get; set; }
    }

    /// <summary>
    /// The user's weapon edits. Sparse: a weapon not in here is untouched, and the compiler leaves
    /// its config alone rather than restating it.
    /// </summary>
    public class WeaponEditDocument
    {
        public const string CollectionId = @"weapons";

        [JsonProperty(@"format")]
        public int Format { get; set; } = 1;

        /// <summary>Keyed "&lt;section&gt;|&lt;property&gt;" lowercased.</summary>
        [JsonProperty(@"values")]
        public Dictionary<string, WeaponEdit> Values { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public int Count => Values.Count;

        public static string Key(string section, string property) =>
            $@"{section}|{property}".ToLowerInvariant();

        public static WeaponEditDocument FromJson(JObject document) =>
            document == null
                ? new WeaponEditDocument()
                : document.ToObject<WeaponEditDocument>() ?? new WeaponEditDocument();

        public string ToJsonString(bool indented = true) =>
            JObject.FromObject(this).ToString(indented ? Formatting.Indented : Formatting.None);
    }

    public enum WeaponProblemSeverity { Warning, Error }

    public class WeaponProblem
    {
        public WeaponProblemSeverity Severity { get; init; }
        public string Where { get; init; }
        public string Message { get; init; }
        public override string ToString() => $@"{Severity}: {Where}: {Message}";
    }

    /// <summary>
    /// Weapon edits checked against the installed game.
    ///
    /// None of these throw when compiled. They produce a gun that behaves the way it always did, or
    /// one whose displayed stats disagree with what it actually does - failures that look like the
    /// mod not working rather than like a mistake. So they are caught while the author still
    /// remembers making the edit.
    /// </summary>
    public static class WeaponEditValidator
    {
        /// <summary>Stat bars and the real values they claim to show.</summary>
        private static readonly (string Bar, string Real)[] StatBars =
        [
            (@"StatBarDamage", @"Damage"),
            (@"StatBarRateOfFire", @"RateOfFire"),
        ];

        public static List<WeaponProblem> Validate(WeaponEditDocument edits, WeaponCatalogue weapons)
        {
            var problems = new List<WeaponProblem>();
            if (edits == null || weapons == null) return problems;

            foreach (var (key, edit) in edits.Values)
            {
                var where = edit.Section ?? key;

                if (string.IsNullOrWhiteSpace(edit.Section) || string.IsNullOrWhiteSpace(edit.Property))
                {
                    problems.Add(Error(where, @"names no section or no property, so there is nowhere to write it"));
                    continue;
                }

                var weapon = weapons.Find(edit.Section);
                if (weapon == null)
                {
                    problems.Add(Error(where,
                        @"is not a weapon in this install; it may have been renamed or removed since the edit was made"));
                    continue;
                }

                if (edit.Value == null)
                {
                    problems.Add(Error(where, $@"{edit.Property} has no value"));
                    continue;
                }

                // An edit onto a base rather than a weapon. Not refused - changing every SMG at once
                // is a legitimate thing to want - but it is almost never what someone MEANT to do
                // from a screen that was showing them one gun.
                if (string.Equals(weapon.Section, weapon.BaseSection, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add(Warning(weapon.ClassName,
                        $@"is a base class, so changing {edit.Property} changes every weapon that inherits from it"));
                }
            }

            problems.AddRange(StatBarDisagreements(edits, weapons));
            return problems;
        }

        /// <summary>
        /// Warns where a weapon's real stat was changed and the bar that claims to show it was not.
        ///
        /// The loadout screen draws the bars; nothing keeps them in step. The old ModMaker wrote both
        /// from one variable across 126 weapons (docs/04), which hid the problem by making them
        /// impossible to set apart - and produced its own bugs when the two needed to differ. Warning
        /// rather than syncing: a bar that deliberately understates a weapon is a real thing to want,
        /// and silently rewriting it would take that away.
        /// </summary>
        private static IEnumerable<WeaponProblem> StatBarDisagreements(WeaponEditDocument edits, WeaponCatalogue weapons)
        {
            foreach (var (bar, real) in StatBars)
            {
                foreach (var edit in edits.Values.Values.Where(e =>
                             string.Equals(e.Property, real, StringComparison.OrdinalIgnoreCase)))
                {
                    if (string.IsNullOrWhiteSpace(edit.Section)) continue;
                    if (edits.Values.ContainsKey(WeaponEditDocument.Key(edit.Section, bar))) continue;

                    var weapon = weapons.Find(edit.Section);
                    if (weapon == null) continue;

                    // Only where the weapon actually HAS a bar. A weapon that never set one is not
                    // showing a stale number, it is showing nothing.
                    if (!weapon.Own.ContainsKey(bar)) continue;

                    yield return Warning(weapon.ClassName,
                        $@"{real} was changed but {bar} was not, so the loadout screen will still draw the old value");
                }
            }
        }

        private static WeaponProblem Error(string where, string message) =>
            new() { Severity = WeaponProblemSeverity.Error, Where = where, Message = message };

        private static WeaponProblem Warning(string where, string message) =>
            new() { Severity = WeaponProblemSeverity.Warning, Where = where, Message = message };
    }
}
