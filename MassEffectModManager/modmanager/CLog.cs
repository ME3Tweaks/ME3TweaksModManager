using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManager.modmanager
{
    /// <summary>
    /// Conditional logging extension for Serilog. Pass a bool that will be evaluated. If the bool passed is true, the log item will be written.
    /// </summary>
    public static class CLog
    {
        public static void Information(string message, bool condition)
        {
            if (condition)
            {
                Log.Information(message);
            }
        }

        public static void Warning(string message, bool condition)
        {
            if (condition)
            {
                Log.Warning(message);
            }
        }

        public static void Error(string message, bool condition)
        {
            if (condition)
            {
                Log.Error(message);
            }
        }
    }
}
