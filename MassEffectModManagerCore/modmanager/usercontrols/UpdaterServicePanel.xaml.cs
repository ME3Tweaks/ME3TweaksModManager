using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.nexusmodsintegration;
using MassEffectModManagerCore.ui;
using Renci.SshNet;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        private bool CancelOperations;

        public Mod mod { get; }
        public string CurrentActionText { get; private set; }
        public string Username { get; set; }
        public string ChangelogText { get; set; }
        public string ManifestStoragePath { get; set; }
        public string LZMAStoragePath { get; set; }
        public string SettingsSubtext { get; set; }
        public UpdaterServicePanel(Mod mod)
        {
            DataContext = this;
            this.mod = mod;
            LoadCommands();
            InitializeComponent();
        }

        public bool OperationInProgress { get; set; }
        public bool SettingsExpanded { get; set; }
        public bool ChangelogNotYetSet { get; set; } = true;
        public ICommand CloseCommand { get; set; }
        public ICommand SetChangelogCommand { get; set; }
        public ICommand SaveSettingsCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
            SetChangelogCommand = new GenericCommand(SetChangelog, () => !string.IsNullOrWhiteSpace(ChangelogText) && ChangelogNotYetSet);
            SaveSettingsCommand = new GenericCommand(SaveSettings, () => !OperationInProgress);
        }

        private void SaveSettings()
        {
            //Check Auth
            OperationInProgress = true;
            SettingsSubtext = "Validating settings";

            CheckAuth(authCompletedCallback: (result) =>
            {
                if (result is bool?)
                {
                    bool? authenticated = (bool?)result; //can't cast with is for some reason
                    if (authenticated.HasValue && authenticated.Value)
                    {
                        Settings.UpdaterServiceUsername = Username;
                        Settings.UpdaterServiceLZMAStoragePath = LZMAStoragePath;
                        Settings.UpdaterServiceManifestStoragePath = ManifestStoragePath;
                        Settings.Save();

                        MemoryStream encryptedStream = new MemoryStream();
                        var entropy = NexusModsUtilities.EncryptStringToStream(Password_TextBox.Password, encryptedStream);
                        Settings.SaveUpdaterServiceEncryptedValues(Convert.ToBase64String(entropy), Convert.ToBase64String(encryptedStream.ToArray()));

                        SettingsExpanded = false;
                        SettingsSubtext = null;
                    }
                }
                else if (result is string errorMessage)
                {
                    SettingsSubtext = errorMessage;
                }
                else
                {
                    SettingsSubtext = "Error validating settings";
                }
                OperationInProgress = false;
            });

        }

        private void SetChangelog()
        {
            ChangelogNotYetSet = false; //set changelog as locked in.
        }

        private bool CanClosePanel() => !OperationInProgress;

        private void ClosePanel()
        {
            CancelOperations = true;
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
        }

        public override void OnPanelVisible()
        {
            Username = Settings.UpdaterServiceUsername;
            LZMAStoragePath = Settings.UpdaterServiceLZMAStoragePath;
            ManifestStoragePath = Settings.UpdaterServiceManifestStoragePath;
            Password_TextBox.Password = Settings.DecryptUpdaterServicePassword();
            var pw = Settings.DecryptUpdaterServicePassword();
            SettingsExpanded = string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(LZMAStoragePath) ||
                string.IsNullOrWhiteSpace(ManifestStoragePath) ||
                string.IsNullOrWhiteSpace(Password_TextBox.Password);
            if (SettingsExpanded)
            {
                CurrentActionText = "Enter Updater Service settings";
                SettingsSubtext = "Press save to validate settings";
            }
            else
            {
                CurrentActionText = "Authenticating to ME3Tweaks";
                CheckAuth(authCompletedCallback: (result) =>
                {
                    if (result is bool?)
                    {
                        var authed = (bool?)result;
                        if (authed.HasValue && authed.Value)
                        {
                            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"UpdaterServiceUpload");
                            nbw.DoWork += (a, b) =>
                            {
                                OperationInProgress = true;
                                StartPreparingMod();
                            };
                            nbw.RunWorkerCompleted += (a, b) =>
                            {
                                OperationInProgress = false;
                            };
                            nbw.RunWorkerAsync();
                        }
                    }
                    else
                    {
                        if (SettingsExpanded)
                        {
                            CurrentActionText = "Enter Updater Service settings";
                            SettingsSubtext = "Press save to validate settings";
                        }
                    }
                });
            }
        }

        private void CheckAuth(string pwOverride = null, Action<object> authCompletedCallback = null)
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"UpdaterServiceAuthCheck");
            nbw.DoWork += (a, b) =>
            {
                b.Result = false;
                string host = @"ftp.me3tweaks.com";
                string username = Username;
                string password = pwOverride ?? Password_TextBox.Password;
                if (string.IsNullOrWhiteSpace(password)) return;

                using (SftpClient sftp = new SftpClient(host, username, password))
                {
                    string currentOp = @"Connecting";
                    try
                    {
                        sftp.Connect();
                        currentOp = "Checking LZMA Storage Directory";
                        sftp.ChangeDirectory(LZMAStoragePath);
                        currentOp = "Checking Manifests Storage Directory";
                        sftp.ChangeDirectory(ManifestStoragePath);
                        b.Result = true;
                    }
                    catch (Exception e)
                    {
                        Log.Information($@"Error logging in during operation '{currentOp}': " + e.Message);
                        b.Result = "Error validating settings: " + currentOp + @": " + e.Message;
                    }
                }
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                Log.Information("Auth checked");
                OperationInProgress = false;
                authCompletedCallback?.Invoke(b.Result);
            };
            OperationInProgress = true;
            nbw.RunWorkerAsync();


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

            bool? canceledCheckCallback() => CancelOperations;
            CurrentActionText = "Compressing mod for updater service";
            var lzmaStagingPath = OnlineContent.StageModForUploadToUpdaterService(mod, files, totalModSizeUncompressed, canceledCheckCallback, updateCurrentTextCallback);
            #endregion
            if (CancelOperations) return;
            #region hash mod and build server manifest
            CurrentActionText = "Building server manifest";

            long amountHashed = 0;
            ConcurrentDictionary<string, SourceFile> manifestFiles = new ConcurrentDictionary<string, SourceFile>();
            Parallel.ForEach(files, new ParallelOptions() { MaxDegreeOfParallelism = 2 }, x =>
            {
                if (CancelOperations) return;
                SourceFile sf = new SourceFile();
                var sFile = Path.Combine(mod.ModPath, x);
                var lFile = Path.Combine(lzmaStagingPath, x + @".lzma");
                sf.hash = Utilities.CalculateMD5(sFile);
                sf.lzmahash = Utilities.CalculateMD5(lFile);
                var fileInfo = new FileInfo(sFile);
                sf.size = fileInfo.Length;
                sf.timestamp = fileInfo.LastWriteTimeUtc.Ticks;
                sf.relativefilepath = x;
                sf.lzmasize = new FileInfo(lFile).Length;
                manifestFiles.TryAdd(x, sf);
                var done = Interlocked.Add(ref amountHashed, sf.size);
                CurrentActionText = $"Building server manifest {Math.Round(done * 100.0 / totalModSizeUncompressed)}%";
            });
            if (CancelOperations) return;

            //Build document
            XmlDocument xmlDoc = new XmlDocument();
            XmlNode rootNode = xmlDoc.CreateElement("mod");
            xmlDoc.AppendChild(rootNode);

            foreach (var mf in manifestFiles)
            {
                if (CancelOperations) return;

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
            if (CancelOperations) return;

            foreach (var bf in mod.UpdaterServiceBlacklistedFiles)
            {
                if (CancelOperations) return;

                var bfn = xmlDoc.CreateElement("blacklistedfile");
                bfn.InnerText = bf;
                rootNode.AppendChild(bfn);
            }

            if (CancelOperations) return;
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
                CurrentActionText = "Waiting for changelog to be set";
                Thread.Sleep(250);  //wait for changelog to be set.
            }

            #region Finish building manifest
            var changelog = xmlDoc.CreateAttribute("changelog");
            changelog.InnerText = ChangelogText;
            rootNode.Attributes.Append(changelog);

            using var stringWriter = new StringWriter();
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true; settings.IndentChars = " ";
            settings.Encoding = Encoding.UTF8;
            using var xmlTextWriter = XmlWriter.Create(stringWriter, settings);
            xmlDoc.WriteTo(xmlTextWriter);
            xmlTextWriter.Flush();


            #endregion
            Debug.WriteLine(stringWriter.GetStringBuilder().ToString());

            #region Connect to ME3Tweaks
            CurrentActionText = "Connecting to ME3Tweaks Updater Service";
            Log.Information(@"Connecting to ME3Tweaks as " + Username);
            string host = @"ftp.me3tweaks.com";
            string username = Username;
            string password = Settings.DecryptUpdaterServicePassword();

            using SftpClient sftp = new SftpClient(host, username, password);
            sftp.Connect();

            Log.Information(@"Connected to ME3Tweaks over SSH (SFTP)");

            CurrentActionText = "Connected to ME3Tweaks Updater Service";
            var serverFolderName = mod.UpdaterServiceServerFolder;
            if (serverFolderName.Contains('/'))
            {
                serverFolderName = serverFolderName.Substring(serverFolderName.LastIndexOf('/') + 1);
            }

            //sftp.ChangeDirectory(LZMAStoragePath);

            //Log.Information(@"Listing files/folders for " + LZMAStoragePath);
            //var lzmaStorageDirectoryItems = sftp.ListDirectory(LZMAStoragePath);
            var serverModPath = LZMAStoragePath + @"/" + serverFolderName;
            if (!sftp.Exists(serverModPath))
            {
                CurrentActionText = "Creating server folder for mod";
                Log.Information(@"Creating server folder for mod: " + serverModPath);
                sftp.CreateDirectory(serverModPath);
            }

            CurrentActionText = "Hashing files on server for delta";

            Log.Information("Connecting to ME3Tweaks Updater Service over SSH (SSH Shell)");
            using SshClient sshClient = new SshClient(host, username, password);
            sshClient.Connect();

            var command = sshClient.CreateCommand(@"find " + serverModPath + @" -type f -exec md5sum '{}' \;");
            command.Execute();
            var answer = command.Result;
            Debug.WriteLine(answer);

            //UploadDirectory(sftp, lzmaStagingPath, serverModPath, (ucb) => Debug.WriteLine("UCB: " + ucb));

            CurrentActionText = "Done";

            #endregion
        }

        private void UploadDirectory(SftpClient client, string localPath, string remotePath, Action<ulong> uploadCallback)
        {
            Debug.WriteLine("Uploading directory {0} to {1}", localPath, remotePath);

            IEnumerable<FileSystemInfo> infos =
                new DirectoryInfo(localPath).EnumerateFileSystemInfos();
            foreach (FileSystemInfo info in infos)
            {
                if (info.Attributes.HasFlag(FileAttributes.Directory))
                {
                    string subPath = remotePath + "/" + info.Name;
                    if (!client.Exists(subPath))
                    {
                        client.CreateDirectory(subPath);
                    }
                    UploadDirectory(client, info.FullName, remotePath + "/" + info.Name, uploadCallback);
                }
                else
                {
                    using (Stream fileStream = new FileStream(info.FullName, FileMode.Open))
                    {
                        Debug.WriteLine(
                            "Uploading {0} ({1:N0} bytes)",
                            info.FullName, ((FileInfo)info).Length);

                        client.UploadFile(fileStream, remotePath + "/" + info.Name, uploadCallback);
                    }
                }
            }
        }

    }
}
