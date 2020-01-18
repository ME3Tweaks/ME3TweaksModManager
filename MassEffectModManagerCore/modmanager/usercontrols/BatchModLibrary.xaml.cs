using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for BatchModLibrary.xaml
    /// </summary>
    public partial class BatchModLibrary : MMBusyPanelBase
    {
        public BatchLibraryInstallQueue SelectedBatchQueue { get; set; }
        public ObservableCollectionExtended<BatchLibraryInstallQueue> AvailableBatchQueues { get; } = new ObservableCollectionExtended<BatchLibraryInstallQueue>();
        public BatchModLibrary()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }
        public ICommand CloseCommand { get; private set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel);
        }

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void OnPanelVisible()
        {
            parseBatchFiles();
        }

        private void parseBatchFiles()
        {
            AvailableBatchQueues.ClearEx();
            var batchDir = Utilities.GetBatchInstallGroupsFolder();
            var files = Directory.GetFiles(batchDir);
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (extension == ".biq" || extension == ".txt")
                {
                    var queue = BatchLibraryInstallQueue.ParseInstallQueue(file, mainwindow.AllLoadedMods.ToList());
                    if (queue != null)
                    {
                        AvailableBatchQueues.Add(queue);
                    }
                }
            }
        }

        private void OnSelectedBatchQueueChanged()
        {

        }
    }

    public class BatchLibraryInstallQueue : INotifyPropertyChanged
    {
        public List<Mod> ModsToInstall = new List<Mod>();
        public List<string> ModsMissing = new List<string>();

        public Mod.MEGame Game { get; private set; }
        public string QueueName { get; private set; }
        public string QueueDescription { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void SaveQueue()
        {

        }
        public static BatchLibraryInstallQueue ParseInstallQueue(string queueFile, List<Mod> allLoadedMods)
        {
            if (!File.Exists(queueFile)) return null;
            BatchLibraryInstallQueue result = new BatchLibraryInstallQueue();
            string[] lines = File.ReadAllLines(queueFile);
            int line = 0;
            if (Path.GetExtension(queueFile) == ".biq")
            {
                //New Mod Manager 6 format
                if (Enum.TryParse<Mod.MEGame>(lines[line], out var game))
                {
                    result.Game = game;
                    line++;
                }
            }
            else
            {
                //Old Mod Manager 5 format. This code is only used for transition purposes
                result.Game = Mod.MEGame.ME3;
            }

            result.QueueName = lines[line];
            line++;
            result.QueueDescription = lines[line];

            while (line < lines.Length)
            {
                string moddescPath = lines[line];
                Mod m = allLoadedMods.FirstOrDefault(x => x.ModDescPath.Equals(moddescPath, StringComparison.InvariantCultureIgnoreCase));
                if (m != null)
                {
                    result.ModsToInstall.Add(m);
                }
                else
                {
                    result.ModsMissing.Add(moddescPath);
                }
                line++;
            }

            return result;
        }
    }
}