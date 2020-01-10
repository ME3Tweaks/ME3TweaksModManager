using ByteSizeLib;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
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
using System.Windows.Media.Animation;
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

        public void OnOperationInProgressChanged()
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                Storyboard sb = this.FindResource(OperationInProgress ? @"OpenBusyControl" : @"CloseBusyControl") as Storyboard;
                if (sb.IsSealed)
                {
                    sb = sb.Clone();
                }
                Storyboard.SetTarget(sb, LoadingBarAnimation);
                sb.Begin();
            });
        }
        public bool SettingsExpanded { get; set; }
        public bool ChangelogNotYetSet { get; set; } = true;
        public ICommand CloseCommand { get; set; }
        public ICommand CancelCommand { get; set; }
        public ICommand SetChangelogCommand { get; set; }
        public ICommand SaveSettingsCommand { get; set; }
        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(ClosePanel, CanClosePanel);
            CancelCommand = new GenericCommand(CancelOperation, CanCancelOperation);
            SetChangelogCommand = new GenericCommand(SetChangelog, () => !string.IsNullOrWhiteSpace(ChangelogText) && ChangelogNotYetSet);
            SaveSettingsCommand = new GenericCommand(SaveSettings, () => !OperationInProgress);
        }

        private void CancelOperation()
        {
            CancelOperations = true;
        }

        private bool CanCancelOperation()
        {
            return !CancelOperations && OperationInProgress;
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
                        StartPreparingModWrapper();
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

        private void HideChangelogArea()
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                Storyboard sb = this.FindResource(@"CloseBusyControl") as Storyboard;
                if (sb.IsSealed)
                {
                    sb = sb.Clone();
                }
                ChangelogPanel.Height = ChangelogPanel.ActualHeight; //required to unset NaN
                Storyboard.SetTarget(sb, ChangelogPanel);
                sb.Begin();
            });
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

            if (string.IsNullOrWhiteSpace(mod.UpdaterServiceServerFolder))
            {
                HideChangelogArea();
                CurrentActionText = "Mod must have the serverfolder descriptor set under UPDATES header";
                return;
            }

            if (SettingsExpanded)
            {
                CurrentActionText = "Enter your ME3Tweaks Updater Service information";
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
                            StartPreparingModWrapper();
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

        /// <summary>
        /// Starts preparing a mod by launching a background thread. This should be called from a UI thread
        /// </summary>
        private void StartPreparingModWrapper()
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
                CurrentActionText = "Checking if updater service is configured for mod";
                string validationUrl = $@"{UpdaterServiceCodeValidationEndpoint}?updatecode={mod.ModClassicUpdateCode}&updatexmlname={mod.UpdaterServiceServerFolderShortname}.xml";
                string isBeingServed = wc.DownloadStringAwareOfEncoding(validationUrl);
                if (string.IsNullOrWhiteSpace(isBeingServed) || isBeingServed != "true") //we don't parse for bool because it might have a different text that is not specifically true or false. It might
                                                                                         // have an error for example
                {
                    //Not being served
                    Log.Error(@"This mod is not configured for use on the Updater Service. Please contact Mgamerz.");
                    CurrentActionText = "Server not configured for mod - contact Mgamerz";
                    HideChangelogArea();
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(@"Error validating mod is configured on updater service: " + ex.Message);
                CurrentActionText = $"Error checking updater service configuration:\n{ex.Message}";
                HideChangelogArea();
                return;
            }

            #endregion

            #region get current production version to see if we should prompt user
            var latestVersionOnServer = OnlineContent.GetLatestVersionOfModOnUpdaterService(mod.ModClassicUpdateCode);
            if (latestVersionOnServer != null)
            {
                if (latestVersionOnServer >= mod.ParsedModVersion)
                {
                    bool cancel = false;
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        //server is newer or same as version we are pushing
                        var response = M3L.ShowDialog(mainwindow, $"The server version is the same or higher version of the mod you are already uploading. Are you sure you want to push this version to the ME3Tweaks Updater Service? Clients who have a newer or same version of the mod will not see an update.\n\nLocal version: {mod.ParsedModVersion}\nServer version: {latestVersionOnServer}", "Server version same or newer than local", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (response == MessageBoxResult.No)
                        {
                            CurrentActionText = "Upload aborted - mod on server is same or newer than local one being uploaded";
                            HideChangelogArea();
                            cancel = true;
                            return;
                        }
                    });
                    if (cancel)
                    {
                        return;
                    }
                }
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
            if (CancelOperations) { AbortUpload(); return; }
            #region hash mod and build server manifest
            CurrentActionText = "Building server manifest";

            long amountHashed = 0;
            ConcurrentDictionary<string, SourceFile> manifestFiles = new ConcurrentDictionary<string, SourceFile>();
            Parallel.ForEach(files, new ParallelOptions() { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, x =>
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
            if (CancelOperations) { AbortUpload(); return; }

            //Build document
            XmlDocument xmlDoc = new XmlDocument();
            XmlNode rootNode = xmlDoc.CreateElement("mod");
            xmlDoc.AppendChild(rootNode);

            foreach (var mf in manifestFiles)
            {
                if (CancelOperations) { AbortUpload(); return; }

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
            if (CancelOperations) { AbortUpload(); return; }

            foreach (var bf in mod.UpdaterServiceBlacklistedFiles)
            {
                if (CancelOperations) { AbortUpload(); return; }

                var bfn = xmlDoc.CreateElement("blacklistedfile");
                bfn.InnerText = bf;
                rootNode.AppendChild(bfn);
            }

            if (CancelOperations) { AbortUpload(); return; }
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
                if (CancelOperations) { AbortUpload(); return; }

                CurrentActionText = "Waiting for changelog to be set";
                Thread.Sleep(250);  //wait for changelog to be set.
            }

            #region Finish building manifest
            var changelog = xmlDoc.CreateAttribute("changelog");
            changelog.InnerText = ChangelogText;
            rootNode.Attributes.Append(changelog);

            using var stringWriter = new StringWriterWithEncoding(Encoding.UTF8);
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true; settings.IndentChars = " ";
            settings.Encoding = Encoding.UTF8;
            using var xmlTextWriter = XmlWriter.Create(stringWriter, settings);
            xmlDoc.WriteTo(xmlTextWriter);
            xmlTextWriter.Flush();


            #endregion

            var finalManifestText = stringWriter.GetStringBuilder().ToString();

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
            var serverFolderName = mod.UpdaterServiceServerFolderShortname;

            //sftp.ChangeDirectory(LZMAStoragePath);

            //Log.Information(@"Listing files/folders for " + LZMAStoragePath);
            //var lzmaStorageDirectoryItems = sftp.ListDirectory(LZMAStoragePath);
            var serverModPath = LZMAStoragePath + @"/" + serverFolderName;
            bool justMadeFolder = false;
            if (!sftp.Exists(serverModPath))
            {
                CurrentActionText = "Creating server folder for mod";
                Log.Information(@"Creating server folder for mod: " + serverModPath);
                sftp.CreateDirectory(serverModPath);
                justMadeFolder = true;
            }

            Dictionary<string, string> serverHashes = new Dictionary<string, string>();
            if (!justMadeFolder)
            {
                CurrentActionText = "Hashing files on server for delta";

                Log.Information("Connecting to ME3Tweaks Updater Service over SSH (SSH Shell)");
                using SshClient sshClient = new SshClient(host, username, password);
                sshClient.Connect();
                Log.Information("Connected to ME3Tweaks Updater Service over SSH (SSH Shell)");

                var command = sshClient.CreateCommand(@"find " + serverModPath + @" -type f -exec md5sum '{}' \;");
                command.CommandTimeout = TimeSpan.FromMinutes(1);
                command.Execute();
                var answer = command.Result;
                if (CancelOperations) { AbortUpload(); return; }

                foreach (var hashpair in answer.Split("\n"))
                {
                    if (string.IsNullOrWhiteSpace(hashpair)) continue; //last line will be blank
                    string md5 = hashpair.Substring(0, 32);
                    string path = hashpair.Substring(34);

                    path = path.Substring(LZMAStoragePath.Length + 2 + serverFolderName.Length); //+ 2 for slashes
                    serverHashes[path] = md5;
                    Debug.WriteLine(md5 + " for file " + path);
                }
            }

            //Calculate what needs to be updated or removed from server
            List<string> filesToUploadToServer = new List<string>();
            List<string> filesToDeleteOffServer = new List<string>();

            //Files to upload
            foreach (var sourceFile in manifestFiles)
            {
                //find matching server file
                if (serverHashes.TryGetValue(sourceFile.Key.Replace('\\', '/') + @".lzma", out var matchingHash))
                {
                    //exists on server, compare hash
                    if (matchingHash != sourceFile.Value.lzmahash)
                    {
                        //server hash is different! Upload new file.
                        Log.Information(@"Server version of file is different from local: " + sourceFile.Key);
                        filesToUploadToServer.Add(sourceFile.Key);
                    }
                    else
                    {
                        Log.Information(@"Server version of file is same as local: " + sourceFile.Key);
                    }
                }
                else
                {
                    Log.Information(@"Server does not have file: " + sourceFile.Key);
                    filesToUploadToServer.Add(sourceFile.Key);
                }
            }

            //Files to remove from server
            foreach (var serverfile in serverHashes.Keys)
            {
                if (!manifestFiles.Any(x => (x.Key + @".lzma") == serverfile.Replace('/', '\\')))
                {
                    Log.Information("File exists on server but not locally: " + serverfile);
                    filesToDeleteOffServer.Add(serverfile);
                }
            }

            #endregion

            #region upload files
            //Create directories
            SortedSet<string> directoriesToCreate = new SortedSet<string>();
            foreach (var f in filesToUploadToServer)
            {
                string foldername = f;
                var lastIndex = foldername.LastIndexOf("\\");

                while (lastIndex > 0)
                {
                    foldername = foldername.Substring(0, lastIndex);
                    directoriesToCreate.Add(foldername.Replace('\\', '/'));
                    lastIndex = foldername.LastIndexOf("\\");
                }
            }


            //foreach (FileSystemInfo info in infos)
            //{
            //    if (info.Attributes.HasFlag(FileAttributes.Directory))
            //    {
            //        string subPath = remotePath + "/" + info.Name;
            //        if (!client.Exists(subPath))
            //        {
            //            client.CreateDirectory(subPath);
            //        }
            //        UploadDirectory(client, info.FullName, remotePath + "/" + info.Name, uploadCallback);
            //    }
            //    else
            //    {
            //        using (Stream fileStream = new FileStream(info.FullName, FileMode.Open))
            //        {
            //            Debug.WriteLine(
            //                "Uploading {0} ({1:N0} bytes)",
            //                info.FullName, ((FileInfo)info).Length);

            //            client.UploadFile(fileStream, remotePath + "/" + info.Name, uploadCallback);
            //        }
            //    }
            //}

            #endregion
            //UploadDirectory(sftp, lzmaStagingPath, serverModPath, (ucb) => Debug.WriteLine("UCB: " + ucb));
            var dirsToCreateOnServerSorted = directoriesToCreate.ToList();
            dirsToCreateOnServerSorted.Sort((a, b) => a.Length.CompareTo(b.Length)); //short to longest so we create top levels first!
            if (dirsToCreateOnServerSorted.Count > 0)
            {
                CurrentActionText = "Creating mod directories on server";
                foreach (var f in dirsToCreateOnServerSorted)
                {
                    var serverFolderStr = serverModPath + "/" + f;
                    if (!sftp.Exists(serverFolderStr))
                    {
                        Log.Information(@"Creating directory on server: " + serverFolderStr);
                        sftp.CreateDirectory(serverFolderStr);
                    }
                    else
                    {
                        Log.Information(@"Server folder already exists, skipping: " + serverFolderStr);
                    }
                }
            }

            //Upload files
            long amountUploaded = 0;
            long amountToUpload = filesToUploadToServer.Sum(x => new FileInfo(Path.Combine(lzmaStagingPath, x + @".lzma")).Length);
            foreach (var file in filesToUploadToServer)
            {
                var fullPath = Path.Combine(lzmaStagingPath, file + @".lzma");
                var serverFilePath = serverModPath + "/" + file.Replace("\\", "/") + @".lzma";
                Log.Information(@"Uploading file " + fullPath + " to " + serverFilePath);
                long amountUploadedBeforeChunk = amountUploaded;
                using (Stream fileStream = new FileStream(fullPath, FileMode.Open))
                {
                    sftp.UploadFile(fileStream, serverFilePath, true, (x) =>
                       {
                           if (CancelOperations) { CurrentActionText = "Aborting upload"; return; }
                           amountUploaded = amountUploadedBeforeChunk + (long)x;
                           CurrentActionText = $"Uploading files to server {ByteSize.FromBytes(amountUploaded).ToString("0.00")}/{ByteSize.FromBytes(amountToUpload).ToString("0.00")}";
                       });
                }
            }
            if (CancelOperations) { AbortUpload(); return; }
            //delete extra files
            int numdone = 0;
            foreach (var file in filesToDeleteOffServer)
            {
                CurrentActionText = $"Deleting obsolete mod files from server {numdone}/{filesToDeleteOffServer.Count}";
                var fullPath = $@"{LZMAStoragePath}/{ serverFolderName}/{ file}";
                Log.Information(@"Deleting unused file off server: " + fullPath);
                sftp.DeleteFile(fullPath);
                numdone++;
            }


            //Upload manifest
            using var manifestStream = finalManifestText.ToStream();
            var serverManifestPath = $@"{ManifestStoragePath}/{serverFolderName}.xml";
            Log.Information(@"Uploading manifest to server: " + serverManifestPath);
            sftp.UploadFile(manifestStream, serverManifestPath, true, (x) =>
            {
                CurrentActionText = $"Uploading update manifest to server {ByteSize.FromBytes(amountUploaded).ToString("0.00")}/{ByteSize.FromBytes(amountToUpload).ToString("0.00")}";
            });

            CurrentActionText = "Validating mod on server";

        }

        private void AbortUpload()
        {
            CurrentActionText = "Upload aborted";
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
