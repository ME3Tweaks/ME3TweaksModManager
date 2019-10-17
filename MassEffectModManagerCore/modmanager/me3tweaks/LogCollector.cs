using MassEffectModManager;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    class LogCollector
    {
        public static string CollectLogs(string logfile)
        {
            string log = Utilities.ReadLockedTextFile(logfile);
            return log;
        }
    }
}
