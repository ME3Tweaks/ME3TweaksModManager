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
using MassEffectModManagerCore.gamefileformats.unreal;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
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
            DataContext = this;
            this.mainWindow = mainWindow;
            ModBeingDeployed = mod;

            DeploymentChecklistItems.Add(new DeploymentChecklistItem("Verify mod version is correct: " + mod.ParsedModVersion, mod, ManualValidation));
            DeploymentChecklistItems.Add(new DeploymentChecklistItem("Verify URL is correct: " + mod.ModWebsite, mod, ManualValidation));
            DeploymentChecklistItems.Add(new DeploymentChecklistItem("Verify mod description accuracy", mod, ManualValidation));
            DeploymentChecklistItems.Add(new DeploymentChecklistItem("Compact TFCs with AFC Compactor to reduce mod size", mod, CheckModForAFCCompactability));
            DeploymentChecklistItems.Add(new DeploymentChecklistItem("Compact AFCs with TFC Compactor to reduce mod size", mod, CheckModForTFCCompactability));
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
            Debug.WriteLine("aFC check");

            Thread.Sleep(1000);
            item.Foreground = Brushes.Green;
            item.Icon = FontAwesomeIcon.CheckCircle;
            Debug.WriteLine("aFC done");
        }

        private void CheckModForTFCCompactability(DeploymentChecklistItem item)
        {
            if (ModBeingDeployed.Game == Mod.MEGame.ME3)
            {
                bool hasError = false;
                item.ItemText = "Checking textures in mod";
                var referencedFiles = ModBeingDeployed.GetAllRelativeReferences().Select(x => Path.Combine(ModBeingDeployed.ModPath, x)).ToList();
                int numChecked = 0;
                GameTarget validationTarget = mainWindow.InstallationTargets.FirstOrDefault(x => x.Game == ModBeingDeployed.Game);
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
                                    Debug.WriteLine(cache.Value);
                                    var mips = Texture2D.GetTexture2DMipInfos(texture, cache.Value);
                                    Texture2D tex = new Texture2D(texture);
                                    try
                                    {
                                        var imageBytes = tex.GetImageBytesForMip(tex.GetTopMip(), validationTarget, false); //use active target
                                        Debug.WriteLine("Texture OK: " + texture.GetInstancedFullPath);
                                    }
                                    catch (Exception e)
                                    {
                                        hasError = true;
                                        item.Icon = FontAwesomeIcon.Close;
                                        item.Foreground = Brushes.Red;
                                        item.Spinning = false;
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
                }

                Debug.WriteLine("TFC done");
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

            private Action<DeploymentChecklistItem> validationFunction;
            private Mod modToValidateAgainst;

            public event PropertyChangedEventHandler PropertyChanged;

            public string ToolTip { get; set; }

            public DeploymentChecklistItem(string initialDisplayText, Mod m, Action<DeploymentChecklistItem> validationFunction)
            {
                this.ItemText = initialDisplayText;
                this.modToValidateAgainst = m;
                this.validationFunction = validationFunction;
                Icon = FontAwesomeIcon.Spinner;
                Spinning = true;
                Foreground = Brushes.Gray;
                ToolTip = "Validation in progress...";
            }

            public void ExecuteValidationFunction()
            {
                Foreground = Brushes.Yellow;
                validationFunction?.Invoke(this);
                Debug.WriteLine("Invoke finished");
                Spinning = false;
            }
        }
    }
}
