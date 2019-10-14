using MassEffectModManager;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FontAwesome.WPF;
using MassEffectModManagerCore.GameDirectories;
using MassEffectModManagerCore.gamefileformats.unreal;
using MassEffectModManagerCore.modmanager.windows;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using Microsoft.AppCenter.Analytics;
using SevenZip;
using Brushes = System.Windows.Media.Brushes;
using UserControl = System.Windows.Controls.UserControl;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for ArchiveDeployment.xaml
    /// </summary>
    public partial class ArchiveDeployment : UserControl, INotifyPropertyChanged
    {
        private MainWindow mainWindow;

        public Mod ModBeingDeployed { get; }

        public ArchiveDeployment(Mod mod, MainWindow mainWindow)
        {
            Analytics.TrackEvent("Started deployment for mod", new Dictionary<string, string>()
            {
                { "Mod name" , mod.ModName + " " + mod.ParsedModVersion}
            });
            DataContext = this;
            this.mainWindow = mainWindow;
            ModBeingDeployed = mod;

            DeploymentChecklistItems.Add(new DeploymentChecklistItem() { ItemText = "Verify mod version is correct: " + mod.ParsedModVersion, ValidationFunction = ManualValidation });
            DeploymentChecklistItems.Add(new DeploymentChecklistItem() { ItemText = "Verify URL is correct: " + mod.ModWebsite, ValidationFunction = ManualValidation });
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = "Verify mod description accuracy",
                ValidationFunction = ManualValidation

            });
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = "Audio check",
                ModToValidateAgainst = mod,
                ValidationFunction = CheckModForAFCCompactability,
                ErrorsMessage = "The audio check detected errors while attempting to verify all referenced audio will be usable by end users. Review these issues and fix them if applicable before deployment.",
                ErrorsTitle = "Audio issues detected in mod"
            });
            DeploymentChecklistItems.Add(new DeploymentChecklistItem()
            {
                ItemText = "Textures check",
                ModToValidateAgainst = mod,
                ErrorsMessage = "The textures check detected errors while attempting to load all textures in the mod. Review these issues and fix them before deployment.",
                ErrorsTitle = "Texture errors in mod",
                ValidationFunction = CheckModForTFCCompactability
            });
            LoadCommands();
            InitializeComponent();
            NamedBackgroundWorker bw = new NamedBackgroundWorker("DeploymentValidation");
            bw.DoWork += (a, b) =>
                {
                    foreach (var checkItem in DeploymentChecklistItems)
                    {
                        checkItem.ExecuteValidationFunction();
                    }
                };
            bw.RunWorkerAsync();
        }

        private void ManualValidation(DeploymentChecklistItem item)
        {
            item.Foreground = Brushes.Gray;
            item.Icon = FontAwesomeIcon.CheckCircle;
            item.ToolTip = "This item must be manually checked by you";
        }

        private void CheckModForAFCCompactability(DeploymentChecklistItem item)
        {
            bool hasError = false;
            item.HasError = false;
            item.ItemText = "Checking audio references in mod";
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
            int numChecked = 0;
            GameTarget validationTarget = mainWindow.InstallationTargets.FirstOrDefault(x => x.Game == ModBeingDeployed.Game);
            List<string> gameFiles = MEDirectories.EnumerateGameFiles(validationTarget.Game, validationTarget.TargetPath);

            var errors = new List<string>();
            Dictionary<string, MemoryStream> cachedAudio = new Dictionary<string, MemoryStream>();

            foreach (var f in referencedFiles)
            {
                numChecked++;
                item.ItemText = $"Checking audio references in mod [{numChecked}/{referencedFiles.Count}]";
                if (f.RepresentsPackageFilePath())
                {
                    var package = MEPackageHandler.OpenMEPackage(f);
                    var wwiseStreams = package.Exports.Where(x => x.ClassName == "WwiseStream" && !x.IsDefaultObject).ToList();
                    foreach (var wwisestream in wwiseStreams)
                    {
                        //Check each reference.
                        var afcNameProp = wwisestream.GetProperty<NameProperty>("Filename");
                        if (afcNameProp != null)
                        {
                            string afcNameWithExtension = afcNameProp + ".afc";
                            int audioSize = BitConverter.ToInt32(wwisestream.Data, wwisestream.Data.Length - 8);
                            int audioOffset = BitConverter.ToInt32(wwisestream.Data, wwisestream.Data.Length - 4);

                            string afcPath = null;
                            Stream audioStream = null;
                            var localDirectoryAFCPath = Path.Combine(Path.GetDirectoryName(wwisestream.FileRef.FilePath), afcNameWithExtension);
                            bool isInOfficialArea = false;
                            if (File.Exists(localDirectoryAFCPath))
                            {
                                //local afc
                                afcPath = localDirectoryAFCPath;

                            }
                            else
                            {
                                //Check game
                                var fullPath = gameFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(afcNameWithExtension, StringComparison.InvariantCultureIgnoreCase));
                                if (fullPath != null)
                                {
                                    afcPath = fullPath;
                                    isInOfficialArea = MEDirectories.IsInBasegame(afcPath, validationTarget) || MEDirectories.IsInOfficialDLC(afcPath, validationTarget);
                                }
                                else if (cachedAudio.TryGetValue(afcNameProp.Value.Name, out var cachedAudioStream))
                                {
                                    audioStream = cachedAudioStream;
                                    isInOfficialArea = true;
                                }
                                else if (MEDirectories.OfficialDLC(validationTarget.Game).Any(x => afcNameProp.Value.Name.StartsWith(x)))
                                {
                                    var dlcName = afcNameProp.Value.Name.Substring(0, afcNameProp.Value.Name.LastIndexOf("_", StringComparison.InvariantCultureIgnoreCase));
                                    var audio = VanillaDatabaseService.FetchFileFromVanillaSFAR(validationTarget, dlcName, afcNameWithExtension);
                                    if (audio != null)
                                    {
                                        cachedAudio[afcNameProp.Value.Name] = audio;
                                    }

                                    audioStream = audio;
                                    isInOfficialArea = true;
                                    continue;
                                }
                                else
                                {
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    errors.Add($"{wwisestream.FileRef.FilePath} - {wwisestream.GetInstancedFullPath}: Could not find referenced audio file cache: {afcNameProp}");
                                    continue;
                                }
                            }

                            if (afcPath != null)
                            {
                                audioStream = new FileStream(afcPath, FileMode.Open);
                            }

                            try
                            {
                                audioStream.Seek(audioOffset, SeekOrigin.Begin);
                                if (audioStream.ReadStringASCIINull(4) != "RIFF")
                                {
                                    hasError = true;
                                    item.Icon = FontAwesomeIcon.TimesCircle;
                                    item.Foreground = Brushes.Red;
                                    item.Spinning = false;
                                    errors.Add($"{wwisestream.FileRef.FilePath} - {wwisestream.GetInstancedFullPath}: Invalid audio pointer, does not point to RIFF header");
                                    if (audioStream is FileStream) audioStream.Close();
                                    continue;
                                }

                                //attempt to seek audio length.
                                audioStream.Seek(audioSize + 4, SeekOrigin.Current);

                                //Check if this file is in basegame
                                if (isInOfficialArea)
                                {
                                    //Verify offset is not greater than vanilla size
                                    //TODO: Awaiting vanilla database update from Mass Effect Modder
                                    int vanillaSize = 1000000000;
                                    if (audioOffset >= vanillaSize)
                                    {
                                        hasError = true;
                                        item.Icon = FontAwesomeIcon.TimesCircle;
                                        item.Foreground = Brushes.Red;
                                        item.Spinning = false;
                                        errors.Add($"{wwisestream.FileRef.FilePath} - {wwisestream.GetInstancedFullPath}: This audio is in a basegame/DLC AFC but is not vanilla audio. End-users will not have this audio available. Use AFC compactor in ME3Explorer to pull this audio locally into an AFC for this mod.");
                                    }
                                }
                                if (audioStream is FileStream) audioStream.Close();
                            }
                            catch (Exception e)
                            {
                                hasError = true;
                                item.Icon = FontAwesomeIcon.TimesCircle;
                                item.Foreground = Brushes.Red;
                                item.Spinning = false;
                                errors.Add($"{wwisestream.FileRef.FilePath} - {wwisestream.GetInstancedFullPath}: Error validating audio reference: {e.Message}");
                                continue;
                            }
                        }
                    }
                }
            }


            if (!hasError)
            {
                item.Foreground = Brushes.Green;
                item.Icon = FontAwesomeIcon.CheckCircle;
                item.ItemText = "No audio issues were detected";
                item.ToolTip = "Validation passed";
            }
            else
            {
                item.Errors = errors;
                item.ItemText = "Audio issues were detected";
                item.ToolTip = "Validation failed";
            }
            item.HasError = hasError;
            cachedAudio.Clear();
        }

        private void CheckModForTFCCompactability(DeploymentChecklistItem item)
        {
            if (ModBeingDeployed.Game >= Mod.MEGame.ME2)
            {
                bool hasError = false;
                item.HasError = false;
                item.ItemText = "Checking textures in mod";
                var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
                int numChecked = 0;
                GameTarget validationTarget = mainWindow.InstallationTargets.FirstOrDefault(x => x.Game == ModBeingDeployed.Game);
                var errors = new List<string>();
                foreach (var f in referencedFiles)
                {
                    numChecked++;
                    item.ItemText = $"Checking textures in mod [{numChecked}/{referencedFiles.Count}]";
                    if (f.RepresentsPackageFilePath())
                    {
                        var package = MEPackageHandler.OpenMEPackage(f);
                        var textures = package.Exports.Where(x => x.IsTexture()).ToList();
                        foreach (var texture in textures)
                        {
                            var cache = texture.GetProperty<NameProperty>("TextureFileCacheName");
                            if (cache != null)
                            {
                                if (!VanillaDatabaseService.IsBasegameTFCName(cache.Value, ModBeingDeployed.Game))
                                {
                                    var mips = Texture2D.GetTexture2DMipInfos(texture, cache.Value);
                                    Texture2D tex = new Texture2D(texture);
                                    try
                                    {
                                        tex.GetImageBytesForMip(tex.GetTopMip(), validationTarget, false); //use active target
                                    }
                                    catch (Exception e)
                                    {
                                        hasError = true;
                                        item.Icon = FontAwesomeIcon.TimesCircle;
                                        item.Foreground = Brushes.Red;
                                        item.Spinning = false;
                                        errors.Add($"{texture.FileRef.FilePath} - {texture.GetInstancedFullPath}: Could not load texture data: {e.Message}");
                                    }
                                }
                            }
                        }
                    }
                }

                if (!hasError)
                {
                    item.Foreground = Brushes.Green;
                    item.Icon = FontAwesomeIcon.CheckCircle;
                    item.ItemText = "No broken textures were found";
                    item.ToolTip = "Validation passed";
                }
                else
                {
                    item.Errors = errors;
                    item.ItemText = "Texture issues were detected";
                    item.ToolTip = "Validation failed";
                }
                item.HasError = hasError;
            }
        }

        public ICommand DeployCommand { get; set; }
        private void LoadCommands()
        {
            DeployCommand = new GenericCommand(StartDeployment, CanDeploy);
        }

        private void StartDeployment()
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker("ModDeploymentThread");
            bw.DoWork += Deployment_BackgroundThread;
            bw.RunWorkerAsync();
            mainWindow.UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VISIBILITY, Visibility.Visible));
            mainWindow.UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_INDETERMINATE, false));
            mainWindow.UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_MAX, 100));
        }

        private void Deployment_BackgroundThread(object sender, DoWorkEventArgs e)
        {
            var referencedFiles = ModBeingDeployed.GetAllRelativeReferences();

            string archivePath = @"C:\users\public\deploytest.7z";
            //Key is in-archive path, value is on disk path
            var archiveMapping = new Dictionary<string, string>();
            SortedSet<string> directories = new SortedSet<string>();
            foreach (var file in referencedFiles)
            {
                string[] split = file.Split('\\');
                if (split.Length < 2) continue; //not a directory
                split = split.Take(split.Length - 1).ToArray();
                string folderpath = string.Join('\\', split);
                if (directories.Add(folderpath))
                {
                    archiveMapping[folderpath] = null;
                }
            }

            archiveMapping.AddRange(referencedFiles.ToDictionary(x => x, x => Path.Combine(ModBeingDeployed.ModPath, x)));

            var compressor = new SevenZip.SevenZipCompressor();
            compressor.CompressionLevel = CompressionLevel.Ultra;
            //compressor.CustomParameters.Add("s", "on");
            compressor.Progressing += (a, b) =>
            {
                mainWindow.UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_MAX, b.TotalAmount));
                mainWindow.UpdateBusyProgressBarCallback(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VALUE, b.AmountCompleted));
            };
            compressor.FileCompressionStarted += (a, b) => { Debug.WriteLine(b.FileName); };
            compressor.CompressFileDictionary(archiveMapping, archivePath);
            Debug.WriteLine("Now compressing moddesc.ini...");
            compressor.CompressionMode = CompressionMode.Append;
            compressor.CompressionLevel = CompressionLevel.None;
            compressor.CompressFiles(archivePath, new string[]
            {
                Path.Combine(ModBeingDeployed.ModPath, "moddesc.ini")
            });
            Utilities.HighlightInExplorer(archivePath);
        }

        private bool CanDeploy()
        {
            return true;
        }



        #region Closing and INotify
        public event EventHandler<DataEventArgs> Close;
        public event PropertyChangedEventHandler PropertyChanged;
        private void Close_Clicked(object sender, RoutedEventArgs e)
        {
            OnClosing(new DataEventArgs());
        }
        protected virtual void OnClosing(DataEventArgs e)
        {
            EventHandler<DataEventArgs> handler = Close;
            handler?.Invoke(this, e);
        }
        #endregion

        public ObservableCollectionExtended<DeploymentChecklistItem> DeploymentChecklistItems { get; } = new ObservableCollectionExtended<DeploymentChecklistItem>();

        public class DeploymentChecklistItem : INotifyPropertyChanged
        {
            public string ItemText { get; set; }
            public SolidColorBrush Foreground { get; set; }
            public FontAwesomeIcon Icon { get; set; }
            public bool Spinning { get; set; }

            public Action<DeploymentChecklistItem> ValidationFunction;
            public Mod ModToValidateAgainst;
            internal string ErrorsMessage;
            internal string ErrorsTitle;

            public event PropertyChangedEventHandler PropertyChanged;

            public string ToolTip { get; set; }
            public bool HasError { get; internal set; }
            public List<string> Errors { get; internal set; }

            //public DeploymentChecklistItem(string initialDisplayText, Mod m, Action<DeploymentChecklistItem> validationFunction)
            //{
            //    this.ItemText = initialDisplayText;
            //    this.ModToValidateAgainst = m;
            //    this.ValidationFunction = validationFunction;
            //    Icon = FontAwesomeIcon.Spinner;
            //    Spinning = true;
            //    Foreground = Brushes.Gray;
            //    ToolTip = "Validation in progress...";
            //}

            public DeploymentChecklistItem()
            {
                Icon = FontAwesomeIcon.Spinner;
                Spinning = true;
                Foreground = Brushes.Gray;
                ToolTip = "Validation in progress...";
            }

            public void ExecuteValidationFunction()
            {
                Foreground = Brushes.RoyalBlue;
                ValidationFunction?.Invoke(this);
                Debug.WriteLine("Invoke finished");
                Spinning = false;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink hl && hl.DataContext is DeploymentChecklistItem dcli)
            {
                ListDialog ld = new ListDialog(dcli.Errors, dcli.ErrorsTitle, dcli.ErrorsMessage, Window.GetWindow(hl));
                ld.ShowDialog();
            }
            Debug.WriteLine("Request navigate.");
        }
    }
}
