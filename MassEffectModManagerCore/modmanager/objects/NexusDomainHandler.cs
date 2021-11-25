using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using MassEffectModManagerCore.modmanager.diagnostics;
using MassEffectModManagerCore.modmanager.localizations;
using Newtonsoft.Json;
using PropertyChanged;

namespace MassEffectModManagerCore.modmanager.objects
{
    /// <summary>
    /// Describes a NexusMods domain and what program to invoke to handle it
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class NexusDomainHandler
    {
        [JsonProperty(@"domains")]
        public List<string> Domains { get; set; } = new();

        [JsonProperty(@"programpath")]
        public string ProgramPath { get; set; }

        [JsonProperty(@"arguments")]
        public string Arguments { get; set; }

        [JsonIgnore] public string DomainsEditable { get; set; }

        /// <summary>
        /// Loads DomainsEditable
        /// </summary>
        public void LoadEditable()
        {
            DomainsEditable = string.Join(',', Domains);
        }
        public string Invoke(string domain, string nxmLink)
        {
            try
            {
                if (File.Exists(ProgramPath))
                {
                    M3Utilities.RunProcess(ProgramPath, Arguments.Replace(@"%1", nxmLink));
                    return null;
                }
                else
                {
                    M3Log.Error($@"Cannot invoke handler for {domain} domain: specified application {ProgramPath} does not exist");
                    return M3L.GetString(M3L.string_interp_handlerProgramDoesNotExist, domain, ProgramPath);
                }
            }
            catch (Exception e)
            {
                M3Log.Error($@"Error invoking handler for {domain} domain: {e.Message}");
                return e.Message;
            }
            return null;
        }

        public static void LoadExternalHandlers()
        {
            if (File.Exists(M3Utilities.GetExternalNexusHandlersFile()))
            {
                M3Log.Information(@"Loading external nxm handler info");
                App.NexusDomainHandlers.ReplaceAll(JsonConvert.DeserializeObject<List<NexusDomainHandler>>(File.ReadAllText(M3Utilities.GetExternalNexusHandlersFile())));
            }
        }

        public static bool HandleExternalLink(string nxmLink)
        {
            return HandleExternalLink(NexusProtocolLink.Parse(nxmLink));
        }

        public static bool HandleExternalLink(NexusProtocolLink npl)
        {
            if (npl == null) return false;
            var handler = App.NexusDomainHandlers.FirstOrDefault(x => x.Domains.Contains(npl.Domain));
            if (handler != null)
            {
                handler.Invoke(npl.Domain, npl.Link);
                return true;
            }

            return false;
        }
    }
}
