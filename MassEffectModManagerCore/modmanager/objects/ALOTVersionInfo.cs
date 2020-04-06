/*
 * Ported from ALOT Installer
 */
namespace MassEffectModManagerCore.modmanager.objects
{
    public class ALOTVersionInfo
    {
        public short ALOTVER;
        public byte ALOTUPDATEVER;
        public byte ALOTHOTFIXVER;
        public int MEUITMVER;

        public ALOTVersionInfo(short ALOTVersion, byte ALOTUpdaterVersion, byte ALOTHotfixVersion, int MEUITMVersion)
        {
            this.ALOTVER = ALOTVersion;
            this.ALOTUPDATEVER = ALOTUpdaterVersion;
            this.ALOTHOTFIXVER = ALOTHotfixVersion;
            this.MEUITMVER = MEUITMVersion;
        }

        //Todo: Maybe support MEUITM. 
        public override string ToString()
        {
            if (ALOTVER == 0 && ALOTUPDATEVER == 0 && MEUITMVER == 0)
            {
                return "Texture modded";
            }

            return $@"{ALOTVER}.{ALOTUPDATEVER}";
        }
    }
}