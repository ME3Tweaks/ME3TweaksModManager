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
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksCore.Config;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
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
                SelectedLevelText = GetTlkStringForLevelName(SelectedSaveFile.Proxy_BaseLevelName);

                var sfname = SelectedSaveFile.Proxy_BaseLevelName.ToLower();
                if (LevelImageCache.TryGetValue(Target.Game, out var cache) && cache.TryGetValue(sfname, out var cachedImage))
                {
                    CurrentSaveImage = cachedImage;
                }
                else
                {
                    // Load into cache
                    LoadSaveImageCache(sfname);
                    if (LevelImageCache.TryGetValue(Target.Game, out var cache2) && cache2.TryGetValue(sfname, out var cachedImage2))
                    {
                        CurrentSaveImage = cachedImage2;
                    }

                }

            }
        }

        private string GetTlkStringForLevelName(string proxyBaseLevelName)
        {
            if (mapToImageAssetMap.TryGetValue(proxyBaseLevelName, out var saveInfo))
            {
                foreach (var tlk in TlkFiles)
                {
                    var data = tlk.FindDataById(saveInfo.TlkStringId, returnNullIfNotFound: true, noQuotes: true);
                    if (data != null) return data;
                }
            }

            return $"Unknown map: {proxyBaseLevelName}";
        }


        private string LangCode = "INT"; // Todo: Support changing this
        private List<ME2ME3LazyTLK> TlkFiles { get; } = new();
        private void LoadTLKs()
        {
            // Load basegame
            var baseTlk = new ME2ME3LazyTLK();
            baseTlk.LoadTlkData(Path.Combine(Target.GetCookedPath(), $"BIOGame_{LangCode}.tlk"));
            TlkFiles.Add(baseTlk);

            // Load DLC
            var dlcs = Target.GetInstalledDLCByMountPriority();
            foreach (var dlc in dlcs)
            {
                var dlcFolderPath = Path.Combine(Target.GetDLCPath(), dlc, Target.Game.CookedDirName());
                var tlks = Directory.EnumerateFiles(dlcFolderPath, $"*{LangCode}.tlk", SearchOption.AllDirectories);
                foreach (var tlk in tlks)
                {
                    var dlcTlk = new ME2ME3LazyTLK();
                    dlcTlk.LoadTlkData(tlk);
                    TlkFiles.Add(dlcTlk);
                }
            }
        }

        private void LoadSaveImageCache(string mapName)
        {
            if (!LevelImageCache.TryGetValue(Target.Game, out _))
            {
                // Initialize
                LevelImageCache[Target.Game] = new CaseInsensitiveDictionary<BitmapSource>();
            }

            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(Target.Game);
            if (mapToImageAssetMap.TryGetValue(mapName, out var saveImageInfo) && loadedFiles.TryGetValue(saveImageInfo.PackageName, out var packagePath))
            {
                using var package = MEPackageHandler.UnsafePartialLoad(packagePath, x =>
                {
                    if (x.InstancedFullPath.CaseInsensitiveEquals(saveImageInfo.FullInstancePath))
                    {
                        return true;
                    }
                    return false;
                });
                var tex = package.FindExport(saveImageInfo.FullInstancePath);

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
                    LevelImageCache[Target.Game][mapName] = (BitmapSource)new ImageSourceConverter().ConvertFrom(memory);
                }
            }
        }

        /// <summary>
        /// The text at the top of the save area
        /// </summary>
        public string SelectedLevelText { get; set; }



        public BitmapSource CurrentSaveImage { get; set; }
        public GameTarget Target { get; }

        public SaveSelectorUI(GameTarget target)
        {
            Target = target;
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
                // Load game information
                var gameConfig = ConfigTools.GetMergedBundle(Target);

                // Build save game image map
                BuildUIAssetMap(gameConfig);

                // Load strings for UI
                LoadTLKs();

                // Load saves
                var savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", GetSaveSubDir(Target.Game));

                var saveDirs = Directory.GetDirectories(savePath);
                Dictionary<string, List<ISaveFile>> charNameCareers = new Dictionary<string, List<ISaveFile>>();
                foreach (var saveDir in saveDirs)
                {
                    int numLoaded = 0;
                    foreach (var sf in Directory.GetFiles(saveDir, @"*.pcsav").OrderByDescending(x => new FileInfo(x).LastWriteTime))
                    {
                        if (numLoaded > 50)
                            break; // Don't load more
                        numLoaded++;

                        using var saveFileS = File.OpenRead(sf);
                        var saveFile = SaveFileLoader.LoadSaveFile(saveFileS, Target.Game, sf);
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

        private class SaveImageAsset2
        {
            public string FullInstancePath { get; set; }

            public string PackageName { get; set; }

            public int TlkStringId { get; set; }
        }

        private CaseInsensitiveDictionary<SaveImageAsset2> mapToImageAssetMap = new();

        /// <summary>
        /// Builds the save-image map from the bundle
        /// </summary>
        /// <param name="getAsset"></param>
        private void BuildUIAssetMap(ConfigAssetBundle bundle)
        {
            //bundle.CommitDLCAssets(@"B:\test");
            // Temp dictionaries as we cross entries mapping seek free entries
            var mapToAssetNameMap = new CaseInsensitiveDictionary<string>();
            var assetNameToSourcePackageMap = new CaseInsensitiveDictionary<string>();
            var mapToStrRefName = new CaseInsensitiveDictionary<int>();


            var bioui = bundle.GetAsset(@"BioUI", false);
            var saveload = bioui.GetOrAddSection(@"sfxgame.sfxsfhandler_save");
            if (saveload.TryGetValue(@"areadata", out var areadata))
            {
                foreach (var mapEntry in areadata)
                {
                    var parms = StringStructParser.GetCommaSplitValues(mapEntry.Value, canBeCaseInsensitive: true);
                    var isUsable = parms.TryGetValue(@"AreaName", out var areaname);
                    isUsable &= parms.TryGetValue(@"ImageName", out var imagename);
                    parms.TryGetValue(@"AreaStrRef", out var areaStrRefStr);

                    if (isUsable)
                    {
                        mapToAssetNameMap[areaname] = imagename;
                        if (int.TryParse(areaStrRefStr, out var strRef))
                        {
                            mapToStrRefName[areaname] = strRef;
                        }
                    }
                }
            }

            if (Target.Game.IsGame2())
            {
                // Game2 uses a prefix property - in OT it overrode package ever DLC release, LE uses just base package of 
                // GUI_SF_SaveLoad
                foreach (var sf in mapToAssetNameMap)
                {
                    mapToImageAssetMap[sf.Key] = new SaveImageAsset2() { FullInstancePath = sf.Value, PackageName = @"GUI_SF_SaveLoad_Images.pcc" };
                }
            }
            else if (Target.Game.IsGame3())
            {
                // Game 3 uses seek free package mapping
                var bioengine = bundle.GetAsset(@"BioEngine", false);
                var sfxengine = bioengine.GetOrAddSection("sfxgame.sfxengine");

                if (sfxengine.TryGetValue(@"dynamicloadmapping", out var dynamicLoadMapping))
                {
                    // Build a temporary map of all entries so we only enumerate twice
                    foreach (var dlm in dynamicLoadMapping)
                    {
                        var parms = StringStructParser.GetCommaSplitValues(dlm.Value, canBeCaseInsensitive: true);
                        assetNameToSourcePackageMap[parms[@"ObjectName"]] = parms[@"SeekFreePackageName"];
                    }
                }

                // Now build a final lookup cache

                foreach (var mapEntry in mapToAssetNameMap)
                {
                    if (assetNameToSourcePackageMap.TryGetValue(mapEntry.Value, out var packageName))
                    {
                        // Found
                        Debug.WriteLine($"Found {mapEntry.Key}");
                        var saveEntry = new SaveImageAsset2() { FullInstancePath = mapEntry.Value, PackageName = packageName + @".pcc" };
                        if (mapToStrRefName.TryGetValue(mapEntry.Key, out var strId))
                        {
                            saveEntry.TlkStringId = strId;
                        }

                        mapToImageAssetMap[mapEntry.Key] = saveEntry;
                    }
                    else
                    {
                        Debug.WriteLine($"MISS {mapEntry.Key}");
                    }
                }
            }
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
