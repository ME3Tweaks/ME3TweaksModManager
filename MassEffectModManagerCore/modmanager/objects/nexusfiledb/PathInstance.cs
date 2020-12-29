using Newtonsoft.Json;

namespace MassEffectModManagerCore.modmanager.objects.nexusfiledb
{
    public class PathInstance
    {
        [JsonProperty(@"pathid")]
        public int PathId { get; set; }

        [JsonProperty(@"nameid")]
        public int NameId { get; set; }

        [JsonProperty(@"parentpathid")]
        public int ParentPathId { get; set; }

        public string GetFullPath(GameDatabase assocDB, string appendedText = null)
        {
            if (appendedText != null)
            {
                appendedText = $@"{assocDB.NameTable[NameId]}/{appendedText}";
            }
            else
            {
                appendedText = assocDB.NameTable[NameId];
            }

            if (ParentPathId == 0) return appendedText;
            return assocDB.Paths[ParentPathId].GetFullPath(assocDB, appendedText);
        }
    }
}
