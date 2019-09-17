using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ByteSizeLib;
using MassEffectModManager.gamefileformats;
using MassEffectModManager.modmanager.helpers;
using MassEffectModManager.modmanager.me3tweaks;
using MassEffectModManager.modmanager.objects;
using MassEffectModManager.ui;
using SevenZip;

namespace MassEffectModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for LogUploader.xaml
    /// </summary>
    public partial class LogUploader : UserControl, INotifyPropertyChanged
    {
        private readonly Action<ProgressBarUpdate> progressBarUpdateCallback;
        private bool UploadingLog;
        private const string LogUploaderEndpoint = "https://me3tweaks.com/modmanager/loguploader";
        public string TopText { get; private set; } = "Select a log to view on log viewing service";
        public ObservableCollectionExtended<LogItem> AvailableLogs { get; } = new ObservableCollectionExtended<LogItem>();
        public LogUploader(Action<ProgressBarUpdate> progressBarVisibilityCallback)
        {
            DataContext = this;
            this.progressBarUpdateCallback = progressBarVisibilityCallback;
            LoadCommands();
            InitializeComponent();
            InitLogUploaderUI();
        }


        private void InitLogUploaderUI()
        {
            AvailableLogs.ClearEx();
            var directory = new DirectoryInfo(App.LogDir);
            var logfiles = directory.GetFiles("modmanagerlog*.txt").OrderByDescending(f => f.LastWriteTime).ToList();
            AvailableLogs.AddRange(logfiles.Select(x => new LogItem(x.FullName)));
            if (LogSelector_ComboBox.Items.Count > 0)
            {
                LogSelector_ComboBox.SelectedIndex = 0;
            }
        }

        public event EventHandler Close;
        protected virtual void OnClosing(EventArgs e)
        {
            EventHandler handler = Close;
            handler?.Invoke(this, e);
        }
        public ICommand UploadLogCommand { get; set; }
        public ICommand CancelUploadCommand { get; set; }

        private void LoadCommands()
        {
            UploadLogCommand = new GenericCommand(StartLogUpload, CanUploadLog);
            CancelUploadCommand = new GenericCommand(CancelUpload, CanCancelUpload);
        }

        private void CancelUpload()
        {
            OnClosing(EventArgs.Empty);
        }

        private bool CanCancelUpload()
        {
            return !UploadingLog;
        }

        private void StartLogUpload()
        {
            UploadingLog = true;
            TopText = "Collecting log information";
            progressBarUpdateCallback?.Invoke(new ProgressBarUpdate(ProgressBarUpdate.UpdateTypes.SET_VISIBILITY, Visibility.Visible));
            NamedBackgroundWorker bw = new NamedBackgroundWorker("LogUpload");
            bw.DoWork += (a, b) =>
            {
                string logUploadText = LogCollector.CollectLogs();
                MemoryStream outStream = new MemoryStream();
                //I love libraries that have no documentation on how to use them
                var lzmaEncoder = new LzmaEncodeStream();
                var bytes = Encoding.UTF8.GetBytes(logUploadText);
                lzmaEncoder.Write(bytes, 0, bytes.Length);
                //This doesn't work. sevenziphelper doesn't seem to support compressing as LZMA
                //var bytes = System.Text.Encoding.UTF8.GetBytes(logUploadText);

                //var compressedBytes = SevenZipHelper.LZMA.Compress(bytes);
                //RIP
                File.WriteAllBytes(@"C:\users\public\test.lzma", lzmaEncoder.ReadStreamFully());
                Thread.Sleep(2000);
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                OnClosing(EventArgs.Empty);
            };
            bw.RunWorkerAsync();
        }

        private bool CanUploadLog()
        {
            return LogSelector_ComboBox.SelectedItem != null && !UploadingLog;
        }

        public class LogItem
        {
            public string filepath;
            public LogItem(string filepath)
            {
                this.filepath = filepath;
            }

            public override string ToString()
            {
                return System.IO.Path.GetFileName(filepath) + " - " + ByteSize.FromBytes(new FileInfo(filepath).Length);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
