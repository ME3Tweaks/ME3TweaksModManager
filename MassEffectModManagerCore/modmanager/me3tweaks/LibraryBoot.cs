using System.Windows;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.NativeMods;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.me3tweakscoreextended;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects.gametarget;

#if WITH_APPCENTER
using Microsoft.AppCenter.Crashes;
#endif

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
                // This uses just EnableTelemetry as it uses the queue system which will check if the telemetry witholding gate has been witheld.
                TrackEventCallback = (eventName, properties) => { if (Settings.EnableTelemetry) { App.SubmitAnalyticTelemetryEvent(eventName, properties); } },
                // This uses CanSendTelemetry to ensure gating any bootup telemetry
                TrackErrorCallback = (eventName, properties) =>
                {
                    if (Settings.CanSendTelemetry)
                    {
                        // Microsoft shut down AppCenter March 2025
                        // TelemetryInterposer.TrackError(eventName, properties);
                    }
                },
                UploadErrorLogCallback = (e, data) =>
                {
                    if (Settings.CanSendTelemetry)
                    {
#if WITH_APPCENTER
                        var attachments = new List<ErrorAttachmentLog>();
                        string log = LogCollector.CollectLatestLog(MCoreFilesystem.GetLogDir(), true);
                        if (log != null && log.Length < FileSize.MebiByte * 7)
                        {
                            attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, @"applog.txt"));
                        }

                        // We directly send to Crashes
                        Crashes.TrackError(e, data);
#endif
                    }
                },
                CanFetchContentThrottleCheck = M3OnlineContent.CanFetchContentThrottleCheck,
                LECPackageSaveFailedCallback = x => M3Log.Error($@"Error saving package: {x}"),
                CreateLogger = M3Log.CreateLogger,
                GetLogger = M3Log.GetLogger,
                GenerateInstalledDlcModDelegate = M3InstalledDLCMod.GenerateInstalledDLCMod,
                GenerateInstalledExtraFileDelegate = InstalledExtraFileWPF.GenerateInstalledExtraFileWPF,
                GenerateSFARObjectDelegate = SFARObjectWPF.GenerateSFARObjectWPF,
                GenerateModifiedFileObjectDelegate = M3ModifiedFileObject.GenerateModifiedFileObject,
                GenerateKnownInstalledASIModDelegate = KnownInstalledASIModWPF.GenerateKnownInstalledASIModWPF,
                GenerateUnknownInstalledASIModDelegate = UnknownInstalledASIModWPF.GenerateUnknownInstalledASIModWPF,
                BetaMode = Settings.BetaMode,
                InitialLanguage = App.InitialLanguage,
                LoadBuildInfo = false // We already did this
            };
        }

        public static void AddM3SpecificFixes()
        {
            T2DLocalizationShim.SetupTexture2DLocalizationShim();
        }
    }
}
