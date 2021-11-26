using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using MassEffectModManagerCore.modmanager.diagnostics;
using ME3TweaksCore.Diagnostics;
using Microsoft.AppCenter.Crashes;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    /// <summary>
    /// Class that helps initialize the ME3TweaksCore library
    /// </summary>
    class ME3TweaksCoreInitializer
    {
        /// <summary>
        /// Handler for when an exception telemetry event should be submitted and include a log for more context.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="data"></param>
        private static void UploadLogWithAttachment(Exception e, Dictionary<string, string> data)
        {
            var attachments = new List<ErrorAttachmentLog>();
            string log = LogCollector.CollectLatestLog(M3Log.LogDir, true);
            if (log != null && log.Length < FileSize.MebiByte * 7)
            {
                attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, @"applog.txt"));
            }

            Crashes.TrackError(e, data);
        }
    }
}
