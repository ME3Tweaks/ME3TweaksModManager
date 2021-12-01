using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace ME3TweaksModManager.modmanager.me3tweaks
{
    class LibraryBoot
    {
        /// <summary>
        /// Gets the package for ME3TweaksModManager to interface with ME3TweaksCore.
        /// </summary>
        /// <returns></returns>
        public static ME3TweaksCoreLibInitPackage GetPackage()
        {
            return new ME3TweaksCoreLibInitPackage()
            {
                LoadAuxillaryServices = false,
                RunOnUiThreadDelegate = action => Application.Current.Dispatcher.Invoke(action),
                TrackEventCallback = (eventName, properties) => { Analytics.TrackEvent(eventName, properties); },
                TrackErrorCallback = (eventName, properties) => { Crashes.TrackError(eventName, properties); },
                UploadErrorLogCallback = (e, data) =>
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    string log = LogCollector.CollectLatestLog(MCoreFilesystem.GetLogDir(), true);
                    if (log != null && log.Length < FileSize.MebiByte * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, @"applog.txt"));
                    }

                    Crashes.TrackError(e, data);
                },
                LECPackageSaveFailedCallback = x => M3Log.Error($@"Error saving package: {x}"),
                CreateLogger = M3Log.CreateLogger
            };
        }

        public static void AddM3SpecificFixes()
        {
            T2DLocalizationShim.SetupTexture2DLocalizationShim();
        }
    }
}
