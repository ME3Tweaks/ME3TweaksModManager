using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using Newtonsoft.Json;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.objects
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

        /// <summary>
        /// If this is the wildcard handler
        /// </summary>
        [JsonIgnore]
        public bool IsWildcard => Domains != null && Domains.Any(x => x == @"*");

        [JsonIgnore] public string DomainsEditable { get; set; }

        /// <summary>
        /// The message shown for validation
        /// </summary>
        public string ValidationMessage { get; set; }

        /// <summary>
        /// Loads DomainsEditable
        /// </summary>
        public void LoadEditable()
        {
            DomainsEditable = string.Join(',', Domains);
        }

        /// <summary>
        /// Invokes this domain handler with the specified nxmlink
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="nxmLink"></param>
        /// <returns></returns>
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
                    return M3L.GetString(M3L.string_nxm_interp_handlerProgramDoesNotExist, domain, ProgramPath);
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
            if (File.Exists(M3Filesystem.GetExternalNexusHandlersFile()))
            {
                M3Log.Information(@"Loading external nxm handler info");
                App.NexusDomainHandlers.ReplaceAll(JsonConvert.DeserializeObject<List<NexusDomainHandler>>(File.ReadAllText(M3Filesystem.GetExternalNexusHandlersFile())));
            }
        }

        /// <summary>
        /// Handles an nxmlink with an external application, if one is registered
        /// </summary>
        /// <param name="nxmLink"></param>
        /// <returns></returns>
        public static bool HandleExternalLink(string nxmLink)
        {
            return HandleExternalLink(NexusProtocolLink.Parse(nxmLink));
        }

        public static bool HandleExternalLink(NexusProtocolLink npl)
        {
            if (npl == null) return false;
            if (IsModManagerDomain(npl.Domain)) return false; // This is not handled externally
            var handler = App.NexusDomainHandlers.FirstOrDefault(x => x.Domains.Contains(npl.Domain));
            if (handler == null) handler = App.NexusDomainHandlers.FirstOrDefault(x => x.IsWildcard);
            if (handler != null)
            {
                handler.Invoke(npl.Domain, npl.Link);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Validates the domain handler
        /// </summary>
        /// <returns></returns>
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(ProgramPath))
            {
                ValidationMessage = M3L.GetString(M3L.string_nxm_applicationPathMustBeSpecified);
                return false; // Can't be empty
            }
            if (!File.Exists(ProgramPath))
            {
                ValidationMessage = M3L.GetString(M3L.string_nxm_interp_applicationDoesntExistProgramPath, ProgramPath);
                return false;
            }

            if (Path.GetFileName(ProgramPath).Equals(@"ME3TweaksModManager.exe", StringComparison.InvariantCultureIgnoreCase))
            {
                ValidationMessage = M3L.GetString(M3L.string_nxm_interp_cannotSetME3TweaksModManagerexeAsAnExternalHandler);
                return false;
            }


            if (!Arguments.Contains(@"%1"))
            {
                ValidationMessage = M3L.GetString(M3L.string_nxm_argumentsMustIncludePercent);
                return false;
            }

            if (string.IsNullOrWhiteSpace(DomainsEditable))
            {
                ValidationMessage = M3L.GetString(M3L.string_nxm_domainsMustBeSpecified);
                return false;
            }

            var domains = DomainsEditable.Split(',');
            foreach (var domain in domains)
            {
                if (IsModManagerDomain(domain))
                {
                    ValidationMessage = M3L.GetString(M3L.string_nxm_interp_alreadyHandledByM3, domain);
                    return false;
                }
            }

            ValidationMessage = null;
            return true;
        }

        /// <summary>
        /// If ME3Tweaks Mod Manager is equipped to handle the specified NexusMods domain
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public static bool IsModManagerDomain(string domain)
        {
            if (domain == null) return false;
            domain = domain.Trim();
            if (domain.Equals(@"masseffect", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (domain.Equals(@"masseffect2", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (domain.Equals(@"masseffect3", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (domain.Equals(@"masseffectlegendaryedition", StringComparison.InvariantCultureIgnoreCase)) return true;
            return false;
        }
    }
}
