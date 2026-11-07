using System.Text.Json.Serialization;

namespace ME3TweaksModManager.modmanager.milan
{
    /// <summary>
    /// The signable portion of a Milan delta manifest. Properties are declared in the exact
    /// order the signature algorithm requires - System.Text.Json serializes POCO members in
    /// declaration order (base type first), which is why <see cref="MilanDeltaManifest"/> extends
    /// this class instead of declaring <c>Signature</c> alongside these properties.
    /// </summary>
    internal class MilanDeltaSignableBody
    {
        [JsonPropertyName(@"milan")]
        public bool Milan { get; set; }

        [JsonPropertyName(@"version")]
        public double Version { get; set; }

        [JsonPropertyName(@"deltas")]
        public List<MilanDeltaFileEntry> Deltas { get; set; } = new();
    }

    /// <summary>
    /// Manifest describing the JPatch deltas that must be applied to specific LE3 packages when
    /// building the Project Milan base installation. Lives at MilanDelta/manifest.json inside the
    /// Project Milan mod's folder.
    /// </summary>
    internal class MilanDeltaManifest : MilanDeltaSignableBody
    {
        [JsonPropertyName(@"signature")]
        public string Signature { get; set; }
    }

    internal class MilanDeltaFileEntry
    {
        [JsonPropertyName(@"comment")]
        public string Comment { get; set; }

        /// <summary>
        /// Destination filename this delta applies to, e.g. "Core.pcc".
        /// </summary>
        [JsonPropertyName(@"filename")]
        public string Filename { get; set; }

        /// <summary>
        /// Path of the JPatch delta file, relative to the MilanDelta folder.
        /// </summary>
        [JsonPropertyName(@"delta")]
        public string Delta { get; set; }

        /// <summary>
        /// Expected size, in bytes, of the decompressed LE3 source package before patching.
        /// </summary>
        [JsonPropertyName(@"sourcesize")]
        public long SourceSize { get; set; }

        /// <summary>
        /// Expected SHA-256 (lowercase hex) of the decompressed LE3 source package before patching.
        /// </summary>
        [JsonPropertyName(@"sourcehash")]
        public string SourceHash { get; set; }

        /// <summary>
        /// Expected size, in bytes, of the decompressed package data after patching.
        /// </summary>
        [JsonPropertyName(@"destsize")]
        public long DestSize { get; set; }

        /// <summary>
        /// Expected SHA-256 (lowercase hex) of the decompressed package data after patching.
        /// </summary>
        [JsonPropertyName(@"desthash")]
        public string DestHash { get; set; }

        /// <summary>Optional preprocessing format. Null preserves legacy raw deltas.</summary>
        [JsonPropertyName(@"format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Format { get; set; }
    }
}
