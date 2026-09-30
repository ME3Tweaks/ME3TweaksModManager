using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3TweaksModManager.modmaker.editor
{
    /// <summary>
    /// The generic value editor's data: the definition bundle, shaped for a page.
    ///
    /// Unlike the three dedicated editors this one has no catalogue of its own - the bundle already
    /// IS the catalogue, so the work here is subtraction rather than construction. Two things are
    /// removed, and both matter:
    ///
    ///  - **The verbatim originals.** `originalValue` / `originalValues` exist so the COMPILER can
    ///    re-emit untouched array entries byte-for-byte. A page renders the parsed tree and never
    ///    needs them, and they are 1.45 MB of the bundle's 8.46.
    ///  - **Classes with nothing to edit.** Of the game's 3,579 config classes, most declare no
    ///    property the config surfaces. A page for one would be an empty page, and it would bury the
    ///    classes that do have something in a list nobody can navigate.
    ///
    /// What is deliberately NOT done here is inheritance. The schema allows a property to be edited on
    /// any subclass that inherits it - the project key carries the class precisely so that a cascaded
    /// property can be overridden per subclass - but this editor shows each property on the class that
    /// DECLARES it. Every property is still reachable exactly once, which is the property that matters
    /// for a first version; per-subclass override is a bigger idea and wants its own design, because
    /// the same leaf then has as many project keys as there are subclasses.
    /// </summary>
    public static class ValuesEditorPayload
    {
        /// <summary>The project surface this page edits. Not a collection - `values` is top level.</summary>
        public const string CollectionId = @"values";

        /// <summary>Fields the compiler needs and a page does not.</summary>
        private static readonly string[] CompilerOnly = [@"originalValue", @"originalValues", @"arrayType"];

        public static JObject Build(JObject bundle)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            var properties = new JArray();
            var classesWithContent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in bundle[@"properties"] as JArray ?? [])
            {
                var trimmed = (JObject)property.DeepClone();
                foreach (var field in CompilerOnly) trimmed.Remove(field);

                properties.Add(trimmed);
                if ((string)trimmed[@"class"] is { } owner) classesWithContent.Add(owner);
            }

            var classes = new JArray();
            foreach (var entry in bundle[@"classes"] as JArray ?? [])
            {
                var id = (string)entry[@"id"];
                if (id == null || !classesWithContent.Contains(id)) continue;
                classes.Add(entry.DeepClone());
            }

            return new JObject
            {
                [@"game"] = bundle[@"game"]?.DeepClone(),
                [@"bundleVersion"] = bundle[@"bundleVersion"]?.DeepClone(),
                [@"groups"] = bundle[@"groups"]?.DeepClone() ?? new JArray(),
                [@"classes"] = classes,
                [@"properties"] = properties,
            };
        }

        public static string ToJson(JObject bundle, bool indented = true) =>
            Build(bundle).ToString(indented ? Formatting.Indented : Formatting.None);
    }
}
