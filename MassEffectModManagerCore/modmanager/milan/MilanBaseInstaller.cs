using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3TweaksCore.Localization;
using ME3TweaksCore.NativeMods;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.usercontrols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ME3TweaksModManager.modmanager.usercontrols.OIGODisabler;

namespace ME3TweaksModManager.modmanager.milan
{
    /// <summary>
    /// Sets up the intial base installation for Milan
    /// </summary>
    internal class MilanBaseInstaller
    {
        /// <summary>
        /// Files from Binaries folder
        /// </summary>
        private static readonly string[] BinaryFiles = [
            "amd_ags_x64.dll",
            "bink2w64.dll",
            "MassEffect3.exe",
            "oo2core_8_win64.dll",
            "PhysXCooking64.dll",
            "PhysXCore64.dll",
            "Activation.dll",
            "Activation64.dll",
            "PhysXLoader64.dll"
        ];

        /// <summary>
        /// Files from CookedPCConsole folder. A few extra ones are plucked from METR, but they are hardcoded into the installer.
        /// </summary>
        private static readonly string[] CookedFiles = [
            // Binaries file 

            // Base required files for game boot
            "Core.pcc",
            "Engine.pcc",
            "SFXGame.pcc",
            "Startup.pcc",

            // Game executable directory is already included by default


            // Required for main menu to load
            "EntryMenu.pcc",

            // Texture assets

            // Shader cache

            "AreaMap_NumIcon.pcc",
            "BIOGame_DEU.tlk",
            "BIOGame_ESN.tlk",
            "BIOGame_FRA.tlk",
            "BIOGame_INT.tlk",
            "BIOGame_ITA.tlk",
            "BIOGame_JPN.tlk",
            "BIOGame_POL.tlk",
            "BIOGame_RUS.tlk",
            "BIOG_UIWorld.pcc",
            "Coalesced.bin",
            "Core.pcc",
            "Engine.pcc",
            "EntryMenu.pcc",
            "GameFramework.pcc",
            "GFxUI.pcc",
            "GlobalShaderCache-PC-D3D-SM5.bin",
            "GuidCache.pcc",
            "IpDrv.pcc",
            "RefShaderCache-PC-D3D-SM5.upk",
            "SFXGame.pcc",
            "SFXGUI_A_LegalLogos.pcc",
            "SFXGUI_Fonts.pcc",
            "SFXOnlineFoundation.pcc",
            "Startup.pcc",
            "Textures.tfc",
            "WwiseAudio.pcc",
            "Wwise_Generic_GUI.afc",
            "Wwise_Main_Menu_NonStreaming.afc",
            "Wwise_Main_Menu_Streaming.afc"
            ];

        /// <summary>
        /// Required movies for the game to boot. I am not sure why it needs both startup loops.
        /// </summary>
        private static readonly string[] MovieFiles = [
            "load_f01.bik",
            "StartupLoop.bik",
            "StartupLoop_a.bik",
        ];


        public MilanBaseInstaller()
        {
            // Initialize the base installation for Milan
        }

        private void CopySourceFiles(string sourceDirectory, string[] files, string destGamePath, string subFolder, MBIInterop interop)
        {
            var sourceBasePath = Path.Combine(sourceDirectory, subFolder);
            var destFolder = Path.Combine(destGamePath, subFolder);
            Directory.CreateDirectory(destFolder);
            var sourceFiles = Directory.GetFiles(sourceBasePath, "*.*", SearchOption.AllDirectories)
                .Where(f => files.Contains(Path.GetFileName(f))).ToList();

            foreach (var file in sourceFiles)
            {
                interop.SetStatus?.Invoke(Path.GetFileName(file));

                // Check to see if this is a nested file
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destFolder, fileName);
                var sRelPath = Path.GetRelativePath(sourceBasePath, file);
                var dRelPath = Path.GetRelativePath(destFolder, destFile);
                if (sRelPath != dRelPath)
                {
                    // It's in a subfolder
                    destFile = Path.Combine(destFolder, sRelPath);
                    Directory.GetParent(destFile).Create();
                }

                M3Log.Information($"Copying {file} to {destFile}");
                File.Copy(file, destFile, true);
            }
        }


        /// <summary>
        /// Copies the require files from LE3 to the destination folder. The source folder should be a backup to help ensure mods are not installed, as base install should be "vanilla".
        /// </summary>
        /// <param name="le3SourcePath"></param>
        /// <param name="destinationFolderPath"></param>
        public void CreateInstallation(GameTarget sourceGameTarget, string destinationFolderPath, MBIInterop interop)
        {
            bool successful = true;
            string errorMessage = null;
            // Copy the source files over.
            try
            {
                CopySourceFiles(sourceGameTarget.TargetPath, BinaryFiles, destinationFolderPath, "Binaries", interop);
                CopySourceFiles(sourceGameTarget.TargetPath, CookedFiles, destinationFolderPath, "BioGame\\CookedPCConsole", interop);
                CopySourceFiles(sourceGameTarget.TargetPath, MovieFiles, destinationFolderPath, "BioGame\\Movies", interop);

                // Create stub directories, some of our tooling requires them to exist, even if they're empty.
                // Required by AutoTOC. I don't want to have to add a folder check to this, so we make it exist
                Directory.CreateDirectory(Path.Combine(destinationFolderPath, @"Engine", @"Shaders")); 


                // Patch to shrink them?

                // Unapply bink. This will be a common problem, so we can just handle it here.

                // Change to LMP someday.
                var lmpTarget = new GameTargetWPF(MEGame.LE3, destinationFolderPath, false, skipInit: true);
                lmpTarget.UninstallBinkBypass();

                // TOC game so game can find files.
                AutoTOC.RunTOCOnGameTarget(lmpTarget);

#if DEBUG
                // Debug only for now!
                interop?.AddTarget(lmpTarget);

                // This makes it easier to debug 
                lmpTarget.InstallBinkBypass(false);
                ASIManager.InstallASIToTargetByGroupID(ASIModUpdateGroupID.LE3_DebugLogger, @"DebugLogger", lmpTarget);
#endif
            }
            catch (Exception e)
            {
                M3Log.Exception(e, $@"Error building base milan installation:");
                successful = false;
                errorMessage = e.Message;
            }
            finally
            {
                interop?.InstallationFinished(successful, errorMessage);
            }
        }
    }
}
