using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.save.game2.FileFormats;
using ME3TweaksModManager.modmanager.save.game3;
using ME3TweaksModManager.ui;
using Microsoft.Win32;
using PropertyChanged;
using Path = System.IO.Path;

namespace ME3TweaksModManager.modmanager.save.game2.UI
{
    /// <summary>
    /// Interaction logic for SaveSelectorUI.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class SaveSelectorUI : Window
    {
        #region Career

        /// <summary>
        /// Save career as defined by name
        /// </summary>
        [AddINotifyPropertyChangedInterface]
        public class Career
        {
            public Career(List<ISaveFile> saves, string charName)
            {
                CharacterName = charName;
                SaveFiles.ReplaceAll(saves);
            }

            public string CharacterName { get; init; }
            public ObservableCollectionExtended<ISaveFile> SaveFiles { get; } = new();

        }

        #endregion

        /// <summary>
        /// Are saves being read?
        /// </summary>
        public bool LoadingSaves { get; set; }


        public ObservableCollectionExtended<Career> SaveCareers { get; } = new();

        /// <summary>
        /// The currently selected Career
        /// </summary>
        public Career SelectedCareer { get; set; }

        public void OnSelectedCareerChanged()
        {
            SelectedSaveFile = SelectedCareer?.SaveFiles.FirstOrDefault();
        }

        public ISaveFile SelectedSaveFile { get; set; }

        public MEGame CurrentGame { get; set; } = MEGame.LE3; // debug

        public Dictionary<MEGame, CaseInsensitiveDictionary<BitmapSource>> LevelImageCache { get; } = new();

        public void OnSelectedSaveFileChanged()
        {
            var csi = "";
            if (SelectedSaveFile == null)
            {
                SelectedLevelText = "Select a save file";
                // csi = MERUtilities.ListStaticAssets("saveimages", includemerPrefix: true).FirstOrDefault(x => x.EndsWith("nosave.png"));
            }
            else
            {
                SelectedLevelText = LevelNameStringConverter.StaticConvert(SelectedSaveFile.Game, SelectedSaveFile.Proxy_BaseLevelName);

                var sfname = SelectedSaveFile.Proxy_BaseLevelName.ToLower();
                if (LevelImageCache.TryGetValue(CurrentGame, out var cache) && cache.TryGetValue(sfname, out var cachedImage))
                {
                    CurrentSaveImage = cachedImage;
                }
                else
                {
                    // Load into cache
                    LoadSaveImageCache(sfname);
                    if (LevelImageCache.TryGetValue(CurrentGame, out var cache2) && cache2.TryGetValue(sfname, out var cachedImage2))
                    {
                        CurrentSaveImage = cachedImage2;
                    }

                }

            }
        }

        private static CaseInsensitiveDictionary<string> ME3MapToTextureNames = new()
        {

            { @"Biop_Nor", @"GUI_SF_SaveLoad_Images.Elevators.LVL_NorCIC_512x256" },
            { @"Biop_ProEar", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Earth_512x256" },
            { @"Biop_ProMar", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Mars_512x256" },
            { @"Biop_ProCit", @"GUI_SF_SaveLoad_Images.Elevators.LVL_CitHosp_512x256" },
            { @"Biop_Gth001", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Geth01_512x256" },
            { @"Biop_Gth002", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Geth02_512x256" },
            { @"Biop_GthN7a", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_GethAdmiral_512x256" },
            { @"Biop_GthLeg", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_GethLegion_512x256" },
            { @"Biop_KroGar", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Garrus_512x256" },
            { @"Biop_Kro001", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Geno01_512x256" },
            { @"Biop_Kro002", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Geno02_512x256" },
            { @"Biop_KroN7a", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_GenoRescue_512x256" },
            { @"Biop_KroN7b", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_GenoBomb_512x256" },
            { @"Biop_KroGru", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_GenoGrunt_512x256" },
            { @"Biop_Cat002", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Cat2Thessia_512x256" },
            { @"Biop_Cat003", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Cat3Coup_512x256" },
            { @"Biop_CerMir", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Miranda_512x256" },
            { @"Biop_CerJcb", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Jacob_512x256" },
            { @"Biop_CitHub", @"GUI_SF_SaveLoad_Images.Elevators.LVL_CitCommon_512x256" },
            { @"Biop_CitSam", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Samara_512x256" },
            { @"Biop_OmgJck", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Grissom_512x256" },
            { @"Biop_SPDish", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_FBDagger_512x256" },
            { @"Biop_SPSlum", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_FBGhost_512x256" },
            { @"Biop_SPTowr", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_FBGiant_512x256" },
            { @"Biop_SPCer", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_FBGlacier_512x256" },
            { @"Biop_SPRctr", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_FBReactor_512x256" },
            { @"Biop_SPNov", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_FBWhite_512x256" },
            { @"Biop_Cat004", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_Cat4Illusive_512x256" },
            { @"Biop_End001", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_EndEarth_512x256" },
            { @"Biop_End002", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_EndCitadel_512x256" },
            { @"Biop_End003", @"GUI_SF_SaveLoad_Images.ME3_images.LVL_EndCitadel_512x256" },

            // From Ashes
            { @"Biop_Cat001", @"GUI_PR_Images.LVL_Prothean_512x256" },

            // Leviathan
            { @"Biop_Lev001", @"GUI_LevSaveLoad_Images.SaveLoad.LVL_Lev001" },
            { @"Biop_Lev002", @"GUI_LevSaveLoad_Images.SaveLoad.LVL_Lev002" },
            { @"Biop_Lev003", @"GUI_LevSaveLoad_Images.SaveLoad.LVL_Lev003" },
            { @"Biop_Lev004", @"GUI_LevSaveLoad_Images.SaveLoad.LVL_Lev004" },

            // Omega
            //{ @"Biop_Omg000", @"BIOG_GUI_SAVE_GAME_ICONS.Textures.OmgMission01" },
            { @"Biop_Omg001", @"BIOG_GUI_SAVE_GAME_ICONS.Textures.OmgMission01" },
            { @"Biop_Omg02a", @"BIOG_GUI_SAVE_GAME_ICONS.Textures.OmgMission02" },
            { @"Biop_Omg003", @"BIOG_GUI_SAVE_GAME_ICONS.Textures.OmgMission03" },
            { @"Biop_Omg004", @"BIOG_GUI_SAVE_GAME_ICONS.Textures.OmgMission04" },
            { @"Biop_OmgHub", @"BIOG_GUI_SAVE_GAME_ICONS.OmgMissionHub" },

            // Citadel
            { @"Biop_Cit001", @"DLC_MPImages.SaveLoad.LVL_Cit_Sushi_512x256" },
            { @"Biop_Cit002", @"DLC_MPImages.SaveLoad.LVL_Cit_Casino_512x256" },
            { @"Biop_Cit003", @"DLC_MPImages.SaveLoad.LVL_Cit_Archives_512x256" },
            { @"Biop_Cit004", @"DLC_MPImages.SaveLoad.LVL_Cit_Docks_512x256" },
            { @"Biop_CitHot", @"DLC_MPImages.SaveLoad.LVL_Cit_Street_512x256" },
            { @"Biop_CitApt", @"DLC_MPImages.SaveLoad.LVL_Cit_Apartmnt_512x256" },
            { @"Biop_CitStart", @"DLC_MPImages.SaveLoad.LVL_Cit_Apartmnt_512x256" },
        };

        private string ConvertLevelToImageName(MEGame game, string baselevelname)
        {
            if (game.IsGame2())
            {
                return baselevelname + "_IMG";
            }
            else if (game.IsGame3())
            {
                ME3MapToTextureNames.TryGetValue(baselevelname, out var result);
                if (result != null)
                    return result;
            }

            return "";
        }

        private void LoadSaveImageCache(string mapName)
        {
            var imagePackages = new List<string>();
            if (!LevelImageCache.TryGetValue(CurrentGame, out _))
            {
                // Initialize
                LevelImageCache[CurrentGame] = new CaseInsensitiveDictionary<BitmapSource>();
            }
            switch (CurrentGame)
            {
                case MEGame.LE2:
                case MEGame.ME2:
                    imagePackages.Add(@"GUI_SF_SaveLoad_Images.pcc");
                    break;
                case MEGame.ME3:
                    imagePackages.Add(@"SFXImages_SaveLoad_1.pcc");
                    imagePackages.Add(@"SFXImages_SaveLoad_2.pcc");
                    imagePackages.Add(@"SFXImages_SaveLoad_3.pcc");
                    imagePackages.Add(@"SFXImages_SaveLoad_4.pcc");
                    imagePackages.Add(@"SFXImages_SaveLoad_5.pcc");
                    imagePackages.Add(@"SFXImages_SaveLoad_6.pcc");
                    break;
                case MEGame.LE3:
                    imagePackages.Add(@"SFXGUIData_ElevatorImages.pcc"); // Normandy, Citadel

                    imagePackages.Add(@"SFXImages_SaveLoad_1.pcc");
                    imagePackages.Add(@"SFXImages_SaveLoad_2.pcc");
                    imagePackages.Add(@"SFXImages_SaveLoad_3.pcc");
                    imagePackages.Add(@"SFXImages_SaveLoad_4.pcc");
                    imagePackages.Add(@"SFXImages_SaveLoad_5.pcc");
                    imagePackages.Add(@"SFXImages_SaveLoad_6.pcc");

                    imagePackages.Add(@"SFX_GUI_PR_SaveLoadImages_1.pcc"); // Prothean
                    imagePackages.Add(@"SFXImages_SaveLoad_Lev.pcc"); // Leviathan
                    imagePackages.Add(@"SFXImages_OMG_SaveLoad.pcc"); // Omega
                    imagePackages.Add(@"SFXImages_DLCCit_SaveLoad.pcc"); // Citadel
                    break;
            }

            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(CurrentGame);
            foreach (var imagePackageName in imagePackages)
            {
                if (loadedFiles.TryGetValue(imagePackageName, out var imagePackagePath))
                {
                    var exportPath = ConvertLevelToImageName(CurrentGame, mapName);

                    using var package = MEPackageHandler.UnsafePartialLoad(imagePackagePath, x =>
                    {
                        if (x.InstancedFullPath.CaseInsensitiveEquals(exportPath))
                        {
  //                          Debug.WriteLine($"Loading {x.InstancedFullPath}");
                            return true;
                        }
//                        Debug.WriteLine($"Not loading {x.InstancedFullPath}, needs {exportPath}");
                        return false;
                    });
                    var tex = package.FindExport(exportPath);

                    if (tex != null)
                    {
                        if (!tex.IsDataLoaded())
                        {
                            Debugger.Break();
                        }
                        Texture2D t2d = new Texture2D(tex);
                        // NOTE: Set 'ClearAlpha' to false to make image support transparency!
                        var bitmap = Image.convertRawToBitmapARGB(
                            t2d.GetImageBytesForMip(t2d.GetTopMip(), t2d.Export.Game, true, out _),
                            t2d.GetTopMip().width, t2d.GetTopMip().height, Image.getPixelFormatType(t2d.TextureFormat),
                            true);
                        //var bitmap = DDSImage.ToBitmap(imagebytes, fmt, mipToLoad.width, mipToLoad.height, CurrentLoadedExport.FileRef.Platform.ToString());
                        var memory = new MemoryStream(bitmap.Height * bitmap.Width * 4 + 54);
                        bitmap.Save(memory, ImageFormat.Bmp);
                        memory.Position = 0;
                        LevelImageCache[CurrentGame][GetLevelNameFromTexturePath(tex.InstancedFullPath, CurrentGame)] =
                            (BitmapSource)new ImageSourceConverter().ConvertFrom(memory);
                        break;
                    }
                }
            }
        }

        private string GetLevelNameFromTexturePath(string texInstancedFullPath, MEGame currentGame)
        {
            if (currentGame.IsGame2())
            {
                //return ME2
                texInstancedFullPath = texInstancedFullPath.Split('.').Last();
                return texInstancedFullPath.Substring(0, texInstancedFullPath.IndexOf("_IMG"));
            }
            if (currentGame.IsGame3())
            {
                var kvp = ME3MapToTextureNames.FirstOrDefault(x => x.Value.Equals(texInstancedFullPath, StringComparison.InvariantCultureIgnoreCase));
                if (kvp.Key != null)
                {
                    // Workaround for duplicates
                    if (kvp.Key == @"biop_Omg000")
                        return @"biop_Omg001";
                    return kvp.Key;
                }
            }

            return "";
        }

        /// <summary>
        /// The text at the top of the save area
        /// </summary>
        public string SelectedLevelText { get; set; }



        public BitmapSource CurrentSaveImage { get; set; }

        public SaveSelectorUI()
        {
            LoadingSaves = true;
            LoadCommands();
            InitializeComponent();
            OnSelectedSaveFileChanged();
        }


        private void LoadCommands()
        {
            SelectSaveCommand = new GenericCommand(SelectSave, CanSelectSave);
            //RefundHenchTalentsCommand = new GenericCommand(RefundHenchTalents, SaveIsSelected);
            //RefundPlayerTalentsCommand = new GenericCommand(RefundPlayerHenchTalents, SaveIsSelected);
            //RefundHenchPlayerTalentsCommand = new GenericCommand(RefundHenchPlayerTalents, SaveIsSelected);
        }

        private void SelectSave()
        {
            SaveWasSelected = true;
            Close();
        }

        private bool CanSelectSave()
        {
            return SaveIsSelected() && (SelectedSaveFile.Game.IsGame2() || SelectedSaveFile.Game.IsGame3());
        }

        //        private void RefundPlayerHenchTalents()
        //        {
        //            InternalRefundPoints(true, false);
        //        }

        //        private async void InternalRefundPoints(bool refundPlayer, bool refundHench)
        //        {
        //            if (refundPlayer)
        //            {
        //                // CLEAR PLAYER POWERS
        //                for (int i = SelectedSaveFile.PlayerRecord.Powers.Count - 1; i > 0; i--)
        //                {
        //                    // Need to figure out a way to only remove powers and leave things like first aid.
        //                    var power = SelectedSaveFile.PlayerRecord.Powers[i].PowerClassName;
        //                    bool shouldRemove = true;
        //                    Debug.WriteLine(power);
        //                    switch (power)
        //                    {
        //                        case "SFXGameContent_Powers.SFXPower_FirstAid":
        //                        case "SFXGameContent_Powers.SFXPower_PlayerMelee":
        //                        case "SFXGameContent_Powers.SFXPower_PlayerMeleePistol":
        //                            shouldRemove = false;
        //                            break;
        //                    }

        //                    if (shouldRemove)
        //                    {
        //                        SelectedSaveFile.PlayerRecord.Powers.RemoveAt(i);
        //                    }
        //                }

        //                // Set the number of talent points
        //                SelectedSaveFile.PlayerRecord.TalentPoints = TalentReset.GetNumTalentPoints(SelectedSaveFile.PlayerRecord.Level, true, true);
        //            }

        //            if (refundHench)
        //            {

        //                // CLEAR SQUADMATE POWERS
        //                //foreach (var hm in SelectedSaveFile.HenchmanRecords)
        //                //{
        //                //    var numTalentPoints = TalentReset.GetNumTalentPoints(SelectedSaveFile.PlayerRecord.Level, false, true, hm.Tag == "hench_vixen" || hm.Tag == "hench_leading");
        //                //    hm.TalentPoints = numTalentPoints;
        //                //    hm.Powers.Clear(); // Wipe out the talents list so game has to rebuild it on load
        //                //}

        //                // Game breaking?
        //                SelectedSaveFile.HenchmanRecords.Clear(); // Clears the loadout which removes powers (and weapons)
        //            }

        //            // Commit the save
        //#if DEBUG
        //            using (var outS = File.Open(SelectedSaveFile.FileName, FileMode.Create, FileAccess.Write))
        //            {
        //                SelectedSaveFile.Save(outS);
        //            }
        //#endif

        //            // Test the save file
        //            using (var outS = File.OpenRead(SelectedSaveFile.FileName))
        //            {
        //                var testSave = SaveFile.Load(outS);
        //            }

        //            M3L.ShowDialog(this, "Your save file has been updated.", "Save updated");
        //            Debug.WriteLine("Done!");
        //        }

        //        private void RefundHenchTalents()
        //        {
        //            InternalRefundPoints(false, true);
        //        }

        //        private void RefundHenchPlayerTalents()
        //        {
        //            InternalRefundPoints(true, true);
        //        }

        public GenericCommand SelectSaveCommand { get; set; }
        public bool SaveWasSelected { get; set; }

        private bool SaveIsSelected() => SelectedSaveFile != null;

        private void SSContent_Rendered(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                // Load saves
                var savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", GetSaveSubDir(CurrentGame));

                var saveDirs = Directory.GetDirectories(savePath);
                Dictionary<string, List<ISaveFile>> charNameCareers = new Dictionary<string, List<ISaveFile>>();
                foreach (var saveDir in saveDirs)
                {
                    int numLoaded = 0;
                    foreach (var sf in Directory.GetFiles(saveDir, @"*.pcsav").OrderByDescending(x=>new FileInfo(x).LastWriteTime))
                    {
                        if (numLoaded > 50)
                            break; // Don't load more
                        numLoaded++;

                        using var saveFileS = File.OpenRead(sf);
                        var saveFile = SaveFileLoader.LoadSaveFile(saveFileS, sf);
                        if (!charNameCareers.TryGetValue(saveFile.Proxy_PlayerRecord.Proxy_FirstName, out var list))
                        {
                            list = new List<ISaveFile>();
                            charNameCareers[saveFile.Proxy_PlayerRecord.Proxy_FirstName] = list;
                        }

                        list.Add(saveFile);
                    }
                }

                foreach (var v in charNameCareers)
                {
                    charNameCareers[v.Key] = v.Value.OrderByDescending(x => x.Proxy_TimeStamp).ToList();
                }

                return charNameCareers;
            }).ContinueWithOnUIThread(result =>
            {
                SaveCareers.ReplaceAll(result.Result.Select(x => new Career(x.Value, x.Key)));
                LoadingSaves = false;
            });
        }

        private string GetSaveSubDir(MEGame currentGame)
        {
            switch (currentGame)
            {
                case MEGame.ME1:
                    return @"Mass Effect\Save";
                case MEGame.ME2:
                    return @"Mass Effect 2\Save";
                case MEGame.ME3:
                    return @"Mass Effect 3\Save";
                case MEGame.LE1:
                    return @"Mass Effect Legendary Edition\Save\ME1";
                case MEGame.LE2:
                    return @"Mass Effect Legendary Edition\Save\ME2";
                case MEGame.LE3:
                    return @"Mass Effect Legendary Edition\Save\ME3";
            }

            return null;
        }
    }
}
