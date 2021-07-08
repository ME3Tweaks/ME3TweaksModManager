using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.Helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.save.game2.FileFormats;
using MassEffectModManagerCore.ui;
using Path = System.IO.Path;

namespace MassEffectModManagerCore.modmanager.save.game2.UI
{
    /// <summary>
    /// Interaction logic for SaveSelectorUI.xaml
    /// </summary>
    public partial class SaveSelectorUI : Window, INotifyPropertyChanged
    {
        #region Career
        /// <summary>
        /// Save career as defined by name
        /// </summary>
        public class Career : INotifyPropertyChanged
        {
            public Career(List<SaveFile> saves, string charName)
            {
                CharacterName = charName;
                SaveFiles.ReplaceAll(saves);
            }

            public string CharacterName { get; init; }
            public LegendaryExplorerCore.Misc.ObservableCollectionExtended<SaveFile> SaveFiles { get; } = new LegendaryExplorerCore.Misc.ObservableCollectionExtended<SaveFile>();

#pragma warning disable
            public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore
        }
        #endregion

        /// <summary>
        /// Are saves being read?
        /// </summary>
        public bool LoadingSaves { get; set; }


        public LegendaryExplorerCore.Misc.ObservableCollectionExtended<Career> SaveCareers { get; } = new LegendaryExplorerCore.Misc.ObservableCollectionExtended<Career>();

        /// <summary>
        /// The currently selected Career
        /// </summary>
        public Career SelectedCareer { get; set; }

        public void OnSelectedCareerChanged()
        {
            SelectedSaveFile = SelectedCareer?.SaveFiles.FirstOrDefault();
        }

        public SaveFile SelectedSaveFile { get; set; }

        public void OnSelectedSaveFileChanged()
        {
            //var csi = "";
            //if (SelectedSaveFile == null)
            //{
            //    SelectedLevelText = "Select a save file";
            //    csi = MERUtilities.ListStaticAssets("saveimages", includemerPrefix: true).FirstOrDefault(x => x.EndsWith("nosave.png"));
            //}
            //else
            //{
            //    var sfname = SelectedSaveFile.BaseLevelName.ToLower() + ".png";
            //    var path = MERUtilities.ListStaticAssets("saveimages", includemerPrefix: true).FirstOrDefault(x => x.EndsWith(sfname));
            //    path ??= MERUtilities.ListStaticAssets("saveimages", includemerPrefix: true).FirstOrDefault(x => x.EndsWith("unknown.png")); // could not find asset
            //    csi = path;
            //    SelectedLevelText = LevelNameStringConverter.StaticConvert(SelectedSaveFile.BaseLevelName);

            //}

            //CurrentSaveImage = MERUtilities.LoadImage(MERUtilities.GetEmbeddedStaticFilesBinaryFile(csi, true));
        }

        /// <summary>
        /// The text at the top of the save area
        /// </summary>
        public string SelectedLevelText { get; set; }



        public BitmapImage CurrentSaveImage { get; set; }

        public SaveSelectorUI()
        {
            LoadingSaves = true;
            LoadCommands();
            InitializeComponent();
            OnSelectedSaveFileChanged();
        }


        private void LoadCommands()
        {
            //RefundHenchTalentsCommand = new GenericCommand(RefundHenchTalents, SaveIsSelected);
            //RefundPlayerTalentsCommand = new GenericCommand(RefundPlayerHenchTalents, SaveIsSelected);
            //RefundHenchPlayerTalentsCommand = new GenericCommand(RefundHenchPlayerTalents, SaveIsSelected);
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

        public GenericCommand RefundPlayerTalentsCommand { get; set; }

        public GenericCommand RefundHenchPlayerTalentsCommand { get; set; }

        public GenericCommand RefundHenchTalentsCommand { get; set; }

        private bool SaveIsSelected() => SelectedSaveFile != null;

        private void SSContent_Rendered(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                // Load saves
                var savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BioWare", "Mass Effect 2", "Save");

                var saveDirs = Directory.GetDirectories(savePath);
                Dictionary<string, List<SaveFile>> charNameCareers = new Dictionary<string, List<SaveFile>>();
                foreach (var saveDir in saveDirs)
                {
                    foreach (var sf in Directory.GetFiles(saveDir, "*.pcsav"))
                    {
                        using var saveFileS = File.OpenRead(sf);
                        var saveFile = SaveFile.Load(saveFileS, sf);
                        if (!charNameCareers.TryGetValue(saveFile.PlayerRecord.FirstName, out var list))
                        {
                            list = new List<SaveFile>();
                            charNameCareers[saveFile.PlayerRecord.FirstName] = list;
                        }

                        list.Add(saveFile);
                    }
                }

                foreach (var v in charNameCareers)
                {
                    charNameCareers[v.Key] = v.Value.OrderByDescending(x => x.TimeStamp.ToDate()).ToList();
                }

                return charNameCareers;
            }).ContinueWithOnUIThread(result =>
            {
                SaveCareers.ReplaceAll(result.Result.Select(x => new Career(x.Value, x.Key)));
                LoadingSaves = false;
            });
        }

#pragma warning disable
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore
    }
}
