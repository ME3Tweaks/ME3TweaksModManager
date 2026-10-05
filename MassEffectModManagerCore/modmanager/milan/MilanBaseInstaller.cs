using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.Services;
using ME3TweaksCore.Services.Backup;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.installer;
using ME3TweaksModManager.modmanager.milan.slash;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.installer;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
            "GuidCache.pcc", // Replaced with Milan's version by a MilanDelta patch

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
        /// Applies a MilanDelta JPatch delta to the package <paramref name="sourceFile"/> and returns the resulting package,
        /// recompressed. The delta is diffed against the decompressed package data, so the source is decompressed before
        /// patching and the result is recompressed afterwards; both the input and the patched output are verified against the
        /// manifest's recorded size and SHA-256 hash before being trusted.
        /// </summary>
        /// <param name="packagePath">Path the package is associated with while it is re-saved.</param>
        private byte[] ApplyMilanDelta(string sourceFile, string packagePath, MilanDeltaFileEntry entry)
        {
            using var sourceStream = new MemoryStream(File.ReadAllBytes(sourceFile));
            using var decompressedStream = MEPackage.GetDecompressedPackageStream(sourceStream);
            decompressedStream.Position = 0;

            VerifySizeAndHash(decompressedStream.Length, () => MUtilities.CalculateHash(decompressedStream, "sha256"),
                entry.SourceSize, entry.SourceHash, entry.Filename, "source");

            using var patchedStream = ApplyMilanDeltaPatch(decompressedStream, entry);

            // Re-compress the verified, patched data - this becomes the final data of the file.
            patchedStream.Position = 0;
            using var package = MEPackageHandler.OpenMEPackageFromStream(patchedStream, packagePath);
            using var saved = package.SaveToStream(compress: true);
            return saved.ToArray();
        }

        /// <summary>
        /// Applies a MilanDelta JPatch delta to a file that is not a package (e.g. Coalesced.bin), which has no
        /// compression to undo: the delta is applied to the file's bytes as they are, and the patched output is returned as is.
        /// Both the input and the output are verified against the manifest's recorded size and SHA-256 hash.
        /// </summary>
        private byte[] ApplyMilanRawDelta(string sourceFile, MilanDeltaFileEntry entry)
        {
            using var sourceStream = new MemoryStream(File.ReadAllBytes(sourceFile));

            VerifySizeAndHash(sourceStream.Length, () => MUtilities.CalculateHash(sourceStream, "sha256"),
                entry.SourceSize, entry.SourceHash, entry.Filename, "source");

            using var patchedStream = ApplyMilanDeltaPatch(sourceStream, entry);
            return patchedStream.ToArray();
        }

        /// <summary>
        /// Applies a MilanDelta patch to verified source data, and verifies the output.
        /// </summary>
        private MemoryStream ApplyMilanDeltaPatch(Stream source, MilanDeltaFileEntry entry)
        {
            var patchPath = Path.Combine(milanDeltaFolder, entry.Delta);
            if (!File.Exists(patchPath))
            {
                throw new Exception($"Milan delta patch file is missing: {patchPath}");
            }

            source.Position = 0;
            using var patchStream = File.OpenRead(patchPath);
            var patchedStream = new MemoryStream();
            JPatch.ApplyJPatch(source, patchStream, patchedStream);

            VerifySizeAndHash(patchedStream.Length, () => MUtilities.CalculateHash(patchedStream, "sha256"),
                entry.DestSize, entry.DestHash, entry.Filename, "output");
            return patchedStream;
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

        #region Slash

        /// <summary>
        /// The slash manifest that was applied to this installation, if any.
        /// </summary>
        private MilanSlashManifest slashManifest;

        /// <summary>
        /// The installation plan, worked out before anything is written.
        /// </summary>
        private MilanInstallPlan plan;

        /// <summary>
        /// Project Milan files whose slash patch has been applied as the mod was installed.
        /// </summary>
        private readonly HashSet<string> appliedModPatches = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The first failure slashing a Project Milan file as it was installed. ModInstaller reports failures in its own terms,
        /// so this is kept to report the real problem.
        /// </summary>
        private Exception modFileSlashFailure;

        /// <summary>
        /// Project Milan files larger than this are raw patched through a temporary file rather than in memory.
        /// </summary>
        private const long InMemoryModPatchLimit = 100 * 1024 * 1024;

        /// <summary>
        /// Assembles a slash build into the installation and verifies it against the manifest.
        /// </summary>
        /// <param name="resolveGameFile">Returns the path to read a game file source (relative to the installation root) from.</param>
        private void AssembleSlashBuild(MilanSlashBuildEntry build, string gamePath, Func<string, string> resolveGameFile)
        {
            interop.SetStatus?.Invoke($"Building {Path.GetFileName(build.Filename)}");
            var destFile = MilanSlashApplier.Resolve(gamePath, build.Filename);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile));

            // Built next to its final location, as the output can be one of its own sources
            var stagingPath = destFile + @".slashtmp";
            try
            {
                using (var output = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024))
                {
                    MilanSlashApplier.AssembleBuild(build, source => source.Payload ? MilanSlashApplier.Resolve(milanDeltaFolder, source.Filename) : resolveGameFile(source.Filename), output);
                    MilanSlashApplier.VerifySize(output.Length, build.DestSize, build.Filename, @"output");
                }
                MilanSlashApplier.VerifyHash(MUtilities.CalculateHash(stagingPath, @"sha256"), build.DestHash, build.Filename, @"output");
                File.Move(stagingPath, destFile, overwrite: true);
            }
            finally
            {
                if (File.Exists(stagingPath))
                    File.Delete(stagingPath);
            }

            M3Log.Information($@"Slash: built {build.Filename} ({FileSize.FormatSize(build.DestSize)})");
        }

        /// <summary>
        /// ModInstaller hook: applies the slash patch of a Project Milan file, if it has one, as soon as it has been installed.
        /// The file is patched in memory and written back once.
        /// </summary>
        private void SlashInstalledModFile(string gamePath, string installedFile)
        {
            if (plan?.Slash == null)
                return;

            var relativePath = Path.GetRelativePath(gamePath, installedFile);
            if (!plan.ModPatches.TryGetValue(relativePath, out var patch))
                return;

            try
            {
                var deltaPath = MilanSlashApplier.Resolve(milanDeltaFolder, patch.Delta);
                var size = new FileInfo(installedFile).Length;
                if (size > InMemoryModPatchLimit)
                {
                    M3Log.Warning($@"Slash: {relativePath} is {FileSize.FormatSize(size)}; files this large are not expected to be patched while Project Milan is installed");
                }

                if (patch.Mode == MilanSlashPatchModes.Raw && size > InMemoryModPatchLimit)
                {
                    var stagingPath = installedFile + @".slashtmp";
                    try
                    {
                        using (var source = new FileStream(installedFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024))
                        using (var output = new FileStream(stagingPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1024 * 1024))
                        {
                            MilanSlashApplier.PatchRaw(patch, deltaPath, source, output);
                        }
                        File.Move(stagingPath, installedFile, overwrite: true);
                    }
                    finally
                    {
                        if (File.Exists(stagingPath))
                            File.Delete(stagingPath);
                    }
                }
                else
                {
                    var data = File.ReadAllBytes(installedFile);
                    File.WriteAllBytes(installedFile, ApplySlashPatchInMemory(patch, deltaPath, data, installedFile));
                }

                appliedModPatches.Add(relativePath);
            }
            catch (Exception e)
            {
                modFileSlashFailure ??= e;
                throw;
            }
        }

        /// <summary>
        /// Applies a slash patch to a file held in memory, returning the patched file.
        /// </summary>
        /// <param name="filePath">Where the file is installed.</param>
        private static byte[] ApplySlashPatchInMemory(MilanSlashPatchEntry patch, string deltaPath, byte[] data, string filePath)
        {
            if (patch.Mode != MilanSlashPatchModes.Raw)
                return MilanSlashApplier.PatchPackage(patch, deltaPath, data, filePath);

            using var source = new MemoryStream(data);
            using var output = new MemoryStream();
            MilanSlashApplier.PatchRaw(patch, deltaPath, source, output);
            return output.ToArray();
        }

        /// <summary>
        /// Finishes slashing once Project Milan has been installed: checks every Project Milan file that should have been
        /// patched was, then runs the slash operations that need its files.
        /// </summary>
        private void FinishModSlash(string gamePath)
        {
            if (plan.Slash == null)
                return;

            var missed = plan.ModPatches.Keys.Where(x => !appliedModPatches.Contains(x)).ToList();
            if (missed.Count > 0)
            {
                throw new Exception($"{missed.Count} Project Milan file(s) were not optimized as they were installed. First file: {missed[0]}");
            }

            foreach (var build in plan.PostInstallBuilds)
            {
                AssembleSlashBuild(build, gamePath, relativePath => plan.ModOutputs.Contains(relativePath)
                    ? MilanSlashApplier.Resolve(gamePath, relativePath)
                    : le3Sources.GetVanillaSourceFile(plan.LE3Files[relativePath].SourcePath));
            }

            foreach (var deletion in plan.PostInstallDeletions)
            {
                var path = MilanSlashApplier.Resolve(gamePath, deletion.Filename);
                if (File.Exists(path))
                {
                    M3Log.Information($@"Slash: deleting {deletion.Filename} ({deletion.Comment})");
                    File.Delete(path);
                }
            }

            M3Log.Information($@"Slash: {appliedModPatches.Count} Project Milan file(s) patched as installed, {plan.PostInstallBuilds.Count} post-install build(s), {plan.PostInstallDeletions.Count} post-install deletion(s)");
        }

        /// <summary>
        /// Verifies every file in the finished installation against the final state recorded in the slash manifest when it
        /// was generated, so we know the user's game is exactly the one we built and tested.
        /// </summary>
        private void VerifyFinalState(string gamePath)
        {
            if (slashManifest == null)
                return; // Nothing was slashed, so there is no recorded state this installation should match

            if (slashManifest.FinalFiles.Count == 0)
            {
                M3Log.Warning(@"The slash manifest has no recorded final file state; the installation cannot be verified");
                return;
            }

            interop.SetStatus?.Invoke(@"Verifying installation...");
            var problems = MilanFinalState.Verify(slashManifest.FinalFiles, gamePath, s => interop.SetStatus?.Invoke(s));
            if (problems.Count == 0)
            {
                M3Log.Information($@"Installation verified: all {slashManifest.FinalFiles.Count} files match the expected final state");
                return;
            }

            foreach (var problem in problems)
            {
                M3Log.Error($@"Installation verification: {problem}");
            }

#if DEBUG
            M3Log.Warning($@"The installation does not match the final state recorded in the slash manifest ({problems.Count} difference(s)). Continuing as this is a development build.");
#else
            throw new Exception($"The installation does not match the expected result ({problems.Count} difference(s); see the log for all of them). First difference: {problems[0]}");
#endif
        }

#if DEBUG
        /// <summary>
        /// Generates a new slash manifest for this installation and writes it into the Project Milan mod's MilanDelta folder,
        /// replacing any existing one. Generation slashes the game (TFC compaction, RefShaderCache reduction), records the
        /// differences, and restores the game; the manifest is then applied here the same way it is on users' machines, which
        /// both proves it works and leaves this installation identical to what users will get.
        /// </summary>
        private void GenerateAndApplyMilanSlash(string gamePath)
        {
            interop.SetStatus?.Invoke(@"Generating slash manifest...");
            var manifest = MilanSlashGenerator.Generate(gamePath, milanDeltaFolder, s => interop.SetStatus?.Invoke(s));
            GC.Collect(); // Generation can hold several GB (the RefShaderCache, package diffs); release it before applying

            interop.SetStatus?.Invoke(@"Applying generated slash manifest...");
            MilanSlashApplier.Apply(manifest, milanDeltaFolder, gamePath, s => interop.SetStatus?.Invoke(s));
            slashManifest = manifest;
        }
#endif

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

        /// <summary>
        /// The LE3 installation files are copied from.
        /// </summary>
        private LE3SourceResolver le3Sources;

        /// <summary>
        /// Copies every file in the plan into the installation, then assembles the slash builds that come from LE3's files.
        /// </summary>
        private void ExecutePlan(string destGamePath)
        {
            int done = 0;
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 4) };
            try
            {
                Parallel.ForEach(plan.Copies, parallelOptions, copy =>
                {
                    CopyFile(copy, destGamePath);
                    var count = Interlocked.Increment(ref done);
                    interop.SetStatus?.Invoke($"Copying files ({count}/{plan.Copies.Count})");
                });
            }
            catch (AggregateException ae) when (ae.InnerExceptions.Count > 0)
            {
                // Surface the actual failure rather than Parallel's "One or more errors occurred"
                ExceptionDispatchInfo.Capture(ae.InnerExceptions[0]).Throw();
            }

            foreach (var build in plan.InlineBuilds)
            {
                AssembleSlashBuild(build, destGamePath, relativePath => le3Sources.GetVanillaSourceFile(plan.LE3Files[relativePath].SourcePath));
            }
        }

        /// <summary>
        /// Copies a file from LE3 into the installation, writing it once: it is MilanDelta patched (against the original LE3
        /// data), converted to LMP, then slash patched (against the file as it is in a finished installation).
        /// </summary>
        private void CopyFile(MilanInstallPlan.CopyEntry copy, string destGamePath)
        {
            var file = le3Sources.GetVanillaSourceFile(copy.SourcePath);
            var destFile = Path.Combine(destGamePath, copy.DestRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile));
            M3Log.Information($"Copying {file} to {destFile}");

            bool convertToLMP = LMPLicensee.AppliesTo(destFile);
            var slashPatch = copy.SlashPatch;
            var slashDeltaPath = slashPatch != null ? MilanSlashApplier.Resolve(milanDeltaFolder, slashPatch.Delta) : null;

            if (slashPatch?.Mode == MilanSlashPatchModes.Raw && copy.Delta == null)
            {
                // Large uncompressed files (the 1.3GB RefShaderCache) are patched straight from LE3 into the installation,
                // converted to LMP as they are read
                using var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
                using var input = convertToLMP ? LMPLicensee.WrapForReading(sourceStream) : sourceStream;
                using var output = new FileStream(destFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1024 * 1024);
                MilanSlashApplier.PatchRaw(slashPatch, slashDeltaPath, input, output);
                return;
            }

            if (copy.Delta == null && slashPatch == null)
            {
                using (var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                using (var destStream = new FileStream(destFile, FileMode.Create, FileAccess.ReadWrite))
                {
                    sourceStream.CopyTo(destStream);
                }

                if (convertToLMP)
                {
                    LMPLicensee.Set(destFile);
                }
                return;
            }

            // Everything else is built in memory and written once.
            byte[] data;
            if (copy.Delta != null)
            {
                // The delta was diffed against the original LE3 data, so it is applied before the file is converted to LMP.
                // The source file has already been resolved to a vanilla copy, so there is no retry on failure.
                data = Path.GetExtension(destFile).Equals(@".pcc", StringComparison.OrdinalIgnoreCase)
                    ? ApplyMilanDelta(file, destFile, copy.Delta)
                    : ApplyMilanRawDelta(file, copy.Delta);
            }
            else
            {
                data = File.ReadAllBytes(file);
            }

            if (convertToLMP)
            {
                LMPLicensee.Set(data);
            }

            if (slashPatch != null)
            {
                // Slash was generated against the finished installation, so it is applied once the file is converted to LMP
                data = ApplySlashPatchInMemory(slashPatch, slashDeltaPath, data, destFile);
            }

            File.WriteAllBytes(destFile, data);
        }

        public static readonly MELocalization[] SupportedVOLanguages = new[]
        {
            MELocalization.INT,
            MELocalization.FRA,
            MELocalization.DEU,
            MELocalization.ITA,
            // Any other languages that had VO?
        };

        // UI STRING UPDATES FOR INSTALLER ========



        // END UI STRING UPDATES FOR INSTALLER=====

        private static ModInstallOptionsPackage CreateMilanInstallOptions(Mod milanMod, GameTarget target)
        {
            var optionsPackage = new ModInstallOptionsPackage()
            {
                InstallTarget = target,
                ModBeingInstalled = milanMod,
                SkipPrerequesitesCheck = true
            };

            // We have no options, set it as having none chosen
            optionsPackage.SetNoOptions();
            return optionsPackage;
        }

        /// <summary>
        /// Paths, relative to the installation root, of every file the Project Milan mod installs.
        /// </summary>
        private static List<string> GetModOutputs(Mod milanMod, string destinationFolderPath)
        {
            // Stand-in for the installation, which doesn't exist yet; only its paths are used
            var target = new GameTargetWPF(MEGame.LMP, destinationFolderPath, false, isCustomOption: true, skipInit: true);
            var queues = milanMod.GetInstallationQueues(CreateMilanInstallOptions(milanMod, target));
            if (queues.SFARJobs.Count > 0)
            {
                throw new Exception("Project Milan cannot install files into SFARs.");
            }

            var dlcPath = MEDirectories.GetDLCPath(MEGame.LMP, target.TargetPath);
            var outputs = new List<string>();
            foreach (var (job, mapping) in queues.UnpackedJobMappings)
            {
                // Same destinations ModInstaller uses
                var root = job.Header == ModJob.JobHeader.CUSTOMDLC ? dlcPath : target.TargetPath;
                outputs.AddRange(mapping.FileMapping.Keys.Select(x => Path.GetRelativePath(target.TargetPath, Path.Combine(root, x))));
            }
            return outputs;
        }

        /// <summary>
        /// Copies the require files from LE3 to the destination folder. The source folder should be a backup to help ensure mods are not installed, as base install should be "vanilla".
        /// </summary>
        /// <param name="le3SourcePath"></param>
        /// <param name="destinationFolderPath"></param>
        /// <param name="generateSlashManifest">Development builds only: instead of applying the Project Milan mod's slash manifest, generate a new one against this installation (see <see cref="MilanSlashGenerator"/>), then apply it.</param>
        public void CreateInstallation(GameTarget sourceGameTarget, string destinationFolderPath, MBIInterop interop, bool generateSlashManifest = false)
        {
            this.interop = interop;
            destinationFolderPath = Path.GetFullPath(destinationFolderPath);

            bool successful = true;
            string errorMessage = null;
            bool destinationPrepared = false;
            bool destinationExisted = Directory.Exists(destinationFolderPath);
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

                // Slash is applied as the installation is built. Generating a new slash manifest needs an unslashed installation.
                MilanSlashManifest slash = null;
#if DEBUG
                if (!generateSlashManifest)
#endif
                {
                    slash = MilanSlashApplier.LoadManifest(milanDeltaFolder);
                    if (slash == null)
                    {
                        M3Log.Warning(@"Project Milan mod has no slash manifest; the installation will not be optimized");
                    }
                }

                // Work out and check everything that can be checked up front, so an installation that can't succeed fails
                // before anything is written.
                interop.SetStatus?.Invoke("Checking LE3 installation...");
                le3Sources = new LE3SourceResolver(sourceGameTarget.TargetPath, BackupService.GetGameBackupPath(MEGame.LE3));
                M3Log.Information($"Building Project Milan from {le3Sources.SourceRoot}" + (le3Sources.BackupRoot != null ? $"; modified files will be taken from the LE3 backup at {le3Sources.BackupRoot}" : "; there is no LE3 backup to take modified files from"));
                plan = MilanInstallPlan.Build(le3Sources, installManifests, SupportedVOLanguages, milanDeltaLookup, milanDeltaFolder, slash, GetModOutputs(milanmod, destinationFolderPath));
                slashManifest = plan.Slash;

                // Wipe the destination directory so we start from a clean slate.
                interop.SetStatus?.Invoke("Preparing installation directory...");
                if (Directory.Exists(destinationFolderPath))
                {
                    MUtilities.DeleteFilesAndFoldersRecursively(destinationFolderPath, throwOnFailed: true, deleteDirectoryItself: false);
                }
                MUtilities.CreateDirectoryWithWritePermission(destinationFolderPath);
                destinationPrepared = true;

                ExecutePlan(destinationFolderPath);

                // Rename game executable. This is used a lot by tooling to identify the target game
                var existingExe = Path.Combine(destinationFolderPath, "Binaries", "Win64", "MassEffect3.exe");
                var newNameExe = Path.Combine(destinationFolderPath, "Binaries", "Win64", "MassEffectMultiplayer.exe");
                File.Move(existingExe, newNameExe);

                // Create stub directories, some of our tooling requires them to exist, even if they're empty.
                // Required by AutoTOC. I don't want to have to add a folder check to this, so we make it exist
                Directory.CreateDirectory(Path.Combine(destinationFolderPath, @"Engine", @"Shaders"));


                // Install Project Milan
                var lmpTarget = new GameTargetWPF(MEGame.LMP, destinationFolderPath, true);
                var optionsPackage = CreateMilanInstallOptions(milanmod, lmpTarget);

                interop.SetStatus?.Invoke("Installing Project Milan...");
                ModInstaller mi = new ModInstaller(optionsPackage);
                mi.SetPercent = setPercent;
                mi.SetAction = setAction;
                mi.SetPercentVisibility = setPercentVisibility;
                mi.PostFileInstalled = installedFile => SlashInstalledModFile(destinationFolderPath, installedFile);
                // InstallMod is async (it awaits ASI installs), so it must complete before the installation is TOC'd.
                // We are on a background thread with no synchronization context, so blocking here is safe.
                mi.InstallMod().GetAwaiter().GetResult();
                if (modFileSlashFailure != null)
                {
                    ExceptionDispatchInfo.Capture(modFileSlashFailure).Throw();
                }
                if (mi.InstallationResult.Result != EModInstallerResult.INSTALL_SUCCESSFUL)
                {
                    throw new Exception($"Installing Project Milan failed: {mi.InstallationResult.ErrorMessage ?? mi.InstallationResult.Result.ToString()}");
                }

                FinishModSlash(destinationFolderPath);

#if DEBUG
                // Shrink the installation down to the data LMP actually uses
                if (generateSlashManifest)
                {
                    GenerateAndApplyMilanSlash(destinationFolderPath);
                }
#endif

                // TODO: Build the LMP vanilla object database against the (slashed) installation here

                // Unapply bink. This will be a common problem, so we can just handle it here.
                //lmpTarget.UninstallBinkBypass();

                // TOC game so game can find files.
                AutoTOC.RunTOCOnGameTarget(lmpTarget);

                lmpTarget.ValidateTarget(); // Triggers validation which unlocks UI options

                // This makes it easier to debug
                lmpTarget.InstallBinkBypass(false);

                // Must be the last thing that writes to the game
#if DEBUG
                if (generateSlashManifest)
                {
                    MilanSlashGenerator.RecordFinalState(slashManifest, milanDeltaFolder, destinationFolderPath, s => interop.SetStatus?.Invoke(s));
                }
                else
#endif
                {
                    VerifyFinalState(destinationFolderPath);
                }

                // Add target to Mod Manager
                interop?.AddTarget(lmpTarget);
            }
            catch (Exception e)
            {
                M3Log.Exception(e, $@"Error building base milan installation:");
                successful = false;
                errorMessage = e.Message;

                // Don't leave a half built installation behind
                if (destinationPrepared)
                {
                    DeleteFailedInstallation(destinationFolderPath, deleteDirectoryItself: !destinationExisted);
                }
#if DEBUG
                throw;
#endif
            }
            finally
            {
                interop?.InstallationFinished(successful, errorMessage);
            }
        }

        private void DeleteFailedInstallation(string destinationFolderPath, bool deleteDirectoryItself)
        {
            interop?.SetStatus?.Invoke("Removing incomplete installation...");
            try
            {
                M3Log.Information($"Deleting incomplete installation at {destinationFolderPath}");
                if (!MUtilities.DeleteFilesAndFoldersRecursively(destinationFolderPath, throwOnFailed: false, deleteDirectoryItself: deleteDirectoryItself))
                {
                    M3Log.Error($"Could not fully delete the incomplete installation at {destinationFolderPath}");
                }
            }
            catch (Exception e)
            {
                // Never hide the original failure
                M3Log.Exception(e, $"Could not delete the incomplete installation at {destinationFolderPath}:");
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
        #endregion
    }
}
