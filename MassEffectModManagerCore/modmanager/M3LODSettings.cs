using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager
{
    [Localizable(false)]
    public static class M3LODSettings
    {
        public static bool SetLODs(GameTargetWPF target, bool highres, bool limit2k, bool softshadows)
        {
            var game = target.Game;
            if (game != MEGame.ME1 && softshadows)
            {
                throw new Exception("Cannot use softshadows parameter of SetLODs() with a game that is not ME1");
            }

            if (target.Game.IsLEGame())
            {
                M3Log.Information(@"Settings LODs for Legendary Edition is not currently supported");
                return true; // fake saying we did it
            }

            M3Log.Information($@"Settings LODS for {target.Game}, highres: {highres}, 2K: {limit2k}, SS: {softshadows}");

            try
            {
                string settingspath = MEDirectories.GetLODConfigFile(game);

                if (!File.Exists(settingspath) && game == MEGame.ME1)
                {
                    M3Log.Error("Cannot raise/lower LODs on file that doesn't exist (ME1 bioengine file must already exist or exe will overwrite it)");
                    return false;
                }
                else if (!File.Exists(settingspath))
                {
                    Directory.CreateDirectory(Directory.GetParent(settingspath).FullName); //ensure directory exists.
                    File.Create(settingspath).Close();
                }

                bool configFileReadOnly = false;
                if (game == MEGame.ME1)
                {
                    try
                    {
                        // Get read only state for config file. It seems sometimes they get set read only.
                        FileInfo fi = new FileInfo(settingspath);
                        configFileReadOnly = fi.IsReadOnly;
                        if (configFileReadOnly)
                        {
                            M3Log.Information(@"Removing read only flag from ME1 bioengine.ini");
                            fi.IsReadOnly = false; //clear read only. might happen on some binkw32 in archives, maybe
                        }
                    }
                    catch (Exception e)
                    {
                        M3Log.Error($@"Error removing readonly flag from ME1 bioengine.ini: {e.Message}");
                    }
                }

                DuplicatingIni ini = DuplicatingIni.LoadIni(settingspath);
                if (game > MEGame.ME1 && game.IsOTGame())
                {
                    #region setting systemsetting for me2/3

                    string operation = null;
                    var iniList = game == MEGame.ME2 ? (limit2k ? ME2_2KLODs : ME2HighResLODs) : (limit2k ? ME3_2KLODs : ME3HighResLODs);
                    var section = ini.Sections.FirstOrDefault(x => x.Header == "SystemSettings");
                    if (section == null && highres)
                    {
                        //section missing, and we are setting high res
                        ini.Sections.Add(new DuplicatingIni.Section()
                        {
                            Entries = iniList,
                            Header = "SystemSettings"
                        });
                        operation = "Set high-res lod settings on blank gamersettings.ini";
                    }
                    else if (highres)
                    {
                        //section exists, upgrading still, overwrite keys
                        foreach (var newItem in iniList)
                        {
                            var matchingKey = section.Entries.FirstOrDefault(x => x.Key == newItem.Key);
                            if (matchingKey != null)
                            {
                                matchingKey.Value = newItem.Value; //overwrite value
                            }
                            else
                            {
                                section.Entries.Add(newItem); //doesn't exist, add new item.
                            }
                        }



                        operation = "Set high-res lod settings in gamersettings.ini";

                    }
                    else if (section != null)
                    {
                        //section exists, downgrading
                        section.Entries.RemoveAll(x => iniList.Any(i => i.Key == x.Key));
                        operation = "Removed high-res lod settings from gamersettings.ini";
                    }

                    #endregion

                    //Update GFx (non LOD) settings
                    if (highres)
                    {
                        var hqKeys = target.Game == MEGame.ME2 ? ME2HQGraphicsSettings : ME3HQGraphicsSettings;
                        var hqSection = ini.GetSection(@"SystemSettings");
                        foreach (var entry in hqKeys)
                        {
                            var matchingKey = hqSection.Entries.FirstOrDefault(x => x.Key == entry.Key);
                            if (matchingKey != null)
                            {
                                matchingKey.Value = entry.Value; //overwrite value
                            }
                            else
                            {
                                hqSection.Entries.Add(entry); //doesn't exist, add new item.
                            }
                        }
                    }

                    File.WriteAllText(settingspath, ini.ToString());
                    M3Log.Information(operation);
                }
                else if (game == MEGame.ME1)
                {
                    var section = ini.Sections.FirstOrDefault(x => x.Header == "TextureLODSettings");
                    if (section == null && highres)
                    {
                        M3Log.Error("TextureLODSettings section cannot be null in ME1. Run the game to regenerate the bioengine file.");
                        return false; //This section cannot be null
                    }

                    var iniList = highres ? limit2k ? ME1_2KLODs : ME1HighResLODs : ME1_DefaultLODs;

                    //section exists, upgrading still, overwrite keys
                    foreach (var newItem in iniList)
                    {
                        var matchingKey = section.Entries.FirstOrDefault(x => x.Key == newItem.Key);
                        if (matchingKey != null)
                        {
                            matchingKey.Value = newItem.Value; //overwrite value
                        }
                        else
                        {
                            section.Entries.Add(newItem); //doesn't exist, add new item.
                        }
                    }

                    //Update GFx (non LOD) settings
                    if (highres)
                    {
                        var me1hq = GetME1HQSettings(target.MEUITMInstalled, softshadows);
                        foreach (var hqSection in me1hq)
                        {
                            var existingSect = ini.GetSection(hqSection);
                            if (existingSect != null)
                            {
                                foreach (var item in hqSection.Entries)
                                {
                                    var matchingKey = existingSect.Entries.FirstOrDefault(x => x.Key == item.Key);
                                    if (matchingKey != null)
                                    {
                                        matchingKey.Value = item.Value; //overwrite value
                                    }
                                    else
                                    {
                                        section.Entries.Add(item); //doesn't exist, add new item.
                                    }
                                }
                            }
                            else
                            {
                                //!!! Error
                                M3Log.Error(@"Error: Could not find ME1 high quality settings key in bioengine.ini: " + hqSection.Header);
                            }
                        }
                    }

                    File.WriteAllText(settingspath, ini.ToString());
                    M3Log.Information("Set " + (highres ? limit2k ? "2K lods" : "4K lods" : "default LODs") + " in BioEngine.ini file for ME1");
                }

                if (configFileReadOnly)
                {
                    try
                    {
                        M3Log.Information(@"Re-setting the read only flag on ME1 bioengine.ini");
                        FileInfo fi = new FileInfo(settingspath);
                        fi.IsReadOnly = true;
                    }
                    catch (Exception e)
                    {
                        M3Log.Error($@"Error re-setting readonly flag from ME1 bioengine.ini: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                M3Log.Error(@"Error setting LODs: " + e.Message);
                return false;
            }

            return true;
        }

        #region LOD Data

        private static List<DuplicatingIni.Section> GetME1HQSettings(bool meuitmMode, bool softShadowsME1)
        {
            //Engine.Engine
            var engineEngine = new DuplicatingIni.Section()
            {
                Header = "Engine.Engine",
                Entries = new List<DuplicatingIni.IniEntry>()
                {
                    new DuplicatingIni.IniEntry("MaxShadowResolution=2048"),
                    new DuplicatingIni.IniEntry("bEnableBranchingPCFShadows=True")
                }
            };


            var engineGameEngine = new DuplicatingIni.Section()
            {
                Header = "Engine.GameEngine",
                Entries = new List<DuplicatingIni.IniEntry>()
                {

                    new DuplicatingIni.IniEntry("MaxShadowResolution=2048"),
                    new DuplicatingIni.IniEntry("bEnableBranchingPCFShadows=True")
                }
            };

            var systemSettings = new DuplicatingIni.Section
            {
                Header = "SystemSettings",
                Entries = new List<DuplicatingIni.IniEntry>()
                {
                    new DuplicatingIni.IniEntry("ShadowFilterQualityBias=2"),
                    new DuplicatingIni.IniEntry("MaxAnisotropy=16"),
                    new DuplicatingIni.IniEntry("DynamicShadows=True"),
                    new DuplicatingIni.IniEntry("Trilinear=True"),
                    new DuplicatingIni.IniEntry("MotionBlur=True"),
                    new DuplicatingIni.IniEntry("DepthOfField=True"),
                    new DuplicatingIni.IniEntry("Bloom=True"),
                    new DuplicatingIni.IniEntry("QualityBloom=True"),
                    new DuplicatingIni.IniEntry("ParticleLODBias=-1"),
                    new DuplicatingIni.IniEntry("SkeletalMeshLODBias=-1"),
                    new DuplicatingIni.IniEntry("DetailMode=2")
                }
            };

            var textureStreaming = new DuplicatingIni.Section()
            {
                Header = "TextureStreaming",
                Entries = new List<DuplicatingIni.IniEntry>()
                {
                    new DuplicatingIni.IniEntry("PoolSize=1536"),
                    new DuplicatingIni.IniEntry("MinTimeToGuaranteeMinMipCount=0"),
                    new DuplicatingIni.IniEntry("MaxTimeToGuaranteeMinMipCount=0")
                }
            };

            var windrvWindowsclient = new DuplicatingIni.Section()
            {
                Header = "WinDrv.WindowsClient",
                Entries = new List<DuplicatingIni.IniEntry>()
                {
                    new DuplicatingIni.IniEntry("EnableDynamicShadows=True"),
                    new DuplicatingIni.IniEntry("TextureLODLevel=3"),
                    new DuplicatingIni.IniEntry("FilterLevel=2")
                }
            };



            //if soft shadows and MEUITM
            if (softShadowsME1 && meuitmMode)
            {
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("DepthBias=0.006000"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("DepthBias=0.006000e"));
            }
            else
            {
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("DepthBias=0.030000"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("DepthBias=0.030000"));
            }

            //if soft shadows
            if (softShadowsME1)
            {
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("MinShadowResolution=16"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("MinShadowResolution=16"));
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("ShadowFilterRadius=2"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("ShadowFilterRadius=2"));
            }
            else
            {
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("ShadowFilterRadius=4"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("ShadowFilterRadius=4"));
                engineEngine.Entries.Add(new DuplicatingIni.IniEntry("MinShadowResolution=64"));
                engineGameEngine.Entries.Add(new DuplicatingIni.IniEntry("MinShadowResolution=64"));
            }

            return new List<DuplicatingIni.Section>()
            {
                engineEngine,
                engineGameEngine,
                systemSettings,
                windrvWindowsclient,
                textureStreaming
            };
        }

        private static List<DuplicatingIni.IniEntry> ME1_DefaultLODs = new List<DuplicatingIni.IniEntry>()
        {
            //ME1 requires default lods to be restored or it'll just overwrite entire file
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=16,MaxLODSize=4096,LODBias=2)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=16,MaxLODSize=4096,LODBias=2)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=32,MaxLODSize=64,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=32,MaxLODSize=128,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=32,MaxLODSize=256,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=32,MaxLODSize=1024,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=8,MaxLODSize=64,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=8,MaxLODSize=128,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=8,MaxLODSize=256,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=8,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=8,MaxLODSize=1024,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=32,MaxLODSize=128,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=32,MaxLODSize=256,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=32,MaxLODSize=1024,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_GUI=(MinLODSize=8,MaxLODSize=1024,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=32,MaxLODSize=1024,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=32,MaxLODSize=512,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=32,MaxLODSize=256,LODBias=0)")
        };

        private static List<DuplicatingIni.IniEntry> ME1_2KLODs = new List<DuplicatingIni.IniEntry>()
        {
            //ME1 lods have bug where they use MinLodSize
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_GUI=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)")
        };

        private static List<DuplicatingIni.IniEntry> ME1HighResLODs = new List<DuplicatingIni.IniEntry>()
        {
            //ME1 lods have bug where they use MinLodSize
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_GUI=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=4096,MaxLODSize=4096,LODBias=0)")
        };

        private static List<DuplicatingIni.IniEntry> ME2HQGraphicsSettings = new List<DuplicatingIni.IniEntry>()
        {
            new DuplicatingIni.IniEntry("MaxShadowResolution=2048"),
            new DuplicatingIni.IniEntry("MinShadowResolution=64"),
            new DuplicatingIni.IniEntry("ShadowFilterQualityBias=2"),
            new DuplicatingIni.IniEntry("ShadowFilterRadius=4"),
            new DuplicatingIni.IniEntry("bEnableBranchingPCFShadows=True"),
            new DuplicatingIni.IniEntry("MaxAnisotropy=16"),
            new DuplicatingIni.IniEntry("Trilinear=True"),
            new DuplicatingIni.IniEntry("MotionBlur=True"),
            new DuplicatingIni.IniEntry("DepthOfField=True"),
            new DuplicatingIni.IniEntry("Bloom=True"),
            new DuplicatingIni.IniEntry("QualityBloom=True"),
            new DuplicatingIni.IniEntry("ParticleLODBias=-1"),
            new DuplicatingIni.IniEntry("SkeletalMeshLODBias=-1"),
            new DuplicatingIni.IniEntry("DetailMode=2")
        };


        private static List<DuplicatingIni.IniEntry> ME2_2KLODs = new List<DuplicatingIni.IniEntry>()
        {
            //under GamerSettings.ini [SystemSettings]
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_RenderTarget=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=128,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_UI=(MinLODSize=64,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=512,MaxLODSize=2048,LODBias=0)")
        };


        private static List<DuplicatingIni.IniEntry> ME2HighResLODs = new List<DuplicatingIni.IniEntry>()
        {
            //under GamerSettings.ini [SystemSettings]
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_LightAndShadowMap=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_RenderTarget=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=128,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_UI=(MinLODSize=64,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=512,MaxLODSize=2048,LODBias=0)")
        };

        private static List<DuplicatingIni.IniEntry> ME3HQGraphicsSettings = new List<DuplicatingIni.IniEntry>()
        {
            //Apply only. Do not unapply
            new DuplicatingIni.IniEntry("MaxShadowResolution=2048"),
            new DuplicatingIni.IniEntry("MinShadowResolution=64"),
            new DuplicatingIni.IniEntry("ShadowFilterQualityBias=2"),
            new DuplicatingIni.IniEntry("ShadowFilterRadius=4"),
            new DuplicatingIni.IniEntry("bEnableBranchingPCFShadows=True"),
            new DuplicatingIni.IniEntry("MaxAnisotropy=16"),
            new DuplicatingIni.IniEntry("MotionBlur=True"),
            new DuplicatingIni.IniEntry("DepthOfField=True"),
            new DuplicatingIni.IniEntry("Bloom=True"),
            new DuplicatingIni.IniEntry("QualityBloom=True"),
            new DuplicatingIni.IniEntry("ParticleLODBias=-1"),
            new DuplicatingIni.IniEntry("SkeletalMeshLODBias=-1"),
            new DuplicatingIni.IniEntry("DetailMode=2")
        };

        private static List<DuplicatingIni.IniEntry> ME3_2KLODs = new List<DuplicatingIni.IniEntry>()
        {
            //under GamerSettings.ini [SystemSettings]
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldSpecular=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_ShadowMap=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_RenderTarget=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=128,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=32,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=1024,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_UI=(MinLODSize=64,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=512,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=512,MaxLODSize=2048,LODBias=0)")
        };

        private static List<DuplicatingIni.IniEntry> ME3HighResLODs = new List<DuplicatingIni.IniEntry>()
        {
            //under GamerSettings.ini [SystemSettings]
            new DuplicatingIni.IniEntry("TEXTUREGROUP_World=(MinLODSize=256,MaxLODSize=2048,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldSpecular=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_WorldNormalMap=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_AmbientLightMap=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_ShadowMap=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_RenderTarget=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_64=(MinLODSize=128,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_128=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_256=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_512=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Environment_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_64=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_128=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_256=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_512=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_VFX_1024=(MinLODSize=32,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_128=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_256=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_512=(MinLODSize=1024,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_APL_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_UI=(MinLODSize=64,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Promotional=(MinLODSize=256,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_1024=(MinLODSize=2048,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Diff=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Norm=(MinLODSize=512,MaxLODSize=4096,LODBias=0)"),
            new DuplicatingIni.IniEntry("TEXTUREGROUP_Character_Spec=(MinLODSize=512,MaxLODSize=4096,LODBias=0)")
        };

        #endregion
    }
}
