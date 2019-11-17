using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    public class ThirdPartyServices
    {
        /// <summary>
        /// Looks up importing information for mods through the third party mod importing service. This returns all candidates, the client code must determine which is the appropriate value.
        /// </summary>
        /// <param name="archiveSize">Size of archive being checked for information</param>
        /// <returns>List of candidates</returns>
        public static List<ThirdPartyImportingInfo> GetImportingInfosBySize(long archiveSize)
        {
            if (App.ThirdPartyImportingService == null) return new List<ThirdPartyImportingInfo>(); //Not loaded
            if (App.ThirdPartyImportingService.TryGetValue(archiveSize, out var result))
            {
                return result;
            }

            return new List<ThirdPartyImportingInfo>();
        }

        /// <summary>
        /// Looks up information about a DLC mod through the third party identification service
        /// </summary>
        /// <param name="dlcName"></param>
        /// <param name="game">Game to look in database for</param>
        /// <returns>Third party mod info about dlc folder, null if not found</returns>
        public static ThirdPartyModInfo GetThirdPartyModInfo(string dlcName, Mod.MEGame game)
        {
            if (App.ThirdPartyIdentificationService == null) return null; //Not loaded
            if (App.ThirdPartyIdentificationService.TryGetValue(game.ToString(), out var infosForGame))
            {
                if (infosForGame.TryGetValue(dlcName, out var info))
                {
                    return info;
                }
            }

            return null;
        }


        public class ThirdPartyImportingInfo
        {
            public string md5 { get; set; }
            public string inarchivepathtosearch { get; set; }
            public string filename { get; set; }
            public string subdirectorydepth { get; set; }
            public string servermoddescname { get; set; }
            public Mod.MEGame game { get; set; }
            public string version { get; set; }
            public string requireddlc { get; set; }
            public string zippedexepath { get; set; }

            public List<string> GetParsedRequiredDLC()
            {
                if (!string.IsNullOrWhiteSpace(requireddlc))
                {
                    return requireddlc.Split(';').ToList();
                }
                else
                {
                    return new List<string>();
                }
            }
        }


        public class ThirdPartyModInfo : INotifyPropertyChanged
        {
            /// <summary>
            /// Denotes that this TPMI object represents a preview object (such as in Starter Kit)
            /// </summary>
            public bool IsPreview { get; internal set; }
            /// <summary>
            /// Denotes this TPMI object is selected in a listbox. (UI only)
            /// </summary>
            public bool IsSelected { get; set; }
            public string dlcfoldername { get; set; } //This is also the key into the TPMIS dictionary. 
            public string modname { get; set; }
            public string moddev { get; set; }
            public string modsite { get; set; }
            public string moddesc { get; set; }
            public string mountpriority { get; set; }
            public string modulenumber { get; set; } //ME2 only
            public string preventimport { get; set; }
            public string updatecode { get; set; } //has to be string I guess

            public int MountPriorityInt => string.IsNullOrWhiteSpace(mountpriority) ? 0 : int.Parse(mountpriority);
            public string StarterKitString => $"{MountPriorityInt} - {modname}{(modulenumber != null ? " - Module # " + modulenumber : "")}";

            public event PropertyChangedEventHandler PropertyChanged;
        }

        internal static List<ThirdPartyModInfo> GetThirdPartyModInfosByModuleNumber(int modDLCModuleNumber)
        {
            if (App.ThirdPartyIdentificationService == null) return new List<ThirdPartyModInfo>(); //Not loaded
            var me2Values = App.ThirdPartyIdentificationService["ME2"];
            return me2Values.Where(x => x.Value.modulenumber == modDLCModuleNumber.ToString()).Select(x=>x.Value).ToList();
        }

        internal static List<ThirdPartyModInfo> GetThirdPartyModInfosByMountPriority(Mod.MEGame game, int modMountPriority)
        {
            if (App.ThirdPartyIdentificationService == null) return new List<ThirdPartyModInfo>(); //Not loaded
            var gameValues = App.ThirdPartyIdentificationService[game.ToString()];
            return gameValues.Where(x => x.Value.MountPriorityInt == modMountPriority).Select(x => x.Value).ToList();
        }
    }
}
