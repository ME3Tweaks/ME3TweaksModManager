using System;
using System.Collections.Generic;
using System.Text;


namespace MassEffectModManagerCore.modmanager.helpers
{
    public class BackupService
    {
        public static bool ME1BackedUp => Utilities.GetGameBackupPath(Mod.MEGame.ME1) != null;
        public static bool ME2BackedUp => Utilities.GetGameBackupPath(Mod.MEGame.ME2) != null;
        public static bool ME3BackedUp => Utilities.GetGameBackupPath(Mod.MEGame.ME3) != null;

    }
}
