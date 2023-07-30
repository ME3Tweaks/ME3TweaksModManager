using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dark.Net;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Save;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using ME3TweaksCore.Config;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Objects;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.converters;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.save;
using ME3TweaksModManager.modmanager.save.shared;

namespace ME3TweaksModManager.modmanager.windows.input
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

        /// <summary>
        /// Should all saves be loaded or just the first 50 of every career
        /// </summary>
        public bool LoadAllSaves { get; set; } = Settings.SSUILoadAllSaves;

        public void OnLoadAllSavesChanged()
        {
            Settings.SSUILoadAllSaves = LoadAllSaves;
            if (LoadAllSaves)
            {
                ReloadSaves();
            }
        }

        /// <summary>
        /// The list of loaded careers
        /// </summary>
        public ObservableCollectionExtended<Career> SaveCareers { get; } = new();

        /// <summary>
        /// The currently selected Career
        /// </summary>
        public Career SelectedCareer { get; set; }

        public void OnSelectedCareerChanged(object oldValue, object newValue)
        {
            if (oldValue != null)
            {
                // Only update the selected save file if we are changing by user - on first load
                // we want to use the pre-populated one
                SelectedSaveFile = SelectedCareer?.SaveFiles.FirstOrDefault();
            }
            if (SelectedCareer != null)
            {
                CareerSaveFiles.ReplaceAll(SelectedCareer.SaveFiles);
            }
            else
            {
                CareerSaveFiles.ClearEx();
            }
            SaveFilterText = @"";
        }

        /// <summary>
        /// The currently selected save file
        /// </summary>
        public ISaveFile SelectedSaveFile { get; set; }

        /// <summary>
        /// A cache of Map name -> texture to display
        /// </summary>
        public Dictionary<MEGame, CaseInsensitiveDictionary<BitmapSource>> LevelImageCache { get; } = new();

        public void OnSelectedSaveFileChanged()
        {
            if (SelectedSaveFile == null)
            {
                SelectedLevelText = M3L.GetString(M3L.string_selectASaveFile);
                CurrentSaveImage = null;
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
                    else
                    {
                        // Unknown map image
                        // Load the placeholder
                        CurrentSaveImage = GetDefaultMapImage();
                    }
                }
            }
        }

        public string CareerFilterText { get; set; }
        public void OnCareerFilterTextChanged() { SaveCareersView.Refresh(); }
        public ICollectionView SaveCareersView => CollectionViewSource.GetDefaultView(SaveCareers);
        private bool FilterCareers(object obj)
        {
            if (!string.IsNullOrWhiteSpace(CareerFilterText) && obj is Career bobj)
            {
                if (string.IsNullOrWhiteSpace(bobj.CharacterName)) return true; // If char is initialized with console commands this can be null

                if (bobj.CharacterName.Contains(CareerFilterText, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }

                return false;
            }
            return true;
        }

        // This is not just bound to ISaveFile because we have to have a collection view over it
        public ObservableCollectionExtended<ISaveFile> CareerSaveFiles { get; } = new();
        public string SaveFilterText { get; set; }
        public void OnSaveFilterTextChanged() { CareerSaveFilesView.Refresh(); }
        public ICollectionView CareerSaveFilesView => CollectionViewSource.GetDefaultView(CareerSaveFiles);
        private bool FilterSaves(object obj)
        {
            if (!string.IsNullOrWhiteSpace(SaveFilterText) && obj is ISaveFile isf)
            {
                if (SaveGameNameConverter.StaticConvert(isf).Contains(SaveFilterText, StringComparison.InvariantCultureIgnoreCase))
                    return true;

                if (!string.IsNullOrWhiteSpace(isf.Proxy_DebugName) &&
                    isf.Proxy_DebugName.Contains(SaveFilterText, StringComparison.InvariantCultureIgnoreCase))
                    return true;

                if (!string.IsNullOrWhiteSpace(isf.Proxy_BaseLevelName) &&
                    isf.Proxy_BaseLevelName.Contains(SaveFilterText, StringComparison.InvariantCultureIgnoreCase))
                    return true;

                return false;
            }
            return true;
        }


        private BitmapImage UnknownMapImage;

        private BitmapImage GetDefaultMapImage()
        {
            if (UnknownMapImage != null) return UnknownMapImage;
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            UnknownMapImage = new BitmapImage();
            using (var stream = M3Utilities.GetResourceStream(@"ME3TweaksModManager.images.unknownmap.png"))
            {
                UnknownMapImage.BeginInit();
                UnknownMapImage.StreamSource = stream;
                UnknownMapImage.CacheOption = BitmapCacheOption.OnLoad;
                UnknownMapImage.EndInit();
                UnknownMapImage.Freeze();
            }

            return UnknownMapImage;
        }

        private string GetTlkStringForLevelName(string proxyBaseLevelName)
        {
            if (mapToImageAssetMap.TryGetValue(proxyBaseLevelName, out var saveInfo))
            {
                if (Target.Game == MEGame.LE1)
                {
                    // Look in LE1 TLKs
                    foreach (var tlk in TlkFilesLE1)
                    {
                        var data = tlk.FindDataById(saveInfo.TlkStringId, returnNullIfNotFound: true, noQuotes: true);
                        if (data != null) return data;
                    }
                }
                else
                {
                    foreach (var tlk in TlkFiles)
                    {
                        var data = tlk.FindDataById(saveInfo.TlkStringId, returnNullIfNotFound: true, noQuotes: true);
                        if (data != null) return data;
                    }
                }
            }

            return M3L.GetString(M3L.string_interp_unknownMapX, proxyBaseLevelName);
        }


        private string LangCode = Settings.Language.ToUpper();

        private List<ME2ME3LazyTLK> TlkFiles { get; } = new();
        private List<ME1TalkFile> TlkFilesLE1 { get; } = new();
        private void LoadTLKs()
        {
            M3Log.Information($@"SSUI: Loading TLK data from game");
            if (Target.Game == MEGame.LE1)
            {
                // oh lord.

                // Load basegame
                var baseTlk = LoadTlkFromPackage(Path.Combine(Target.GetCookedPath(), $@"Startup_{GameLanguage.ConvertModManagerLocToGameLoc(LangCode, MEGame.LE1)}.pcc"), @"GlobalTlk.GlobalTlk_tlk");
                if (baseTlk == null && LangCode != @"INT")
                {
                    M3Log.Information($@"Could not find Startup_{LangCode}.pcc, changing over to INT");
                    // Force onto INT - localized startup package was not found 
                    // Game may be in different lang than M3
                    LangCode = @"INT";
                    baseTlk = LoadTlkFromPackage(Path.Combine(Target.GetCookedPath(), $@"Startup_{LangCode}.pcc"), @"GlobalTlk.GlobalTlk_tlk");
                }

                if (baseTlk != null)
                    TlkFilesLE1.Add(baseTlk);

                var dlcs = Target.GetInstalledDLCByMountPriority();
                var loadedFiles = Target.GetFilesLoadedInGame();

                foreach (var dlc in dlcs)
                {
                    var dlcFolderRoot = Path.Combine(Target.GetDLCPath(), dlc);
                    var autoLoadIni = DuplicatingIni.LoadIni(Path.Combine(dlcFolderRoot, @"AutoLoad.ini"));

                    var packages = autoLoadIni.GetOrAddSection(@"Packages");
                    int i = 1;
                    while (true)
                    {
                        var twoDKey = $@"GlobalTalkTable{i}";
                        var path = packages.GetValue(twoDKey);
                        if (path == null)
                            break;

                        if (path.Value.IndexOf('.') == 0)
                            break; // This won't work

                        var packageName = path.Value.Substring(0, path.Value.IndexOf('.')) + @".pcc";
                        var ifp = path.Value.Substring(path.Value.IndexOf('.') + 1);
                        if (loadedFiles.TryGetValue(packageName, out var packagePath))
                        {
                            var dlcTlk = LoadTlkFromPackage(packagePath, ifp);
                            if (dlcTlk != null)
                                TlkFilesLE1.Add(dlcTlk);
                        }

                        i++;
                    }
                }
            }
            else if (Target.Game.IsGame2() || Target.Game.IsGame3())
            {
                // Load basegame
                var baseTlkPath = Path.Combine(Target.GetCookedPath(), $@"BIOGame_{LangCode}.tlk");
                var baseTlk = new ME2ME3LazyTLK();
                if (!File.Exists(baseTlkPath))
                {
                    if (LangCode != @"INT")
                    {
                        M3Log.Information($@"Could not find BIOGame_{LangCode}.tlk, changing over to INT");
                        // Force onto INT - tlk
                        // Game may be in different lang than M3
                        LangCode = @"INT";
                        baseTlkPath = Path.Combine(Target.GetCookedPath(), $@"BIOGame_{LangCode}.tlk");
                    }
                }

                if (File.Exists(baseTlkPath))
                {
                    baseTlk.LoadTlkData(Path.Combine(Target.GetCookedPath(), $@"BIOGame_{LangCode}.tlk"));
                    TlkFiles.Add(baseTlk);
                }
                else
                {
                    M3Log.Warning($@"Could not find basegame TLK: {baseTlkPath}, we will not load strings from this file");
                }

                // Load DLC
                var dlcs = Target.GetInstalledDLCByMountPriority();
                foreach (var dlc in dlcs)
                {
                    var dlcFolderPath = Path.Combine(Target.GetDLCPath(), dlc, Target.Game.CookedDirName());
                    var tlks = Directory.EnumerateFiles(dlcFolderPath, $@"*{LangCode}.tlk", SearchOption.AllDirectories);
                    foreach (var tlk in tlks)
                    {
                        var dlcTlk = new ME2ME3LazyTLK();
                        dlcTlk.LoadTlkData(tlk);
                        TlkFiles.Add(dlcTlk);
                    }
                }
            }
        }

        private ME1TalkFile LoadTlkFromPackage(string filePath, string instancedFullPath)
        {
            if (!File.Exists(filePath)) return null;
            using var pack = MEPackageHandler.UnsafePartialLoad(filePath, x => x.ClassName == @"BioTlkFile");
            var tlk = pack.FindExport(instancedFullPath);
            if (tlk != null)
            {
                return new ME1TalkFile(tlk);
            }

            return null; // Not found
        }

        private void LoadSaveImageCache(string mapName)
        {
            if (!LevelImageCache.TryGetValue(Target.Game, out _))
            {
                // Initialize
                LevelImageCache[Target.Game] = new CaseInsensitiveDictionary<BitmapSource>();
            }

            M3Log.Information($@"SSUI: Trying to load image for map {mapName}");
            var loadedFiles = Target.GetFilesLoadedInGame();
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
                if (tex == null)
                {
                    // We might have to lop-off and update the package name
                    var dotIndex = saveImageInfo.FullInstancePath.IndexOf('.');
                    if (dotIndex > 0)
                    {
                        var seekFreePackageName = saveImageInfo.FullInstancePath.Substring(0, dotIndex) + @".pcc";
                        var ifp = saveImageInfo.FullInstancePath.Substring(dotIndex + 1);
                        if (loadedFiles.TryGetValue(seekFreePackageName, out var seekFreePath))
                        {
                            using var seekFreePackage = MEPackageHandler.UnsafePartialLoad(seekFreePath, x =>
                            {
                                if (x.InstancedFullPath.CaseInsensitiveEquals(ifp))
                                {
                                    return true;
                                }
                                return false;
                            });
                            tex = seekFreePackage.FindExport(ifp);
                        }
                    }
                }


                if (tex != null && tex.ClassName == @"GFxMovieInfo")
                {
                    // We need to do a secondary read on file to get actual data
                    // Sadly we can't do this in one pass without extensive changes
                    // to how unsafe partial load works
                    M3Log.Information($@"SSUI: Performing 'references' lookup for actual Texture2D");

                    var refs = tex.GetProperty<ArrayProperty<ObjectProperty>>(@"References").Select(x => x.ResolveToEntry(tex.FileRef).InstancedFullPath).ToList();
                    if (refs.Count == 1)
                    {
                        using var package2 = MEPackageHandler.UnsafePartialLoad(tex.FileRef.FilePath, x =>
                        {
                            return refs.Contains(x.InstancedFullPath); // Load our references
                        });
                        tex = package2.FindExport(refs[0]); // Use this texture instead.
                    }



                }


                if (tex != null)
                {
                    M3Log.Information($@"SSUI: Fetching texture data from export {tex.InstancedFullPath}");
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

        public SaveSelectorUI(Window owner, GameTarget target, string titleTextSuffix = null)
        {
            M3Log.Information($@"Opening SaveSelectorUI for target {target.TargetPath}. Reason: {titleTextSuffix}");
            Owner = owner;
            Target = target;
            LoadingSaves = true;
            Title = M3L.GetString(M3L.string_saveSelector);
            if (!string.IsNullOrWhiteSpace(titleTextSuffix))
            {
                Title += $@" - {titleTextSuffix}";
            }
            LoadCommands();
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();

            SaveCareersView.Filter = FilterCareers;
            CareerSaveFilesView.Filter = FilterSaves;
            OnSelectedSaveFileChanged();
        }


        private void LoadCommands()
        {
            SelectSaveCommand = new GenericCommand(SelectSave, CanSelectSave);
            CancelSelectionCommand = new GenericCommand(CancelSelection);
            //RefundHenchTalentsCommand = new GenericCommand(RefundHenchTalents, SaveIsSelected);
            //RefundPlayerTalentsCommand = new GenericCommand(RefundPlayerHenchTalents, SaveIsSelected);
            //RefundHenchPlayerTalentsCommand = new GenericCommand(RefundHenchPlayerTalents, SaveIsSelected);
        }

        private void CancelSelection()
        {
            M3Log.Information($@"SSUI: Closing due to cancelation");
            SaveWasSelected = false;
            Close();
        }

        private void SelectSave()
        {
            M3Log.Information($@"SSUI: Closing with save selected: {SelectedSaveFile.SaveFilePath}");
            SaveWasSelected = true;
            Close();
        }

        private bool CanSelectSave()
        {
            return SaveIsSelected() && (SelectedSaveFile.Game == MEGame.LE1 || SelectedSaveFile.Game.IsGame2() || SelectedSaveFile.Game.IsGame3());
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
        public ICommand CancelSelectionCommand { get; set; }

        public bool SaveWasSelected { get; set; }

        private bool SaveIsSelected() => SelectedSaveFile != null;

        private void SSContent_Rendered(object sender, EventArgs e)
        {
            ReloadSaves();
        }

        private void ReloadSaves()
        {
            // Mark it here as well
            LoadingSaves = true;
            SelectedSaveFile = null;
            SelectedCareer = null;

            Task.Run(() =>
            {
                M3Log.Information($@"SSUI: Beginning content load");

                // Load game information
                var game = Target.Game;
                Target.GetFilesLoadedInGame(forceReload: true); // Reload the list of files


                if (game == MEGame.LE1)
                {
                    // Good old 2DA
                    BuildUIAssetMapLE1();
                }
                else if (game.IsGame2() || game.IsGame3())
                {
                    var gameConfig = ConfigTools.GetMergedBundle(Target);

                    // Build save game image map
                    BuildUIAssetMap(gameConfig);
                }
                else
                {
                    throw new Exception($@"SSUI does not work for game {game}");
                }

                // Load strings for UI
                LoadTLKs();

                // Load profile to find latest save, so we can pre-select it in the UI
                var savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"BioWare", GetSaveSubDir(Target.Game));

                string resumeSavePath = null;
                try
                {
                    var profileFile = MEDirectories.GetProfileSave(Target.Game);
                    if (File.Exists(profileFile))
                    {
                        if (Target.Game == MEGame.LE1)
                        {
                            // LE1 stores path directly.
                            var lp = LocalProfileLE1.DeserializeLocalProfile(profileFile);
                            var lastSaveCareer = lp.GamerProfile.LastPlayedCharacterID;
                            var lastSaveName = lp.GamerProfile.LastSaveGame;
                            resumeSavePath = BuildLastSavePathLE1(savePath, lastSaveCareer, lastSaveName);
                        }
                        else if (Target.Game == MEGame.LE2 || Target.Game == MEGame.LE3)
                        {
                            LocalProfile lp = LocalProfile.DeserializeLocalProfile(profileFile, Target.Game);
                            var lastSaveCareer = lp.ProfileSettings[game == MEGame.LE2 ? (int)LocalProfile.ELE2ProfileSetting.Setting_CurrentCareer : (int)LocalProfile.ELE3ProfileSetting.Setting_CurrentCareer].DataAsString;
                            var lastSaveNumeral = lp.ProfileSettings[game == MEGame.LE2 ? (int)LocalProfile.ELE2ProfileSetting.Setting_CurrentSaveGame : (int)LocalProfile.ELE3ProfileSetting.Setting_CurrentSaveGame].DataAsInt;
                            resumeSavePath = BuildLastSavePath(savePath, lastSaveCareer, lastSaveNumeral);
                        }
                    }
                }
                catch
                {
                    M3Log.Warning(@"Failed to get latest save information from game local profile. Skipping auto-selection");
                }


                // Load saves
                var saveDirs = Directory.GetDirectories(savePath);
                Dictionary<string, List<ISaveFile>> charNameCareers = new Dictionary<string, List<ISaveFile>>();
                foreach (var saveDir in saveDirs)
                {
                    int numLoaded = 0;
                    foreach (var sf in Directory.GetFiles(saveDir, @"*.pcsav").OrderByDescending(x => new FileInfo(x).LastWriteTime))
                    {
                        if (game == MEGame.LE1)
                        {
                            var fname = Path.GetFileName(sf);
                            if (fname.CaseInsensitiveEquals(@"GamerProfile.pcsav"))
                                continue; // We do not work on these save files (in this UI anyways)
                        }

                        // Only load the most recent 50 saves if we are doing an optimized load
                        // Cause this uses a ton of allocations
                        // Also make sure we load the specific resume file if its somehow not in the top 50
                        if (!Settings.SSUILoadAllSaves && numLoaded >= 50 && !sf.CaseInsensitiveEquals(resumeSavePath))
                            break;

                        numLoaded++;
                        try
                        {
                            using var saveFileS = File.OpenRead(sf);
                            var saveFile = SaveFileLoader.LoadSaveFile(saveFileS, sf);
                            if (saveFile == null)
                            {
                                continue; // We do not parse this
                            }
                            if (!charNameCareers.TryGetValue(saveFile.Proxy_PlayerRecord.Proxy_FirstName, out var list))
                            {
                                list = new List<ISaveFile>();
                                charNameCareers[saveFile.Proxy_PlayerRecord.Proxy_FirstName] = list;
                            }

                            list.Add(saveFile);

                            if (resumeSavePath != null && sf.CaseInsensitiveEquals(resumeSavePath))
                            {
                                SelectedSaveFile = saveFile;
                            }
                        }
                        catch (Exception ex)
                        {
                            M3Log.Warning($@"Error parsing save file: {sf}. This may be due to bugs in the save parsing code (it's still new). This save will be skipped: {ex.Message}");
                        }
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
                if (SelectedSaveFile != null)
                {
                    SelectedCareer = SaveCareers.FirstOrDefault(x => x.SaveFiles.Contains(SelectedSaveFile));
                }
                LoadingSaves = false;
            });
        }

        private string BuildLastSavePathLE1(string savePath, string lastSaveCareer, string lastSaveName)
        {
            return Path.Combine(savePath, lastSaveCareer, lastSaveName + @".pcsav");
        }

        // This is only used for LE2/LE3 as LE1 stores the full filename
        private string BuildLastSavePath(string savePath, string lastSaveCareer, int lastSaveNumeral)
        {
            var fSavePath = Path.Combine(savePath, lastSaveCareer);

            // Determine save game type
            var saveGameType = lastSaveNumeral / 1000000;
            switch (saveGameType)
            {
                case 0: // Manual
                    return Path.Combine(fSavePath, $@"Save_{lastSaveNumeral.ToString().PadLeft(4, '0')}.pcsav");
                case 1: // Quicksave
                    return Path.Combine(fSavePath, @"QuickSave.pcsav");
                case 2: // Auto
                    return Path.Combine(fSavePath, @"AutoSave.pcsav");
                case 3: // Chapter
                    return Path.Combine(fSavePath, @"ChapterSave.pcsav");
                case 4: // Export
                    return Path.Combine(fSavePath, @"NewGamePlusSave.pcsav"); // Not entirely sure this is what 'Export' means
                case 5: // Legend
                    return Path.Combine(fSavePath, @"LegendSave.pcsav");
            }

            return null;
        }

        private void BuildUIAssetMapLE1()
        {
            M3Log.Information($@"SSUI: Building LE1 UI Asset Map");

            var loadedFiles = Target.GetFilesLoadedInGame();
            var twoDAsToInspect = new SortedSet<string>(); // In case duplicates are added somehow

            // Basegame
            twoDAsToInspect.Add(loadedFiles[@"Engine.pcc"]);
            twoDAsToInspect.Add(loadedFiles[@"BIOG_2DA_UNC_AreaMap_X.pcc"]); // Bring Down the Sky

            // Get DLC 2DA packages
            var dlcs = Target.GetInstalledDLCByMountPriority();
            foreach (var dlc in dlcs)
            {
                var dlcFolderRoot = Path.Combine(Target.GetDLCPath(), dlc);
                var autoLoadIni = DuplicatingIni.LoadIni(Path.Combine(dlcFolderRoot, @"AutoLoad.ini"));

                var packages = autoLoadIni.GetOrAddSection(@"Packages");
                int i = 1;
                while (true)
                {
                    var twoDKey = $@"2DA{i}";
                    var path = packages.GetValue(twoDKey);
                    if (path == null)
                        break;

                    var filename = path.Value + @".pcc";
                    if (loadedFiles.TryGetValue(filename, out var fullPath))
                    {
                        M3Log.Information($@"SSUI: LE1: Found 2DA to inspect: {filename}");
                        twoDAsToInspect.Add(fullPath); // Inspect this 2DA package from the DLC
                    }

                    i++;
                }
            }


            foreach (var twoDA in twoDAsToInspect)
            {
                // Load areamap
                M3Log.Information($@"SSUI: Looking for areamap 2DA data in {twoDA}");

                using var package = MEPackageHandler.UnsafePartialLoad(twoDA,
                    x => x.ClassName == @"Bio2DANumberedRows" && x.ObjectName.Instanced.Contains(@"AreaMap_AreaMap", StringComparison.InvariantCultureIgnoreCase));
                foreach (var exp in package.Exports.Where(x => x.IsDataLoaded()))
                {
                    var parsed2DA = new Bio2DA(exp);

                    for (int i = 0; i < parsed2DA.RowCount; i++)
                    {

                        var asset = new SaveImageAsset2()
                        {
                            TlkStringId = parsed2DA[i, @"SaveGameStringRef"].IntValue,
                            FullInstancePath = parsed2DA[i, @"SaveGameImage"].NameValue,
                            PackageName = @"Startup_INT.pcc" // Default to this, we will failover if not found to trying to find it via non-forced export
                        };

                        mapToImageAssetMap[parsed2DA[i, @"Label"].NameValue] = asset;
                    }
                }
            }
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

            M3Log.Information($@"SSUI: Building LE2/LE3 UI Asset Map");

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
                // Game2 uses a prefix property - in OT it overrode package every DLC release, LE uses just base package of 
                // GUI_SF_SaveLoad
                foreach (var sf in mapToAssetNameMap)
                {
                    var saveEntry = new SaveImageAsset2() { FullInstancePath = sf.Value, PackageName = @"GUI_SF_SaveLoad_Images.pcc" };
                    if (mapToStrRefName.TryGetValue(sf.Key, out var strId))
                    {
                        saveEntry.TlkStringId = strId;
                    }

                    mapToImageAssetMap[sf.Key] = saveEntry;
                }
            }
            else if (Target.Game.IsGame3())
            {
                // Game 3 uses seek free package mapping
                var bioengine = bundle.GetAsset(@"BioEngine", false);
                var sfxengine = bioengine.GetOrAddSection(@"sfxgame.sfxengine");

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
                        Debug.WriteLine($@"Found {mapEntry.Key}");
                        var saveEntry = new SaveImageAsset2() { FullInstancePath = mapEntry.Value, PackageName = packageName + @".pcc" };
                        if (mapToStrRefName.TryGetValue(mapEntry.Key, out var strId))
                        {
                            saveEntry.TlkStringId = strId;
                        }

                        mapToImageAssetMap[mapEntry.Key] = saveEntry;
                    }
                    else
                    {
                        Debug.WriteLine($@"MISS {mapEntry.Key}");
                    }
                }
            }
        }

        public static string GetSaveSubDir(MEGame currentGame)
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
