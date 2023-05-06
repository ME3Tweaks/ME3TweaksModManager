using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.batch
{
    /// <summary>
    /// For storing data about a texture mod file
    /// </summary>
    public class SerializedTextureMod
    {
        [JsonProperty(@"hash")]
        public string TextureModHash { get; set; }
        [JsonProperty(@"size")]
        public long TextureModSize { get; set; }
        [JsonProperty(@"path")]
        public string TextureModPath { get; set; }
        [JsonProperty(@"attachedtomoddescmod")]
        public bool AttachedToModdescMod { get; set; }
        [JsonProperty(@"intexturelibrary")]
        public bool InTextureLibrary { get; set; }
    }
}
