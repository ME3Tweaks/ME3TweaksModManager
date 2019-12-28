using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Xml;
using System.Xml.Linq;
using static MassEffectModManagerCore.modmanager.me3tweaks.OnlineContent;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for UpdaterServicePanel.xaml
    /// </summary>
    public partial class UpdaterServicePanel : MMBusyPanelBase
    {
        public Mod mod { get; }
        public string CurrentActionText { get; private set; }
        public string ChangelogText;
        public UpdaterServicePanel(Mod mod)
        {
            DataContext = this;
            this.mod = mod;
            LoadCommands();
            InitializeComponent();
        }

        public bool OperationInProgress { get; set; }
        public bool ChangelogNotYetSet { get; set; }
        public ICommand CloseCommand { get; set; }
        public ICommand SetChangelogCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
            SetChangelogCommand = new GenericCommand(SetChangelog, () => ChangelogText != "" && ChangelogNotYetSet);
        }

        private void SetChangelog()
        {
            ChangelogNotYetSet = false; //set changelog as locked in.
        }

        private bool CanClosePanel() => !OperationInProgress;

        private void ClosePanel()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
        }

        public override void OnPanelVisible()
        {
            CheckAuth();
        }

        private void CheckAuth()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"UpdaterServiceAuthCheck");
            nbw.DoWork += (a, b) =>
            {

            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                OperationInProgress = false;
            };
            OperationInProgress = true;
            nbw.RunWorkerAsync();

            //NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"UpdaterServiceUpload");
            //nbw.DoWork += (a, b) =>
            //{
            //    StartPreparingMod();
            //};
            //nbw.RunWorkerCompleted += (a, b) =>
            //{
            //    OperationInProgress = false;
            //};
            ////OperationInProgress = true;
            ////nbw.RunWorkerAsync();      
        }

        private void StartPreparingMod()
        {
            #region online fetch
            //Fetch current production manifest for mod (it may not exist)
            using var wc = new System.Net.WebClient();
            try
            {
                CurrentActionText = "Fetching current production manifest";
                string updateUrl = UpdaterServiceManifestEndpoint + @"?classicupdatecode[]=" + mod.ModClassicUpdateCode;
                string updatexml = wc.DownloadStringAwareOfEncoding(updateUrl);

                XElement rootElement = XElement.Parse(updatexml);
                var modUpdateInfos = (from e in rootElement.Elements(@"mod")
                                      select new ModUpdateInfo
                                      {
                                          changelog = (string)e.Attribute(@"changelog"),
                                          versionstr = (string)e.Attribute(@"version"),
                                          updatecode = (int)e.Attribute(@"updatecode"),
                                          serverfolder = (string)e.Attribute(@"folder"),
                                          sourceFiles = (from f in e.Elements(@"sourcefile")
                                                         select new SourceFile
                                                         {
                                                             lzmahash = (string)f.Attribute(@"lzmahash"),
                                                             hash = (string)f.Attribute(@"hash"),
                                                             size = (int)f.Attribute(@"size"),
                                                             lzmasize = (int)f.Attribute(@"lzmasize"),
                                                             relativefilepath = f.Value,
                                                             timestamp = (long?)f.Attribute(@"timestamp") ?? (long)0
                                                         }).ToList(),
                                      }).ToList();
                Debug.WriteLine(@"Found info: " + modUpdateInfos.Count);

                CurrentActionText = "Fetching current server file listing";

            }
            catch (Exception ex)
            {
                Log.Error(@"Error fetching server manifest: " + ex.Message);
            }

            #endregion

            #region mod variables
            //get refs
            var files = mod.GetAllRelativeReferences(true);
            files = files.OrderByDescending(x => new FileInfo(Path.Combine(mod.ModPath, x)).Length).ToList();
            long totalModSizeUncompressed = files.Sum(x => new FileInfo(Path.Combine(mod.ModPath, x)).Length);
            #endregion

            #region compress and stage mod
            void updateCurrentTextCallback(string newText)
            {
                CurrentActionText = newText;
            }
            CurrentActionText = "Compressing mod for updater service";
            var lzmaStagingPath = OnlineContent.StageModForUploadToUpdaterService(mod, files, totalModSizeUncompressed, updateCurrentTextCallback);
            #endregion

            #region hash mod and build server manifest
            CurrentActionText = "Building server manifest";

            long amountHashed = 0;
            ConcurrentDictionary<string, SourceFile> manifestFiles = new ConcurrentDictionary<string, SourceFile>();
            Parallel.ForEach(files, new ParallelOptions() { MaxDegreeOfParallelism = 2 }, x =>
            {
                SourceFile sf = new SourceFile();
                var sFile = Path.Combine(mod.ModPath, x);
                var lFile = Path.Combine(lzmaStagingPath, x + @".lzma");
                var fileInfo = new FileInfo(sFile);
                sf.size = fileInfo.Length;
                sf.timestamp = fileInfo.LastWriteTimeUtc.Ticks;
                sf.relativefilepath = x;
                sf.hash = Utilities.CalculateMD5(sFile);
                sf.lzmahash = Utilities.CalculateMD5(lFile);
                sf.lzmasize = new FileInfo(lFile).Length;
                manifestFiles.TryAdd(x, sf);
                var done = Interlocked.Add(ref amountHashed, sf.size);
                CurrentActionText = $"Building server manifest {Math.Round(done * 100.0 / totalModSizeUncompressed)}%";
            });

            //Build document
            XmlDocument xmlDoc = new XmlDocument();
            XmlNode rootNode = xmlDoc.CreateElement("mod");
            xmlDoc.AppendChild(rootNode);

            foreach (var mf in manifestFiles)
            {
                XmlNode sourceNode = xmlDoc.CreateElement("sourcefile");

                var size = xmlDoc.CreateAttribute("size");
                size.InnerText = mf.Value.size.ToString();

                var hash = xmlDoc.CreateAttribute("hash");
                hash.InnerText = mf.Value.hash;

                var lzmasize = xmlDoc.CreateAttribute("lzmasize");
                lzmasize.InnerText = mf.Value.lzmasize.ToString();

                var lzmahash = xmlDoc.CreateAttribute("lzmahash");
                lzmahash.InnerText = mf.Value.lzmahash;

                var timestamp = xmlDoc.CreateAttribute("timestamp");
                timestamp.InnerText = mf.Value.timestamp.ToString();

                sourceNode.InnerText = mf.Key;
                sourceNode.Attributes.Append(size);
                sourceNode.Attributes.Append(hash);
                sourceNode.Attributes.Append(lzmasize);
                sourceNode.Attributes.Append(lzmahash);
                sourceNode.Attributes.Append(timestamp);

                rootNode.AppendChild(sourceNode);
            }

            foreach (var bf in mod.UpdaterServiceBlacklistedFiles)
            {
                var bfn = xmlDoc.CreateElement("blacklistedfile");
                bfn.InnerText = bf;
                rootNode.AppendChild(bfn);
            }

            var updatecode = xmlDoc.CreateAttribute("updatecode");
            updatecode.InnerText = mod.ModClassicUpdateCode.ToString();
            rootNode.Attributes.Append(updatecode);

            var version = xmlDoc.CreateAttribute("version");
            version.InnerText = mod.ParsedModVersion.ToString();
            rootNode.Attributes.Append(version);

            var serverfolder = xmlDoc.CreateAttribute("folder");
            serverfolder.InnerText = mod.UpdaterServiceServerFolder;
            rootNode.Attributes.Append(serverfolder);



            #endregion

            //wait to ensure changelog is set.

            while (ChangelogNotYetSet)
            {
                Thread.Sleep(250);  //wait for changelog to be set.
            }

            var changelog = xmlDoc.CreateAttribute("changelog");
            changelog.InnerText = ChangelogText;
            rootNode.Attributes.Append(changelog);
        }
    }
}
