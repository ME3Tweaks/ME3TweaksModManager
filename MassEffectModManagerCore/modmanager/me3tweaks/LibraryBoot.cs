using System.Windows;
using ME3TweaksCore;
using ME3TweaksCore.Diagnostics;
using ME3TweaksCore.Helpers;
using ME3TweaksCoreWPF.NativeMods;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.me3tweakscoreextended;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects.gametarget;
using ME3TweaksModManager.modmanager.telemetry;

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
                TrackEventCallback = M3OpenTelemetry.TrackEvent,
                TrackErrorCallback = M3OpenTelemetry.TrackError,
                UploadErrorLogCallback = (e, data) =>
                {
                    // This uses gating to to improve performance - log collection doesn't need to happen if its disabled
                    if (Settings.CanSendTelemetry)
                    {
                        var properties = data != null ? new Dictionary<string, string>(data) : new Dictionary<string, string>();
                        string log = LogCollector.CollectLatestLog(MCoreFilesystem.GetLogDir(), true);
                        if (log != null)
                        {
                            const int maxLogLength = 8192; // 8192 chars seems pretty small...
                            properties[@"log"] = log.Length > maxLogLength ? log.Substring(log.Length - maxLogLength) : log;
                        }
                        M3OpenTelemetry.TrackError(e, properties);
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
