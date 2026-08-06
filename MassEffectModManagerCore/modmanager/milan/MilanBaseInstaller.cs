using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3TweaksCore.Localization;
using ME3TweaksCore.NativeMods;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.installer;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols;

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

            // Gestures (animations, especially in menus)
            "GesturesConfig.pcc",

            // Game executable directory is already included by default



            // Required for main menu to load
            "EntryMenu.pcc",

            // Beleive it or not this file is required for MP map transitions
            // gonna guess it's the transition map. Without it, game dies transitioning MP maps
            "entry.pcc",

            // Required for MP to not crash and die
            "SFXGUI_WeaponUIDataSF.pcc",

            // Basegame Weapons
            "SFXWeapon_AssaultRifle_Argus.pcc",
            "SFXWeapon_AssaultRifle_Avenger.pcc",
            "SFXWeapon_AssaultRifle_Cobra.pcc",
            "SFXWeapon_AssaultRifle_Collector.pcc",
            "SFXWeapon_AssaultRifle_Falcon.pcc",
            "SFXWeapon_AssaultRifle_Geth.pcc",
            "SFXWeapon_AssaultRifle_Mattock.pcc",
            "SFXWeapon_AssaultRifle_Reckoning.pcc",
            "SFXWeapon_AssaultRifle_Revenant.pcc",
            "SFXWeapon_AssaultRifle_Saber.pcc",
            "SFXWeapon_AssaultRifle_Valkyrie.pcc",
            "SFXWeapon_AssaultRifle_Vindicator.pcc",
            "SFXWeapon_Pistol_Carnifex.pcc",
            "SFXWeapon_Pistol_Eagle.pcc",
            "SFXWeapon_Pistol_EndGameCarnifex.pcc",
            "SFXWeapon_Pistol_Ivory.pcc",
            "SFXWeapon_Pistol_Phalanx.pcc",
            "SFXWeapon_Pistol_Predator.pcc",
            "SFXWeapon_Pistol_Scorpion.pcc",
            "SFXWeapon_Pistol_Talon.pcc",
            "SFXWeapon_Pistol_Thor.pcc",
            "SFXWeapon_Shotgun_Claymore.pcc",
            "SFXWeapon_Shotgun_Crusader.pcc",
            "SFXWeapon_Shotgun_Disciple.pcc",
            "SFXWeapon_Shotgun_Eviscerator.pcc",
            "SFXWeapon_Shotgun_Geth.pcc",
            "SFXWeapon_Shotgun_Graal.pcc",
            "SFXWeapon_Shotgun_Katana.pcc",
            "SFXWeapon_Shotgun_Raider.pcc",
            "SFXWeapon_Shotgun_Scimitar.pcc",
            "SFXWeapon_Shotgun_Striker.pcc",
            "SFXWeapon_SMG_Hornet.pcc",
            "SFXWeapon_SMG_Hurricane.pcc",
            "SFXWeapon_SMG_Locust.pcc",
            "SFXWeapon_SMG_Shuriken.pcc",
            "SFXWeapon_SMG_Tempest.pcc",
            "SFXWeapon_SniperRifle_BlackWidow.pcc",
            "SFXWeapon_SniperRifle_Incisor.pcc",
            "SFXWeapon_SniperRifle_Indra.pcc",
            "SFXWeapon_SniperRifle_Javelin.pcc",
            "SFXWeapon_SniperRifle_Mantis.pcc",
            "SFXWeapon_SniperRifle_Raptor.pcc",
            "SFXWeapon_SniperRifle_Valiant.pcc",
            "SFXWeapon_SniperRifle_Viper.pcc",
            "SFXWeapon_SniperRifle_Widow.pcc",
            
            // Basegame Weapon Mods
            "SFXWEaponMods_AssaultRifles.pcc",
            "SFXWEaponMods_Pistols.pcc",
            "SFXWEaponMods_Shotguns.pcc",
            "SFXWEaponMods_SMGs.pcc",
            "SFXWEaponMods_SniperRifles.pcc",

            // Basegame MP
            "SFXPawn_Atlas.pcc",
            "SFXPawn_Banshee.pcc",
            "SFXPawn_Brute.pcc",
            "SFXPawn_Cannibal.pcc",
            "SFXPawn_Centurion.pcc",
            "SFXPawn_GethHunter.pcc",
            "SFXPawn_GethPrime.pcc",
            "SFXPawn_GethPyro.pcc",
            "SFXPawn_GethRocketTrooper.pcc",
            "SFXPawn_GethTrooper.pcc",
            "SFXPawn_Guardian.pcc",
            "SFXPawn_Gunner.pcc",
            "SFXPawn_Harvester.pcc",
            "SFXPawn_Husk.pcc",
            "SFXPawn_Marauder.pcc",
            "SFXPawn_Nemesis.pcc",
            "SFXPawn_Phantom.pcc",
            "SFXPawn_Ravager.pcc",
            "SFXPawn_Trooper.pcc",

            // Audio - Enemies
            "ss_cerberus_trooper_S_DEU.afc",
            "ss_cerberus_trooper_S_FRA.afc",
            "ss_cerberus_trooper_S_Int.afc",
            "ss_cerberus_trooper_S_ITA.afc",
            "ss_exertion_palette_D_DEU.afc",
            "ss_exertion_palette_D_FRA.afc",
            "ss_exertion_palette_D_Int.afc",
            "ss_exertion_palette_D_ITA.afc",
            "Wwise_Crt_Cerb_Nem_SFX.afc",
            "Wwise_Crt_Cerb_Phtm_SFX.afc",
            "Wwise_Crt_Geth_Hunter_SFX.afc",
            "Wwise_Crt_Geth_Prime_SFX.afc",
            "Wwise_Crt_Geth_Pyro_SFX.afc",
            "Wwise_Crt_Geth_Trooper_SFX.afc",
            "Wwise_Crt_Reap_Banshee_SFX.afc",
            "Wwise_Crt_Reap_Brute_SFX.afc",
            "Wwise_Crt_Reap_Cann_SFX.afc",
            "Wwise_Crt_Reap_Harvester_SFX.afc",
            "Wwise_Crt_Reap_Husk_SFX.afc",
            "Wwise_Crt_Reap_Marauder_SFX.afc",
            "wwise_crt_reap_ravenger_sfx.afc",
            "Wwise_Crt_Reap_Swarmer_SFX.afc",



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

        /// <summary>
        /// Names of mods that are considered 'Project Milan' and are used to install the MP component to LMP
        /// </summary>
        public static readonly string[] MilanModNames = [
            // In shipping version there should be only one item
            // In development there can be multiple
            @"Project Milan",
            // @"Multiplayer component" // Set to name of final mod as shown in M3
        ];

        /// <summary>
        /// Returns if there is a Project Milan mod in the library. Returns the highest version number if multiple are found, if the highest have the same, then the first alphabetical will be used.
        /// </summary>
        /// <returns></returns>
        public static Mod GetMilanMod()
        {
            var lmpMods = M3LoadedMods.GetModsForGame(MEGame.LMP);
            var milanMods = lmpMods.Where(x => MilanBaseInstaller.MilanModNames.Contains(x.ModName)).OrderByDescending(x => x.ParsedModVersion).ToList();
            return milanMods.FirstOrDefault();
        }


        /** The interop used to communicate back to a UI about the install */
        private MBIInterop interop;

        public MilanBaseInstaller()
        {
            // Initialize the base installation for Milan
        }

        private void CopySourceFiles(string sourceDirectory, string[] files, string destGamePath, string subFolder, params string[] extraPatterns)
        {
            var sourceBasePath = Path.Combine(sourceDirectory, subFolder);
            var destFolder = Path.Combine(destGamePath, subFolder);
            Directory.CreateDirectory(destFolder);
            var sourceFiles = Directory.GetFiles(sourceBasePath, "*.*", SearchOption.AllDirectories)
                .Where(f => files.Contains(Path.GetFileName(f))).ToList();

            foreach (var pattern in extraPatterns)
            {
                sourceFiles.AddRange(Directory.GetFiles(sourceBasePath, pattern, SearchOption.AllDirectories));
            }

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

                // Copy localizations
                CopyLocalizationsOfFile(file, destFolder);
            }
        }

        public static readonly MELocalization[] SupportedVOLanguages = new[]
        {
            MELocalization.INT,
            MELocalization.FRA,
            MELocalization.DEU,
            MELocalization.ITA,
            // Any other languages that had VO?
        };


        private void CopyLocalizationsOfFile(string file, string destFolder)
        {
            var dir = Directory.GetParent(file).FullName;
            foreach (var loc in SupportedVOLanguages)
            {
                // LOC_
                var name = file.SetUnrealLocalization(MEGame.LMP, loc, true);
                var testPath = Path.Combine(dir, name + @".pcc");
                if (File.Exists(testPath))
                {
                    var outPath = Path.Combine(destFolder, name);
                    File.Copy(testPath, outPath);
                }

                // Straight loc
                name = file.SetUnrealLocalization(MEGame.LMP, loc, false);
                testPath = Path.Combine(dir, name + @".pcc");
                if (File.Exists(testPath))
                {
                    var outPath = Path.Combine(destFolder, name);
                    File.Copy(testPath, outPath);
                }
            }
        }

        // UI STRING UPDATES FOR INSTALLER ========



        // END UI STRING UPDATES FOR INSTALLER=====


        /// <summary>
        /// Copies the require files from LE3 to the destination folder. The source folder should be a backup to help ensure mods are not installed, as base install should be "vanilla".
        /// </summary>
        /// <param name="le3SourcePath"></param>
        /// <param name="destinationFolderPath"></param>
        public void CreateInstallation(GameTarget sourceGameTarget, string destinationFolderPath, MBIInterop interop)
        {
            this.interop = interop;

            bool successful = true;
            string errorMessage = null;
            // Copy the source files over.
            try
            {
                CopySourceFiles(sourceGameTarget.TargetPath, BinaryFiles, destinationFolderPath, "Binaries");
                CopySourceFiles(sourceGameTarget.TargetPath, CookedFiles, destinationFolderPath, "BioGame\\CookedPCConsole", "*.tfc");
                CopySourceFiles(sourceGameTarget.TargetPath, MovieFiles, destinationFolderPath, "BioGame\\Movies");

                // Create stub directories, some of our tooling requires them to exist, even if they're empty.
                // Required by AutoTOC. I don't want to have to add a folder check to this, so we make it exist
                Directory.CreateDirectory(Path.Combine(destinationFolderPath, @"Engine", @"Shaders"));


                // Install Project Milan
                var lmpTarget = new GameTargetWPF(MEGame.LMP, destinationFolderPath, true);
                var milanmod = GetMilanMod();
                var optionsPackage = new ModInstallOptionsPackage()
                {
                    InstallTarget = lmpTarget,
                    ModBeingInstalled = milanmod,
                    SkipPrerequesitesCheck = true
                };

                // We have no options, set it as having none chosen
                optionsPackage.SetNoOptions();

                interop.SetStatus?.Invoke("Installing Project Milan...");
                ModInstaller mi = new ModInstaller(optionsPackage);
                mi.SetPercent = setPercent;
                mi.SetAction = setAction;
                mi.SetPercentVisibility = setPercentVisibility;
                mi.InstallMod();

                // Patch to shrink them?

                // Unapply bink. This will be a common problem, so we can just handle it here.
                // EARLY DEV - Make LE3 target so we can use LE3 ASIs
                var le3Target = new GameTargetWPF(MEGame.LE3, destinationFolderPath, false, skipInit: true);
                le3Target.UninstallBinkBypass();

                // TOC game so game can find files.
                AutoTOC.RunTOCOnGameTarget(le3Target);

#if DEBUG
                // Debug only for now!
                interop?.AddTarget(le3Target);

                // This makes it easier to debug 
                le3Target.InstallBinkBypass(false);
                ASIManager.InstallASIToTargetByGroupID(ASIModUpdateGroupID.LE3_DebugLogger, @"DebugLogger", le3Target);
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

        #region ModInstaller UI interface
        private bool percentVisible;
        private string action;
        private int percent;


        private void setPercentVisibility(bool visible)
        {
            percentVisible = visible;
            updateInstallText();
        }

        private void setAction(string action)
        {
            this.action = action;
        }

        private void setPercent(int percent)
        {
            this.percent = percent;
            updateInstallText();
        }

        private void updateInstallText()
        {
            string message = action;
            if (percentVisible)
            {
                message += $@" {percent}%";
            }

            // Set the message
            interop.SetStatus?.Invoke(message);
        }
    }
        #endregion
}
