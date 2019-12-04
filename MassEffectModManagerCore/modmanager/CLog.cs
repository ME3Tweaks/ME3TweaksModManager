using Serilog;
using System.ComponentModel;

namespace MassEffectModManagerCore.modmanager
{
    /// <summary>
    /// Conditional logging extension for Serilog. Pass a bool that will be evaluated. If the bool passed is true, the log item will be written.
    /// This class is used to reduce code a log clutter with if statements for conditional logging and unnecessary verbosity.
    /// </summary>
    [Localizable(false)]
    public static class CLog
    {
        /// <summary>
        /// Conditionally logs the indicated message with an Information level if the passed in boolean is true.
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="condition">Condition that must be true to log</param>
        public static void Information(string message, bool condition)
        {
            if (condition)
            {
                Log.Information(message);
            }
        }

        /// <summary>
        /// Conditionally logs the indicated message with a Warning level if the passed in boolean is true.
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="condition">Condition that must be true to log</param>
        public static void Warning(string message, bool condition)
        {
            if (condition)
            {
                Log.Warning(message);
            }
        }

        /// <summary>
        /// Conditionally logs the indicated message with a Error level if the passed in boolean is true.
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="condition">Condition that must be true to log</param>
        public static void Error(string message, bool condition)
        {
            if (condition)
            {
                Log.Error(message);
            }
        }
    }
}
