using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using MassEffectModManagerCore.modmanager.nexusmodsintegration;
using Pathoschild.FluentNexus.Models;

namespace MassEffectModManagerCore.modmanager
{
    public partial class Mod
    {
        private bool checkedEndorsementStatus;
        public bool IsEndorsed { get; set; }
        public bool CanEndorse { get; set; }
        public string EndorsementStatus { get; set; } = "Endorse mod";

        public async Task<bool> GetEndorsementStatus()
        {
            if (!NexusModsUtilities.IsAuthenticated) return false;
            if (checkedEndorsementStatus) return IsEndorsed;
            var client = NexusModsUtilities.GetClient();
            string gamename = "masseffect";
            if (Game == MEGame.ME2) gamename += "2";
            if (Game == MEGame.ME3) gamename += "3";
            var modinfo = await client.Mods.GetMod(gamename, NexusModID);
            var endorsementstatus = modinfo.Endorsement;
            if (endorsementstatus != null)
            {
                if (endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Undecided || endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Abstained)
                {
                    IsEndorsed = false;
                }
                else if (endorsementstatus.EndorseStatus == Pathoschild.FluentNexus.Models.EndorsementStatus.Endorsed)
                {
                    IsEndorsed = true;
                }

                CanEndorse = true;
            }
            checkedEndorsementStatus = true;
            return IsEndorsed;
        }

        public void EndorseMod(Action<Mod, bool> newEndorsementStatus)
        {
            if (!NexusModsUtilities.IsAuthenticated || !CanEndorse) return;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += async (a, b) =>
            {
                var client = NexusModsUtilities.GetClient();
                string gamename = "masseffect";
                if (Game == MEGame.ME2) gamename += "2";
                if (Game == MEGame.ME3) gamename += "3";
                await client.Mods.Endorse(gamename, NexusModID, "1.0");
                checkedEndorsementStatus = false;
                IsEndorsed = await GetEndorsementStatus();
            };
            bw.RunWorkerCompleted += (a, b) => { newEndorsementStatus.Invoke(this, IsEndorsed); };
            bw.RunWorkerAsync();
        }
    }
}
