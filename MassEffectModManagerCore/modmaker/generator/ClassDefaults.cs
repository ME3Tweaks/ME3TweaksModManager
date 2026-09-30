using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace ME3TweaksModManager.modmaker.generator
{
    /// <summary>
    /// The compiled defaults a class ships with, read from its Default__ object.
    ///
    /// These are the other half of the effective-value chain. Config REPLACES defaults rather than
    /// merging with them, so the effective value of a property is "the merged config's value if it has
    /// one, otherwise this" - never a combination. Without defaults, a property the shipped config
    /// never mentions has no known starting value, and "revert to shipped" has nothing to revert to.
    ///
    /// Only scalars are read here. A struct or array default is part of a value tree and belongs with
    /// the parser that builds those, not with a flat lookup that would have to stringify it.
    /// </summary>
    public static class ClassDefaults
    {
        /// <summary>
        /// True for exports the defaults scan needs, on top of the class and property exports the
        /// hierarchy scan needs.
        ///
        /// The default object's ClassName is the CLASS's own name, so no predicate over class names
        /// can find it; it is identified by the Default__ prefix the engine gives it. Leaving it out
        /// of the partial load leaves every default null while the scan still reports success.
        /// </summary>
        public static bool IsDefaultObject(ExportEntry e) =>
            e.ObjectName.Name.StartsWith(@"Default__", StringComparison.Ordinal);

        /// <summary>
        /// Reads the scalar defaults from one class's Default__ object.
        /// Returns an empty map when the class has none, which is normal rather than an error.
        /// </summary>
        public static Dictionary<string, string> Read(IMEPackage package, int defaultsUIndex)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (package == null || defaultsUIndex == 0) return result;
            if (package.GetEntry(defaultsUIndex) is not ExportEntry defaults) return result;

            PropertyCollection properties;
            try { properties = defaults.GetProperties(); }
            catch { return result; }

            foreach (var property in properties)
            {
                var text = Scalar(property);
                if (text != null) result[property.Name.Name] = text;
            }
            return result;
        }

        /// <summary>
        /// A property's value as text, or null when it is not a scalar.
        ///
        /// Floats are written round-trippably ("R"): the defaults are compared against config text and
        /// re-emitted into a bundle, and a value that changes when it is formatted would show a
        /// difference the game does not have.
        /// </summary>
        private static string Scalar(Property property) => property switch
        {
            FloatProperty f => f.Value.ToString(@"R", CultureInfo.InvariantCulture),
            IntProperty i => i.Value.ToString(CultureInfo.InvariantCulture),
            BoolProperty b => b.Value ? @"true" : @"false",
            StrProperty s => s.Value,
            NameProperty n => n.Value.Name,
            ByteProperty by => by.Value.ToString(CultureInfo.InvariantCulture),
            EnumProperty e => e.Value.Name,
            StringRefProperty sr => sr.Value.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };

        /// <summary>
        /// A property's default for a class, resolved up the SuperClass chain.
        ///
        /// A subclass's default object does not restate values it does not change, so looking only at
        /// the leaf finds nothing for most properties and would report "no default" for content that
        /// plainly has one.
        /// </summary>
        public static string Effective(string section, string propertyName,
                                       IReadOnlyDictionary<string, CatalogueClass> classes)
        {
            foreach (var cls in ClassScanner.Chain(section, classes))
            {
                if (cls.Defaults != null && cls.Defaults.TryGetValue(propertyName, out var value)) return value;
            }
            return null;
        }

        /// <summary>How many of a class chain's config properties have a compiled default. Diagnostic.</summary>
        public static int CountWithDefaults(string section, IReadOnlyDictionary<string, CatalogueClass> classes) =>
            ClassScanner.EffectiveProperties(section, classes)
                        .Count(p => Effective(section, p.Property.Name, classes) != null);
    }
}
