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
using ME3TweaksCore.NativeMods;
using ME3TweaksCoreWPF.NativeMods;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects.gametarget;
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
                // We will manually load auxiliary services
                LoadAuxiliaryServices = false,
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
                CanFetchContentThrottleCheck =M3OnlineContent.CanFetchContentThrottleCheck,
                LECPackageSaveFailedCallback = x => M3Log.Error($@"Error saving package: {x}"),
                CreateLogger = M3Log.CreateLogger,
                GenerateInstalledDlcModDelegate = M3InstalledDLCMod.GenerateInstalledDLCMod,
                GenerateInstalledExtraFileDelegate = InstalledExtraFileWPF.GenerateInstalledExtraFileWPF,
                GenerateSFARObjectDelegate = SFARObjectWPF.GenerateSFARObjectWPF,
                GenerateModifiedFileObjectDelegate = ModifiedFileObjectWPF.GenerateModifiedFileObjectWPF,
                GenerateKnownInstalledASIModDelegate = KnownInstalledASIModWPF.GenerateKnownInstalledASIModWPF,
                GenerateUnknownInstalledASIModDelegate = UnknownInstalledASIModWPF.GenerateUnknownInstalledASIModWPF,
                BetaMode = Settings.BetaMode
            };
        }

        public static void AddM3SpecificFixes()
        {
            T2DLocalizationShim.SetupTexture2DLocalizationShim();
        }
    }
}
