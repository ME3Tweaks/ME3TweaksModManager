/*
 * Ported from ALOT Installer
 */

using MassEffectModManagerCore.modmanager.localizations;

namespace MassEffectModManagerCore.modmanager.objects
{
    public class ALOTVersionInfo
    {
        public short ALOTVER;
        public byte ALOTUPDATEVER;
        public byte ALOTHOTFIXVER;
        public int MEUITMVER;
        public int ALOT_INSTALLER_VERSION_USED;
        public int MEM_VERSION_USED;

        public ALOTVersionInfo(short ALOTVersion, byte ALOTUpdaterVersion, byte ALOTHotfixVersion, int MEUITMVersion, short memVersionUsed, short alotInstallerVersionUsed)
        {
            this.ALOTVER = ALOTVersion;
            this.ALOTUPDATEVER = ALOTUpdaterVersion;
            this.ALOTHOTFIXVER = ALOTHotfixVersion;
            this.MEUITMVER = MEUITMVersion;
            this.MEM_VERSION_USED = memVersionUsed;
            this.ALOT_INSTALLER_VERSION_USED = alotInstallerVersionUsed;
        }

        //Todo: Maybe support MEUITM. 
        public override string ToString()
        {
            if (ALOTVER == 0 && ALOTUPDATEVER == 0 && MEUITMVER == 0)
            {
                return M3L.GetString(M3L.string_textureModded);
            }

            return $@"{ALOTVER}.{ALOTUPDATEVER}";
        }
    }
}