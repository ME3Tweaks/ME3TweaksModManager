using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3TweaksCore.NativeMods;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.batch
{
    /// <summary>
    /// Defines an ASI mod for installation for serialization and deserialization in a Batch Queue
    /// </summary>
    public class BatchASIMod : IBatchQueueMod
    {
        /// <summary>
        /// Generates a batch ASI mod from the latest version of an ASI
        /// </summary>
        /// <param name="m"></param>
        public BatchASIMod(ASIMod m)
        {
            UpdateGroup = m.UpdateGroupId;
            Version = m.LatestVersion.Version;
            AssociatedMod = m.LatestVersion;
        }

        public BatchASIMod() { }

        /// <summary>
        /// The UpdateGroup of the ASI
        /// </summary>
        [JsonProperty(@"updategroup")]
        public int UpdateGroup { get; set; }

        /// <summary>
        /// The version to install
        /// </summary>
        [JsonProperty(@"version")]
        public int Version { get; set; }

        /// <summary>
        /// The game this ASI is for
        /// </summary>
        [JsonIgnore]
        public MEGame Game { get; set; }

        [JsonIgnore]
        public ASIModVersion AssociatedMod { get; private set; }

        public bool AssociateASIObject(MEGame game)
        {
            Game = game; // This is not serialized as it is done via the containing serialized item so we set it here.
            AssociatedMod = ASIManager.GetASIVersion(UpdateGroup, Version, game);
            return AssociatedMod != null;
        }

        public bool IsAvailableForInstall()
        {
            return AssociatedMod != null;
        }

        // These are not used but are required for IBatchMod interface
        [JsonIgnore]
        public string Hash { get; set; }
        [JsonIgnore]
        public long Size { get; set; }
    }
}
