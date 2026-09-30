using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.installer;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ME3TweaksModManager.modmanager.milan
{
    /// <summary>
    /// Sets up the intial base installation for Milan
    /// </summary>
    internal class MilanBaseInstaller
    {


        #region Files To Copy
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
            // Base required files for game boot
            "Core.pcc",
            "Engine.pcc",
            "SFXGame.pcc",
            "Startup.pcc",
            "Coalesced.bin",
            "GlobalShaderCache-PC-D3D-SM5.bin",
            "IpDrv.pcc",
            "RefShaderCache-PC-D3D-SM5.upk",
            "SFXOnlineFoundation.pcc",
            "WwiseAudio.pcc",
            "GuidCache.pcc", // Installed by Milan

            // Gestures (animations, especially in menus)
            "GesturesConfig.pcc",
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
            
            // Basegame MP Level Files (excluding BioP which break things)
            // Includes level audio
            "BioA_MPCer.pcc",
            "BioA_MPCer_White.pcc",
            "BioA_MPDish.pcc",
            "BioA_MPDish_000_BG.pcc",
            "BioA_MPDish_200.pcc",
            "BioA_MPDish_300.pcc",
            "BioA_MPDish_300_rails.pcc",
            "BioA_MPDish_400.pcc",
            "BioA_MPNov.pcc",
            "BioA_MPNov_000_BG.pcc",
            "BioA_MPNov_100Exterior.pcc",
            "BioA_MPNov_White.pcc",
            "BioA_MPRctr.pcc",
            "BioA_MPRctr_100Cover.pcc",
            "BioA_MPSlum_100.pcc",
            "BioA_MPSlum_200.pcc",
            "BioA_MPSlum_400TempVista.pcc",
            "BioA_MPTowr.pcc",
            "BioA_MPTowr_200.pcc",
            "BioA_MPTowr_300_UI_Lights.pcc",
            "BioD_MPCer.pcc",
            "BioD_MPDish.pcc",
            "BioD_MPNov.pcc",
            "BioD_MPRctr.pcc",
            "BioD_MPSlum.pcc",
            "BioD_MPTowr.pcc",
            "BioSnd_MPCer.pcc",
            "BioSnd_MPDish.pcc",
            "BioSnd_MPNov.pcc",
            "BioSnd_MPRctr.pcc",
            "BioSnd_MPSlum.pcc",
            "BioSnd_MPTowr.pcc",
            "wwise_mpcer_streaming.afc",
            "wwise_mpdish_streaming.afc",
            "wwise_mpnov_streaming.afc",
            "wwise_mprctr_streaming.afc",
            "Wwise_MPSlum_NonStreaming.afc",
            "wwise_mpslum_streaming.afc",
            "Wwise_MPTowr_NonStreaming.afc",
            "Wwise_MPTowr_Streaming.afc",

            // Basegame Weapon Mods
            "SFXWeaponMods_AssaultRifles.pcc",
            "SFXWeaponMods_Pistols.pcc",
            "SFXWeaponMods_Shotguns.pcc",
            "SFXWeaponMods_SMGs.pcc",
            "SFXWeaponMods_SniperRifles.pcc",

            // Gui Assets for weapons
            "SFXGUI_WMod_AssaultRifleAccuracy.pcc",
            "SFXGUI_WMod_AssaultRifleDamage.pcc",
            "SFXGUI_WMod_PistolAccuracy.pcc",
            "SFXGUI_WMod_PistolDamage.pcc",
            "SFXGUI_WMod_PistolStability.pcc",
            "SFXGUI_WMod_ShotgunDamage.pcc",
            "SFXGUI_WMod_ShotgunMeleeDamage.pcc",
            "SFXGUI_WMod_SMGAccuracy.pcc",
            "SFXGUI_WMod_SMGDamage.pcc",
            "SFXGUI_WMod_SniperRifleAccuracy.pcc",
            "SFXGUI_WMod_SniperRifleDamage.pcc",
            "SFXGUI_W_A_Argus.pcc",
            "SFXGUI_W_A_Avenger.pcc",
            "SFXGUI_W_A_Cobra.pcc",
            "SFXGUI_W_A_Collector.pcc",
            "SFXGUI_W_A_Falcon.pcc",
            "SFXGUI_W_A_Geth.pcc",
            "SFXGUI_W_A_Mattock.pcc",
            "SFXGUI_W_A_Reckoning.pcc",
            "SFXGUI_W_A_Revenant.pcc",
            "SFXGUI_W_A_Saber.pcc",
            "SFXGUI_W_A_Valkyrie.pcc",
            "SFXGUI_W_A_Vindicator.pcc",
            "SFXGUI_W_H_ArcProjector.pcc",
            "SFXGUI_W_H_Avalanche.pcc",
            "SFXGUI_W_H_Blackstorm.pcc",
            "SFXGUI_W_H_Cain.pcc",
            "SFXGUI_W_H_Flamethrower_Player.pcc",
            "SFXGUI_W_H_GrenadeLauncher.pcc",
            "SFXGUI_W_H_MiniGun.pcc",
            "SFXGUI_W_H_MissileLauncher.pcc",
            "SFXGUI_W_H_ParticleBeam.pcc",
            "SFXGUI_W_H_RocketLauncher.pcc",
            "SFXGUI_W_H_TitanMissileLauncher.pcc",
            "SFXGUI_W_P_Carnifex.pcc",
            "SFXGUI_W_P_Eagle.pcc",
            "SFXGUI_W_P_Ivory.pcc",
            "SFXGUI_W_P_Phalanx.pcc",
            "SFXGUI_W_P_Predator.pcc",
            "SFXGUI_W_P_Scorpion.pcc",
            "SFXGUI_W_P_Talon.pcc",
            "SFXGUI_W_P_Thor.pcc",
            "SFXGUI_W_SG_Claymore.pcc",
            "SFXGUI_W_SG_Crusader.pcc",
            "SFXGUI_W_SG_Disciple.pcc",
            "SFXGUI_W_SG_Eviscerator.pcc",
            "SFXGUI_W_SG_Geth.pcc",
            "SFXGUI_W_SG_Graal.pcc",
            "SFXGUI_W_SG_Katana.pcc",
            "SFXGUI_W_SG_Raider.pcc",
            "SFXGUI_W_SG_Scimitar.pcc",
            "SFXGUI_W_SG_Striker.pcc",
            "SFXGUI_W_SMG_Hornet.pcc",
            "SFXGUI_W_SMG_Hurricane.pcc",
            "SFXGUI_W_SMG_Locust.pcc",
            "SFXGUI_W_SMG_Shuriken.pcc",
            "SFXGUI_W_SMG_Tempest.pcc",
            "SFXGUI_W_S_BlackWidow.pcc",
            "SFXGUI_W_S_Incisor.pcc",
            "SFXGUI_W_S_Indra.pcc",
            "SFXGUI_W_S_Javelin.pcc",
            "SFXGUI_W_S_Mantis.pcc",
            "SFXGUI_W_S_Raptor.pcc",
            "SFXGUI_W_S_Valiant.pcc",
            "SFXGUI_W_S_Viper.pcc",
            "SFXGUI_W_S_Widow.pcc",

            // GUI weapon packages referenced by SFXGUI_WeaponUIDataDLC_CON_MPMerged.Data
            "SFXGUI_W_A_Cerb_GUN01.pcc",
            "SFXGUI_W_A_Krogan_GUN02.pcc",
            "SFXGUI_W_A_Lancer.pcc",
            "SFXGUI_W_A_LMG_GUN02.pcc",
            "SFXGUI_W_A_Prothean.pcc",
            "SFXGUI_W_A_Quarian.pcc",
            "SFXGUI_W_A_Spitfire.pcc",
            "SFXGUI_W_P_Asari_GUN02.pcc",
            "SFXGUI_W_P_Bloodpack.pcc",
            "SFXGUI_W_P_Silencer.pcc",
            "SFXGUI_W_S_Batarian_GUN02.pcc",
            "SFXGUI_W_S_Collector.pcc",
            "SFXGUI_W_S_Turian_GUN01.pcc",
            "SFXGUI_W_SG_Assault_GUN02.pcc",
            "SFXGUI_W_SG_Quarian_GUN01.pcc",
            "SFXGUI_W_SG_Salarian.pcc",
            "SFXGUI_W_SMG_Bloodpack.pcc",
            "SFXGUI_W_SMG_Collector.pcc",
            "SFXGUI_W_SMG_Geth_GUN01.pcc",

            // GUI assets for weapons and other stuff
            "SFXNotificationImages_AssaultRifles.pcc",
            "SFXNotificationImages_GAW_1.pcc",
            "SFXNotificationImages_GAW_2.pcc",
            "SFXNotificationImages_HeavyPistols.pcc",
            "SFXNotificationImages_HeavyWeapons.pcc",
            "SFXNotificationImages_Shotguns.pcc",
            "SFXNotificationImages_SMGs.pcc",
            "SFXNotificationImages_SniperRifles.pcc",
            "SFXNotification_ModLarge.pcc",
            "SFXNotification_WeaponUpgrades.pcc",

            // Pawns
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
            "VocalizationBanks.afc", // Also has localizations
            "ss_cerberus_trooper_S.afc", // Localized
            "ss_exertion_palette_D.afc", // Localized
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
            "DLC_EXP_Pack003_Base_1_SFX.afc", // Collectors
            "DLC_EXP_Pack003_Base_2_SFX.afc", // Collectors
            "DLC_EXP_Pack003_Base_3_SFX.afc", // Collectors

            // Audio - Weapons
            // Needs merged in some kind of AFC compaction pass.
            "DLC_CON_GUN01_1_SFX.afc",
            "DLC_CON_GUN01_2_SFX.afc",
            "DLC_CON_GUN01_3_SFX.afc",
            "DLC_CON_GUN02_1_SFX.afc",
            "DLC_EXP_Pack003_Base_SFX.afc",
            "DLC_EXP_Pack003_1_SFX.afc",
            "DLC_EXP_Pack003_2_SFX.afc",
            "DLC_EXP_Pack003_3_SFX.afc",

            // Misc
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
            "GameFramework.pcc",
            "GFxUI.pcc",
            "SFXGUI_A_LegalLogos.pcc",
            "SFXGUI_Fonts.pcc",
            "SFXGUI_FontsJPN.pcc",
            "Textures.tfc",
            // Required for weapon previews
            "Textures_DLC_CON_GUN01.tfc",
            "Textures_DLC_CON_GUN02.tfc",
            "Textures_DLC_EXP_Pack003.tfc", // Lancer, Silencer
            "Textures_DLC_HEN_PR.tfc", // PPR

            // Other audio
            "Wwise_VO_Exertions.afc", // Contains MP-specific stuff
            "Wwise_GUI_MultiPlayer_Specific.afc", // MP Promotion
            "Wwise_Generic_Explosions.afc",
            "Wwise_Generic_Foley.afc",
            "Wwise_Generic_GUI.afc",
            "Wwise_Generic_Gameplay.afc",
            "Wwise_Main_Menu_NonStreaming.afc",
            "Wwise_Main_Menu_Streaming.afc",
            "Wwise_Power_Tech_Overload.afc",
            "Wwise_Power_Biotic_DarkChan.afc",
            "Wwise_Weapons_S_Quarian.afc",

            // Main Menu Audio (might strip later)
            "Wwise_Smls_NorfTL.afc",
            "Wwise_Generic_Galaxy_Map.afc",
            "Wwise_Generic_Scripting.afc",
            "Wwise_Smls_CaptainsCabin.afc",
        ];

        private static readonly string[] Patch02Files = [
            // Patch02
            "SFXGUI_FontsJPN_DLC.pcc",
            "SFXGUI_Fonts_DLC.pcc",
        ];

        /// <summary>
        /// Required movies for the game to boot. I am not sure why it needs both startup loops.
        /// </summary>
        private static readonly string[] MovieFiles = [
            "load_f01.bik",
            "StartupLoop.bik",
            "StartupLoop_a.bik",
        ];

        #endregion

        #region Manifests
        // This must go after Files to copy or Files will be null
        private static readonly CopyManifest[] installManifests = new[]
        {
            // Copy main binary dependencies
            new CopyManifest(
                SourceSubFolder: "Binaries",
                DestSubFolder: "Binaries",
                Files: BinaryFiles
            ),

            // Base CookedPCConsole assets + TFC textures
            new CopyManifest(
                SourceSubFolder: @"BioGame\CookedPCConsole",
                DestSubFolder: @"BioGame\CookedPCConsole",
                Files: CookedFiles,
                UseRelativePathing: false,
                ExtraPatterns: [ "*.tfc" ]
            ),

            // Re-route DLC Patch02 files into BioGame/CookedPCConsole
            new CopyManifest(
                SourceSubFolder: @"BioGame\DLC\DLC_UPD_Patch02\CookedPCConsole",
                DestSubFolder: @"BioGame\CookedPCConsole",
                UseRelativePathing: false,
                Files: Patch02Files
            ),

            // Startup & Loading Movies
            new CopyManifest(
                SourceSubFolder: @"BioGame\Movies",
                DestSubFolder: @"BioGame\Movies",
                UseRelativePathing: false,
                Files: MovieFiles
            )
        };

        #endregion

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
        private string sourceRootPath;
        private string sourceBackupPath;

        public MilanBaseInstaller()
        {
            // Initialize the base installation for Milan
        }

        #region MilanDelta manifest

        private const string MilanDeltaFolderName = "MilanDelta";
        private const string MilanDeltaManifestFileName = "manifest.json";
        private const double SupportedMilanDeltaVersion = 0.1;

        private enum MilanDeltaVerificationFailureKind
        {
            SizeMismatch,
            HashMismatch
        }

        private sealed class MilanDeltaVerificationException : Exception
        {
            public MilanDeltaVerificationException(MilanDeltaVerificationFailureKind failureKind, string message)
                : base(message)
            {
                FailureKind = failureKind;
            }

            public MilanDeltaVerificationFailureKind FailureKind { get; }
        }

        /// <summary>
        /// Folder containing the MilanDelta manifest and its patch files.
        /// </summary>
        private string milanDeltaFolder;

        /// <summary>
        /// Delta entries from the MilanDelta manifest, keyed by destination filename.
        /// </summary>
        private Dictionary<string, MilanDeltaFileEntry> milanDeltaLookup;

        /// <summary>
        /// Loads and verifies the MilanDelta manifest shipped with the Project Milan mod. Throws if the
        /// manifest is missing, unparsable, of an unsupported version, or its signature does not match
        /// its contents - any of these mean the build is invalid and installation must not continue.
        /// </summary>
        private void LoadMilanDeltaManifest(Mod milanMod)
        {
            var deltaFolder = Path.Combine(milanMod.ModPath, MilanDeltaFolderName);
            var manifestPath = Path.Combine(deltaFolder, MilanDeltaManifestFileName);
            if (!File.Exists(manifestPath))
            {
                throw new Exception($"Project Milan mod is missing its {MilanDeltaFolderName} manifest ({MilanDeltaManifestFileName}). This is not a valid Project Milan build.");
            }

            MilanDeltaManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<MilanDeltaManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to parse {MilanDeltaManifestFileName}: {e.Message}");
            }

            if (manifest == null || !manifest.Milan)
            {
                throw new Exception($"{MilanDeltaManifestFileName} is not a valid Milan delta manifest.");
            }

            if (manifest.Version != SupportedMilanDeltaVersion)
            {
                throw new Exception($"{MilanDeltaManifestFileName} has unsupported version {manifest.Version} (expected {SupportedMilanDeltaVersion}).");
            }

            // Verify the manifest hasn't been corrupted or tampered with: recompute the signature over
            // the exact same compact {milan, version, deltas} body that was originally signed.
            var bodyJson = JsonSerializer.Serialize<MilanDeltaSignableBody>(manifest);
            var computedSignature = MUtilities.CalculateHash(Encoding.UTF8.GetBytes(bodyJson), "sha256");
            if (!string.Equals(computedSignature, manifest.Signature, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"{MilanDeltaManifestFileName} signature does not match its contents. This build may be corrupted or tampered with.");
            }

            milanDeltaFolder = deltaFolder;
            milanDeltaLookup = manifest.Deltas.ToDictionary(f => f.Filename, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Applies a MilanDelta JPatch delta to <paramref name="sourceFile"/> and writes the resulting
        /// package, recompressed, to <paramref name="destFile"/>. The delta is diffed against the
        /// decompressed package data, so the source is decompressed before patching and the result is
        /// recompressed afterwards; both the input and the patched output are verified against the
        /// manifest's recorded size and SHA-256 hash before being trusted.
        /// </summary>
        private void ApplyMilanDelta(string sourceFile, string destFile, MilanDeltaFileEntry entry)
        {
            var sourceBytes = File.ReadAllBytes(sourceFile);
            using var sourceStream = new MemoryStream(sourceBytes);
            using var decompressedStream = MEPackage.GetDecompressedPackageStream(sourceStream);
            decompressedStream.Position = 0;

            VerifySizeAndHash(decompressedStream.Length, () => MUtilities.CalculateHash(decompressedStream, "sha256"),
                entry.SourceSize, entry.SourceHash, entry.Filename, "source");

            var patchPath = Path.Combine(milanDeltaFolder, entry.Delta);
            if (!File.Exists(patchPath))
            {
                throw new Exception($"Milan delta patch file is missing: {patchPath}");
            }

            decompressedStream.Position = 0;
            using var patchStream = File.OpenRead(patchPath);
            using var patchedStream = new MemoryStream();
            JPatch.ApplyJPatch(decompressedStream, patchStream, patchedStream);

            VerifySizeAndHash(patchedStream.Length, () => MUtilities.CalculateHash(patchedStream, "sha256"),
                entry.DestSize, entry.DestHash, entry.Filename, "output");

            // Re-compress the verified, patched data - this becomes the final data written to destFile.
            patchedStream.Position = 0;
            var package = MEPackageHandler.OpenMEPackageFromStream(patchedStream);
            package.Save(destFile, compress: true);
        }

        /// <summary>
        /// Verifies actual data against an expected size and hash, checking size first (cheap) so a
        /// mismatch aborts immediately without paying for a hash computation.
        /// </summary>
        private static void VerifySizeAndHash(long actualSize, Func<string> computeHash, long expectedSize, string expectedHash, string fileName, string stage)
        {
            if (actualSize != expectedSize)
            {
                throw new MilanDeltaVerificationException(
                    MilanDeltaVerificationFailureKind.SizeMismatch,
                    $"Milan delta {stage} size mismatch for {fileName}: expected {expectedSize}, got {actualSize}.");
            }

            var actualHash = computeHash();
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new MilanDeltaVerificationException(
                    MilanDeltaVerificationFailureKind.HashMismatch,
                    $"Milan delta {stage} hash mismatch for {fileName}: expected {expectedHash}, got {actualHash}.");
            }
        }

        #endregion

        /// <summary>
        /// Defines a source-to-destination mapping manifest for a batch of files.
        /// </summary>
        public record CopyManifest(
            string SourceSubFolder,
            string DestSubFolder,
            IReadOnlyList<string> Files,
            bool UseRelativePathing = true,
            IReadOnlyList<string>? ExtraPatterns = null
        );

        private void CopySourceFiles(string sourceDirectory, string destGamePath, IEnumerable<CopyManifest> manifests)
        {
            // Pre-scan the entire source folder tree once into a case-insensitive lookup table by file name
            //var filesInSourceFolder = Directory.EnumerateFiles(sourceBasePath, "*.*", SearchOption.AllDirectories)
            //    .ToLookup(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

            // Pre-scan the entire source folder tree once into a case-insensitive lookup table by file name
            var filesInSourceFolder = MELoadedFiles.GetAllFilesLoadedInGame(MEGame.LE3, forceReload: true, includeModDLC: false, gameRootOverride: sourceDirectory);

            foreach (var manifest in manifests)
            {
                var sourceBasePath = Path.Combine(sourceDirectory, manifest.SourceSubFolder);
                var destFolder = Path.Combine(destGamePath, manifest.DestSubFolder);

                if (!Directory.Exists(sourceBasePath))
                {
                    M3Log.Warning($"Source directory does not exist: {sourceBasePath}");
                    continue;
                }

                Directory.CreateDirectory(destFolder);



                var filesToCopy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var baseFilesToProcessLocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Resolve explicitly requested files and localized variants
                foreach (var fileName in manifest.Files)
                {
                    // Exact match lookup (O(1))
                    if (filesInSourceFolder.TryGetValue(fileName, out var file))
                    {
                        filesToCopy.Add(file);
                        baseFilesToProcessLocs.Add(file);
                    }
                    else
                    {
                        // Fallback: look for direct localized matches (e.g. File_INT.pcc)
                        var baseName = Path.GetFileNameWithoutExtension(fileName);
                        var ext = Path.GetExtension(fileName);

                        var localizedMatches = filesInSourceFolder
                            .Values.Where(f =>
                            {
                                var fn = Path.GetFileName(f);
                                return Path.GetFileNameWithoutExtension(fn).StartsWith(baseName + "_", StringComparison.OrdinalIgnoreCase)
                                       && Path.GetExtension(fn).Equals(ext, StringComparison.OrdinalIgnoreCase);
                            });

                        foreach (var match in localizedMatches)
                        {
                            filesToCopy.Add(match);
                        }
                    }
                }

                // 2. Resolve additional wildcards (e.g., *.tfc)
                if (manifest.ExtraPatterns != null)
                {
                    foreach (var pattern in manifest.ExtraPatterns)
                    {
                        foreach (var file in Directory.EnumerateFiles(sourceBasePath, pattern, SearchOption.AllDirectories))
                        {
                            filesToCopy.Add(file);
                        }
                    }
                }

                // 3. Perform file copying and header updating
                foreach (var file in filesToCopy)
                {
                    var fileName = Path.GetFileName(file);
                    if (file.Contains("Indra"))
                    {
                        Debug.WriteLine("h");
                    }
                    interop.SetStatus?.Invoke(fileName);

                    // Maintain nested directory layout if the source file is inside subdirectories
                    var relativePath = manifest.UseRelativePathing ? Path.GetRelativePath(sourceBasePath, file) : fileName;
                    var destFile = Path.Combine(destFolder, relativePath);

                    var parentDir = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }



                    CopyFile(file, destFile);

                    // Copy localization packages if this file was explicitly requested as a base file
                    var extension = Path.GetExtension(file);
                    if ((extension.Equals(".pcc", StringComparison.OrdinalIgnoreCase) ||
                         extension.Equals(".afc", StringComparison.OrdinalIgnoreCase)) &&
                        baseFilesToProcessLocs.Contains(file))
                    {
                        CopyLocalizationsOfFile(file, destFolder);
                    }
                }
            }
        }

        private void CopyFile(string file, string destFile)
        {
            M3Log.Information($"Copying {file} to {destFile}");

            var extension = Path.GetExtension(file);
            var isPackage = extension.Equals(".pcc", StringComparison.OrdinalIgnoreCase);

            // If a MilanDelta patch targets this file, apply it (decompress -> patch -> verify -> recompress)
            // instead of a plain byte copy. This must happen before the licensee version is changed below,
            // since the delta was diffed against the original LE3 data.
            if (isPackage && milanDeltaLookup != null && milanDeltaLookup.TryGetValue(Path.GetFileName(destFile), out var deltaEntry))
            {
                try
                {
                    ApplyMilanDelta(file, destFile, deltaEntry);
                }
                catch (MilanDeltaVerificationException deltaEx)
                {
                    var backupFile = TryGetFallbackBackupFile(file);
                    if (backupFile == null)
                    {
                        throw;
                    }

                    M3Log.Warning($"Milan delta verification failed for {Path.GetFileName(destFile)} from source ({file}). Retrying with backup file ({backupFile}).");

                    try
                    {
                        ApplyMilanDelta(backupFile, destFile, deltaEntry);
                    }
                    catch (Exception backupEx)
                    {
                        throw new Exception($"Milan delta verification failed for {Path.GetFileName(destFile)} from both source and backup files. Source: {file}. Backup: {backupFile}.", new AggregateException(deltaEx, backupEx));
                    }
                }
            }
            else
            {
                using var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read);
                using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.ReadWrite);
                sourceStream.CopyTo(destStream);
            }

            // Update the licensee value for .pcc files
            if (isPackage)
            {
                using var destStream = new FileStream(destFile, FileMode.Open, FileAccess.ReadWrite);
                destStream.Seek(0x6, SeekOrigin.Begin);
                destStream.WriteByte((byte)MEPackage.LMPLicenseeVersion);
            }
        }

        private string TryGetFallbackBackupFile(string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(sourceBackupPath) || string.IsNullOrWhiteSpace(sourceRootPath))
            {
                return null;
            }

            var relativePath = Path.GetRelativePath(sourceRootPath, sourceFile);
            if (relativePath.StartsWith(".." + Path.DirectorySeparatorChar) ||
                relativePath.Equals("..", StringComparison.Ordinal) ||
                Path.IsPathRooted(relativePath))
            {
                return null;
            }

            var backupFile = Path.Combine(sourceBackupPath, relativePath);
            if (!File.Exists(backupFile))
            {
                return null;
            }

            return string.Equals(backupFile, sourceFile, StringComparison.OrdinalIgnoreCase)
                ? null
                : backupFile;
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
                var locName = Path.GetFileName(file.SetUnrealLocalization(MEGame.LMP, loc, true));
                var testPath = Path.Combine(dir, locName);
                if (File.Exists(testPath)) // Is there localized version in LE3?
                {
                    // Copy LOC version to Milan
                    var outPath = Path.Combine(destFolder, locName);
                    CopyFile(testPath, outPath);
                }

                // Straight loc
                locName = Path.GetFileName(file.SetUnrealLocalization(MEGame.LMP, loc, false));
                testPath = Path.Combine(dir, locName);
                if (File.Exists(testPath)) // Is there a unreal localized version in LE3?
                {
                    // Copy _LANG version to Milan
                    var outPath = Path.Combine(destFolder, locName);
                    CopyFile(testPath, outPath);
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
            sourceRootPath = sourceGameTarget.TargetPath;
            sourceBackupPath = BackupService.GetGameBackupPath(sourceGameTarget.Game);

            bool successful = true;
            string errorMessage = null;
            // Copy the source files over.
            try
            {
                // Validate the Project Milan mod and its MilanDelta manifest before touching the
                // destination directory - an invalid/missing/tampered manifest must reject the build.
                interop.SetStatus?.Invoke("Validating Project Milan build...");
                var milanmod = GetMilanMod();
                if (milanmod == null)
                {
                    throw new Exception("No Project Milan mod found in the mod library.");
                }
                LoadMilanDeltaManifest(milanmod);

                // Wipe the destination directory so we start from a clean slate.
                interop.SetStatus?.Invoke("Preparing installation directory...");
                if (Directory.Exists(destinationFolderPath))
                {
                    MUtilities.DeleteFilesAndFoldersRecursively(destinationFolderPath, throwOnFailed: true, deleteDirectoryItself: false);
                }
                MUtilities.CreateDirectoryWithWritePermission(destinationFolderPath);

                CopySourceFiles(sourceGameTarget.TargetPath, destinationFolderPath, installManifests);

                // Rename game executable. This is used a lot by tooling to identify the target game
                var existingExe = Path.Combine(destinationFolderPath, "Binaries", "Win64", "MassEffect3.exe");
                var newNameExe = Path.Combine(destinationFolderPath, "Binaries", "Win64", "MassEffectMultiplayer.exe");
                File.Move(existingExe, newNameExe);

                // Create stub directories, some of our tooling requires them to exist, even if they're empty.
                // Required by AutoTOC. I don't want to have to add a folder check to this, so we make it exist
                Directory.CreateDirectory(Path.Combine(destinationFolderPath, @"Engine", @"Shaders"));


                // Install Project Milan
                var lmpTarget = new GameTargetWPF(MEGame.LMP, destinationFolderPath, true);
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
                //lmpTarget.UninstallBinkBypass();

                // TOC game so game can find files.
                AutoTOC.RunTOCOnGameTarget(lmpTarget);

                lmpTarget.ValidateTarget(); // Triggers validation which unlocks UI options

                // Add target to Mod Manager
                interop?.AddTarget(lmpTarget);

                // This makes it easier to debug 
                lmpTarget.InstallBinkBypass(false);
            }
            catch (Exception e)
            {
                M3Log.Exception(e, $@"Error building base milan installation:");
                successful = false;
                errorMessage = e.Message;
#if DEBUG
                throw;
#endif
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
