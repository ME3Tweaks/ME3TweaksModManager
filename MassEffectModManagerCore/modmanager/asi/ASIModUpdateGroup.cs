using System;
using System.Collections.Generic;
using System.Text;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.usercontrols;

namespace MassEffectModManagerCore.modmanager.asi
{
    /// <summary>
    /// Represents a single group of ASI mods across versions. This is used to help prevent installation of duplicate ASIs even if the names differ
    /// </summary>
    public class ASIModUpdateGroup
    {
        public List<ASIMod> ASIModVersions { get; internal set; }
        public int UpdateGroupId { get; internal set; }
        public Mod.MEGame Game { get; internal set; }
        public bool IsHidden { get; set; }

        public ASIMod GetLatestVersion()
        {
            return ASIModVersions.MaxBy(x => x.Version);
        }
    }
}
