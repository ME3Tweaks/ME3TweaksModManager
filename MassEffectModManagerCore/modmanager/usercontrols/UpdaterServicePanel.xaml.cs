using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.modmanager.nexusmodsintegration;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.WindowsAPICodePack.Taskbar;
using Renci.SshNet;
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
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Xml;
using MassEffectModManagerCore.modmanager.objects.mod;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
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

        public UpdaterServicePanel()
        {
            DataContext = this;
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
            SettingsSubtext = M3L.GetString(M3L.string_validatingSettings);
            Analytics.TrackEvent(@"Saving/authenticating to updater service", new Dictionary<string, string>()
            {
                {@"Username", Username },
                {@"LZMAPath", LZMAStoragePath },
                {@"ManifestPath", ManifestStoragePath }
            });
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
                        //Settings.Save();

                        MemoryStream encryptedStream = new MemoryStream();
                        var entropy = NexusModsUtilities.EncryptStringToStream(Password_TextBox.Password, encryptedStream);
                        Settings.SaveUpdaterServiceEncryptedValues(Convert.ToBase64String(entropy), Convert.ToBase64String(encryptedStream.ToArray()));
                        SettingsExpanded = false;
                        SettingsSubtext = null;
                        if (mod != null)
                        {
                            StartPreparingModWrapper();
                        }
                        else
                        {
                            CurrentActionText = M3L.GetString(M3L.string_settingsSaved);
                        }
                    }
                }
                else if (result is string errorMessage)
                {
                    SettingsSubtext = errorMessage;
                }
                else
                {
                    SettingsSubtext = M3L.GetString(M3L.string_errorValidatingSettings);
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
            if (e.Key == Key.Escape && CanClosePanel())
            {
                e.Handled = true;
                OnClosing(DataEventArgs.Empty);
            }
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
                string.IsNullOrWhiteSpace(Password_TextBox.Password) ||
                mod == null;

            if (mod != null)
            {
                if (string.IsNullOrWhiteSpace(mod.UpdaterServiceServerFolder))
                {
                    HideChangelogArea();
                    CurrentActionText = M3L.GetString(M3L.string_modMissingUpdatesDescriptor);
                    return;
                }
            }

            if (SettingsExpanded)
            {
                CurrentActionText = M3L.GetString(M3L.string_enterYourME3TweaksUpdaterServiceInformation);
                SettingsSubtext = M3L.GetString(M3L.string_pressSaveToValidateSettings);
            }
            else if (mod != null)
            {
                LZMAStoragePath = LZMAStoragePath.Trim();
                ManifestStoragePath = ManifestStoragePath.Trim();
                Username = Username.Trim();
                CurrentActionText = M3L.GetString(M3L.string_authenticatingToME3Tweaks);
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
                            CurrentActionText = M3L.GetString(M3L.string_enterUpdaterServiceSettings);
                            SettingsSubtext = M3L.GetString(M3L.string_pressSaveToValidateSettings);
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
            nbw.WorkerReportsProgress = true;
            nbw.ProgressChanged += (a, b) =>
            {
                if (b.UserState is double d)
                {
                    TaskbarHelper.SetProgress(d);
                }
                else if (b.UserState is TaskbarProgressBarState tbs)
                {
                    TaskbarHelper.SetProgressState(tbs);
                }
            };
            nbw.DoWork += (a, b) =>
            {
                OperationInProgress = true;
                b.Result = UploadMod(d => nbw.ReportProgress(0, d), s => nbw.ReportProgress(0, s));
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                Analytics.TrackEvent(@"Uploaded mod to updater service", new Dictionary<string, string>()
                {
                    {@"Result", b.Result?.ToString() },
                    {@"Mod", mod.ModName +@" "+mod.ModVersionString }
                });
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
                        currentOp = M3L.GetString(M3L.string_checkingLZMAStorageDirectory);
                        sftp.ChangeDirectory(LZMAStoragePath.Trim());
                        currentOp = M3L.GetString(M3L.string_checkingManifestsStorageDirectory);
                        sftp.ChangeDirectory(ManifestStoragePath.Trim());
                        b.Result = true;
                    }
                    catch (Exception e)
                    {
                        Log.Information($@"Error logging in during operation '{currentOp}': " + e.Message);
                        b.Result = M3L.GetString(M3L.string_interp_errorValidatingSettingsXY, currentOp, e.Message);
                    }
                }
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error != null)
                {
                    Log.Error($@"Exception occurred in {nbw.Name} thread: {b.Error.Message}");
                }
                Log.Information(@"Auth checked");
                OperationInProgress = false;
                authCompletedCallback?.Invoke(b.Result);
            };
            OperationInProgress = true;
            nbw.RunWorkerAsync();


        }

        private enum UploadModResult
        {
            NO_RESULT,
            NOT_BEING_SERVED,
            ERROR_VALIDATING_MOD_IS_CONFIGURED,
            ABORTED_BY_USER_SAME_VERSION_UPLOADED,
            ABORTED_BY_USER,
            BAD_SERVER_HASHES_AFTER_VALIDATION,
            UPLOAD_OK,
            CANT_UPLOAD_NATIVE_COMPRESSED_PACKAGES,
            ERROR_UPLOADING_FILE
        }

        private UploadModResult UploadMod(Action<double> progressCallback = null, Action<TaskbarProgressBarState> setTaskbarProgressState = null)
        {
            #region online fetch

            //Fetch current production manifest for mod (it may not exist)
            setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Indeterminate);
            using var wc = new System.Net.WebClient();
            try
            {
                CurrentActionText = M3L.GetString(M3L.string_checkingIfUpdaterServiceIsConfiguredForMod);
                string validationUrl = $@"{UpdaterServiceCodeValidationEndpoint}?updatecode={mod.ModClassicUpdateCode}&updatexmlname={mod.UpdaterServiceServerFolderShortname}.xml";
                string isBeingServed = wc.DownloadStringAwareOfEncoding(validationUrl);
                if (string.IsNullOrWhiteSpace(isBeingServed) || isBeingServed != @"true") //we don't parse for bool because it might have a different text that is not specifically true or false. It might
                                                                                          // have an error for example
                {
                    //Not being served
                    Log.Error(@"This mod is not configured for serving on the Updater Service. Please contact Mgamerz.");
                    CurrentActionText = M3L.GetString(M3L.string_serverNotConfiguredForModContactMgamerz);
                    HideChangelogArea();
                    return UploadModResult.NOT_BEING_SERVED;
                }
            }
            catch (Exception ex)
            {
                Log.Error(@"Error validating mod is configured on updater service: " + ex.Message);
                CurrentActionText = M3L.GetString(M3L.string_interp_errorCheckingUpdaterServiceConfiguration, ex.Message);
                HideChangelogArea();
                return UploadModResult.ERROR_VALIDATING_MOD_IS_CONFIGURED;
            }

            #endregion

            #region get current production version to see if we should prompt user

            var latestVersionOnServer = OnlineContent.GetLatestVersionOfModOnUpdaterService(mod.ModClassicUpdateCode);
            if (latestVersionOnServer != null)
            {
                if (latestVersionOnServer >= mod.ParsedModVersion)
                {
                    bool cancel = false;
                    setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Paused);
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        // server is newer or same as version we are pushing
                        var response = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_interp_dialog_serverVersionSameOrNewerThanLocal, mod.ParsedModVersion, latestVersionOnServer), M3L.GetString(M3L.string_serverVersionSameOrNewerThanLocal), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (response == MessageBoxResult.No)
                        {
                            CurrentActionText = M3L.GetString(M3L.string_uploadAbortedModOnServerIsSameOrNewerThanLocalOneBeingUploaded);
                            HideChangelogArea();
                            cancel = true;
                            return;
                        }
                    });
                    setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Indeterminate);
                    if (cancel)
                    {
                        return UploadModResult.ABORTED_BY_USER_SAME_VERSION_UPLOADED;
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

            #region Check package files are not natively compressed
            {
                // In brackets to scope
                Log.Information(@"UpdaterServiceUpload: Checking for compressed packages");
                double numDone = 0;
                int totalFiles = files.Count;
                var compressedPackages = new List<string>();
                foreach (var f in files)
                {
                    CurrentActionText = M3L.GetString(M3L.string_interp_checkingPackagesBeforeUploadX, (numDone * 100 / totalFiles));
                    numDone++;
                    if (f.RepresentsPackageFilePath())
                    {
                        //var p = MEPackageHandler.OpenMEPackage(Path.Combine(mod.ModPath, f));
                        //p.Save(compress: true);
                        var quickP = MEPackageHandler.QuickOpenMEPackage(Path.Combine(mod.ModPath, f));
                        if (quickP.IsCompressed)
                        {
                            if (quickP.NumCompressedChunksAtLoad > 0)
                            {
                                Log.Error($@"Found compressed package: {quickP.FilePath}");
                            }
                            else
                            {
                                Log.Error($@"Found package with IsCompressed flag but no compressed chunks: {quickP.FilePath}");
                            }

                            compressedPackages.Add(f);

                        }
                    }
                }

                if (compressedPackages.Any())
                {
                    CurrentActionText = M3L.GetString(M3L.string_uploadAborted_foundCompressedPackage);
                    // Abort
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_dialog_uploadAborted_foundCompressedPackage, string.Join('\n', compressedPackages.OrderBy(x => x))),
                            M3L.GetString(M3L.string_cannotUploadMod), MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    CancelOperations = true;
                    return UploadModResult.CANT_UPLOAD_NATIVE_COMPRESSED_PACKAGES;
                }
            }

            #endregion

            #region compress and stage mod

            void updateCurrentTextCallback(string newText)
            {
                CurrentActionText = newText;
            }

            bool? canceledCheckCallback() => CancelOperations;
            CurrentActionText = M3L.GetString(M3L.string_compressingModForUpdaterService);
            progressCallback?.Invoke(0);
            setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Normal);
            var lzmaStagingPath = OnlineContent.StageModForUploadToUpdaterService(mod, files, totalModSizeUncompressed, canceledCheckCallback, updateCurrentTextCallback, progressCallback);

            #endregion

            if (CancelOperations)
            {
                AbortUpload();
                return UploadModResult.ABORTED_BY_USER;
            }

            setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Indeterminate);

            #region hash mod and build server manifest

            CurrentActionText = M3L.GetString(M3L.string_buildingServerManifest);

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
                CurrentActionText = M3L.GetString(M3L.string_buildingServerManifest) + $@" {Math.Round(done * 100.0 / totalModSizeUncompressed)}%";
            });
            if (CancelOperations)
            {
                AbortUpload();
                return UploadModResult.ABORTED_BY_USER;
            }

            //Build document
            XmlDocument xmlDoc = new XmlDocument();
            XmlNode rootNode = xmlDoc.CreateElement(@"mod");
            xmlDoc.AppendChild(rootNode);

            foreach (var mf in manifestFiles)
            {
                if (CancelOperations)
                {
                    AbortUpload();
                    return UploadModResult.ABORTED_BY_USER;
                }

                XmlNode sourceNode = xmlDoc.CreateElement(@"sourcefile");

                var size = xmlDoc.CreateAttribute(@"size");
                size.InnerText = mf.Value.size.ToString();

                var hash = xmlDoc.CreateAttribute(@"hash");
                hash.InnerText = mf.Value.hash;

                var lzmasize = xmlDoc.CreateAttribute(@"lzmasize");
                lzmasize.InnerText = mf.Value.lzmasize.ToString();

                var lzmahash = xmlDoc.CreateAttribute(@"lzmahash");
                lzmahash.InnerText = mf.Value.lzmahash;

                var timestamp = xmlDoc.CreateAttribute(@"timestamp");
                timestamp.InnerText = mf.Value.timestamp.ToString();

                sourceNode.InnerText = mf.Key;
                sourceNode.Attributes.Append(size);
                sourceNode.Attributes.Append(hash);
                sourceNode.Attributes.Append(lzmasize);
                sourceNode.Attributes.Append(lzmahash);
                sourceNode.Attributes.Append(timestamp);

                rootNode.AppendChild(sourceNode);
            }

            if (CancelOperations)
            {
                AbortUpload();
                return UploadModResult.ABORTED_BY_USER;
            }

            foreach (var bf in mod.UpdaterServiceBlacklistedFiles)
            {
                if (CancelOperations)
                {
                    AbortUpload();
                    return UploadModResult.ABORTED_BY_USER;
                }

                var bfn = xmlDoc.CreateElement(@"blacklistedfile");
                bfn.InnerText = bf;
                rootNode.AppendChild(bfn);
            }

            if (CancelOperations)
            {
                AbortUpload();
                return UploadModResult.ABORTED_BY_USER;
            }

            var updatecode = xmlDoc.CreateAttribute(@"updatecode");
            updatecode.InnerText = mod.ModClassicUpdateCode.ToString();
            rootNode.Attributes.Append(updatecode);

            var version = xmlDoc.CreateAttribute(@"version");
            version.InnerText = mod.ParsedModVersion.ToString();
            rootNode.Attributes.Append(version);

            var serverfolder = xmlDoc.CreateAttribute(@"folder");
            serverfolder.InnerText = mod.UpdaterServiceServerFolder;
            rootNode.Attributes.Append(serverfolder);


            setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Indeterminate);
            #endregion

            //wait to ensure changelog is set.

            while (ChangelogNotYetSet)
            {
                setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Paused);
                if (CancelOperations)
                {
                    AbortUpload();
                    return UploadModResult.ABORTED_BY_USER;
                }

                CurrentActionText = M3L.GetString(M3L.string_waitingForChangelogToBeSet);
                Thread.Sleep(250); //wait for changelog to be set.
            }

            setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Indeterminate);

            #region Finish building manifest

            var changelog = xmlDoc.CreateAttribute(@"changelog");
            changelog.InnerText = ChangelogText;
            rootNode.Attributes.Append(changelog);

            using var stringWriter = new StringWriterWithEncoding(Encoding.UTF8);
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = @" ";
            settings.Encoding = Encoding.UTF8;
            using var xmlTextWriter = XmlWriter.Create(stringWriter, settings);
            xmlDoc.WriteTo(xmlTextWriter);
            xmlTextWriter.Flush();


            #endregion

            var finalManifestText = stringWriter.GetStringBuilder().ToString();

            #region Connect to ME3Tweaks

            CurrentActionText = M3L.GetString(M3L.string_connectingToME3TweaksUpdaterService);
            Log.Information(@"Connecting to ME3Tweaks as " + Username);
            string host = @"ftp.me3tweaks.com";
            string username = Username;
            string password = Settings.DecryptUpdaterServicePassword();

            using SftpClient sftp = new SftpClient(host, username, password);
            sftp.Connect();

            Log.Information(@"Connected to ME3Tweaks over SSH (SFTP)");

            CurrentActionText = M3L.GetString(M3L.string_connectedToME3TweaksUpdaterService);
            var serverFolderName = mod.UpdaterServiceServerFolderShortname;

            //sftp.ChangeDirectory(LZMAStoragePath);

            //Log.Information(@"Listing files/folders for " + LZMAStoragePath);
            //var lzmaStorageDirectoryItems = sftp.ListDirectory(LZMAStoragePath);
            var serverModPath = LZMAStoragePath + @"/" + serverFolderName;
            bool justMadeFolder = false;
            if (!sftp.Exists(serverModPath))
            {
                CurrentActionText = M3L.GetString(M3L.string_creatingServerFolderForMod);
                Log.Information(@"Creating server folder for mod: " + serverModPath);
                sftp.CreateDirectory(serverModPath);
                justMadeFolder = true;
            }

            var dirContents = sftp.ListDirectory(serverModPath).ToList();
            Dictionary<string, string> serverHashes = new Dictionary<string, string>();

            //Open SSH connection as we will need to hash files out afterwards.
            Log.Information(@"Connecting to ME3Tweaks Updater Service over SSH (SSH Shell)");
            using SshClient sshClient = new SshClient(host, username, password);
            sshClient.Connect();
            Log.Information(@"Connected to ME3Tweaks Updater Service over SSH (SSH Shell)");

            if (!justMadeFolder && dirContents.Any(x => x.Name != @"." && x.Name != @".."))
            {
                CurrentActionText = M3L.GetString(M3L.string_hashingFilesOnServerForDelta);
                Log.Information(@"Hashing existing files on server to compare for delta");
                serverHashes = getServerHashes(sshClient, serverFolderName, serverModPath);
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
                    Log.Information(@"File exists on server but not locally: " + serverfile);
                    filesToDeleteOffServer.Add(serverfile);
                }
            }

            #endregion


            long amountUploaded = 0, amountToUpload = 1;
            //Confirm changes
            if (Enumerable.Any(filesToDeleteOffServer) || Enumerable.Any(filesToUploadToServer))
            {
                var text = M3L.GetString(M3L.string_interp_updaterServiceDeltaConfirmationHeader, mod.ModName);
                if (Enumerable.Any(filesToUploadToServer)) text += M3L.GetString(M3L.string_nnFilesToUploadToServern) + @" " + string.Join('\n' + @" - ", filesToUploadToServer); //weird stuff to deal with localizer
                if (Enumerable.Any(filesToDeleteOffServer)) text += M3L.GetString(M3L.string_nnFilesToDeleteOffServern) + @" " + string.Join('\n' + @" - ", filesToDeleteOffServer); //weird stuff to deal with localizer
                text += M3L.GetString(M3L.string_interp_updaterServiceDeltaConfirmationFooter);
                bool performUpload = false;
                Log.Information(@"Prompting user to accept server delta");
                setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Paused);
                Application.Current.Dispatcher.Invoke(() => { performUpload = M3L.ShowDialog(mainwindow, text, M3L.GetString(M3L.string_confirmChanges), MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK; });
                setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Indeterminate);

                if (performUpload)
                {
                    Log.Information(@"User has accepted the delta, applying delta to server");

                    #region upload files

                    //Create directories
                    SortedSet<string> directoriesToCreate = new SortedSet<string>();
                    foreach (var f in filesToUploadToServer)
                    {
                        string foldername = f;
                        var lastIndex = foldername.LastIndexOf(@"\");

                        while (lastIndex > 0)
                        {
                            foldername = foldername.Substring(0, lastIndex);
                            directoriesToCreate.Add(foldername.Replace('\\', '/'));
                            lastIndex = foldername.LastIndexOf(@"\");
                        }
                    }

                    #endregion

                    //UploadDirectory(sftp, lzmaStagingPath, serverModPath, (ucb) => Debug.WriteLine("UCB: " + ucb));
                    var dirsToCreateOnServerSorted = directoriesToCreate.ToList();
                    dirsToCreateOnServerSorted.Sort((a, b) => a.Length.CompareTo(b.Length)); //short to longest so we create top levels first!
                    int numFoldersToCreate = dirsToCreateOnServerSorted.Count();
                    int numDone = 0;
                    if (dirsToCreateOnServerSorted.Count > 0)
                    {
                        CurrentActionText = M3L.GetString(M3L.string_creatingModDirectoriesOnServer);
                        foreach (var f in dirsToCreateOnServerSorted)
                        {
                            var serverFolderStr = serverModPath + @"/" + f;
                            if (!sftp.Exists(serverFolderStr))
                            {
                                Log.Information(@"Creating directory on server: " + serverFolderStr);
                                sftp.CreateDirectory(serverFolderStr);
                            }
                            else
                            {
                                Log.Information(@"Server folder already exists, skipping: " + serverFolderStr);
                            }

                            numDone++;
                            CurrentActionText = M3L.GetString(M3L.string_creatingModDirectoriesOnServer) + @" " + Math.Round(numDone * 100.0 / numFoldersToCreate) + @"%";

                        }
                    }

                    //Upload files
                    progressCallback?.Invoke(0);
                    setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Normal);

                    amountToUpload = filesToUploadToServer.Sum(x => new FileInfo(Path.Combine(lzmaStagingPath, x + @".lzma")).Length);
                    foreach (var file in filesToUploadToServer)
                    {
                        if (CancelOperations)
                        {
                            AbortUpload();
                            return UploadModResult.ABORTED_BY_USER;
                        }

                        var fullPath = Path.Combine(lzmaStagingPath, file + @".lzma");
                        var serverFilePath = serverModPath + @"/" + file.Replace(@"\", @"/") + @".lzma";
                        Log.Information(@"Uploading file " + fullPath + @" to " + serverFilePath);
                        long amountUploadedBeforeChunk = amountUploaded;
                        using Stream fileStream = new FileStream(fullPath, FileMode.Open);
                        try
                        {
                            sftp.UploadFile(fileStream, serverFilePath, true, (x) =>
                            {
                                if (CancelOperations)
                                {
                                    CurrentActionText = M3L.GetString(M3L.string_abortingUpload);
                                    return;
                                }

                                amountUploaded = amountUploadedBeforeChunk + (long) x;
                                var uploadedHR = FileSize.FormatSize(amountUploaded);
                                var totalUploadHR = FileSize.FormatSize(amountToUpload);
                                if (amountToUpload > 0)
                                {
                                    progressCallback?.Invoke(amountUploaded * 1.0 / amountToUpload);
                                }

                                CurrentActionText = M3L.GetString(M3L.string_interp_uploadingFilesToServerXY, uploadedHR, totalUploadHR);
                            });
                        }
                        catch (Exception e)
                        {
                            Log.Error($@"Error uploading file {fullPath} to server: {e.Message}");
                            CurrentActionText = $"Upload failed: {e.Message}";
                            // Abort
                            Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                M3L.ShowDialog(mainwindow, $"Uploading {fullPath} to the ME3Tweaks Updater Service failed: {e.Message}. The mod may be partially uploaded, you will need to try again to ensure a successful upload, as the files are out of sync with the online manifest. If issues continue, please contact Mgamerz.",
                                    "Upload failed", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                            return UploadModResult.ERROR_UPLOADING_FILE;
                        }
                    }
                    setTaskbarProgressState?.Invoke(TaskbarProgressBarState.Indeterminate);

                    if (CancelOperations)
                    {
                        AbortUpload();
                        return UploadModResult.ABORTED_BY_USER;
                    }

                    //delete extra files
                    int numdone = 0;
                    foreach (var file in filesToDeleteOffServer)
                    {
                        CurrentActionText = M3L.GetString(M3L.string_interp_deletingObsoleteFiles, numdone, filesToDeleteOffServer.Count);
                        var fullPath = $@"{LZMAStoragePath}/{serverFolderName}/{file}";
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
                        var uploadedAmountHR = FileSize.FormatSize(amountUploaded);
                        var uploadAmountTotalHR = FileSize.FormatSize(amountToUpload);
                        CurrentActionText = M3L.GetString(M3L.string_uploadingUpdateManifestToServer) + $@"{uploadedAmountHR}/{uploadAmountTotalHR}";
                    });
                }
                else
                {
                    Log.Warning(@"User has declined uploading the delta. We will not change anything on the server.");
                    CancelOperations = true;
                    AbortUpload();
                    return UploadModResult.ABORTED_BY_USER;
                }

                CurrentActionText = M3L.GetString(M3L.string_validatingModOnServer);
                Log.Information(@"Verifying hashes on server for new files");
                var newServerhashes = getServerHashes(sshClient, serverFolderName, serverModPath);
                var badHashes = verifyHashes(manifestFiles, newServerhashes);
                if (Enumerable.Any(badHashes))
                {
                    CurrentActionText = M3L.GetString(M3L.string_someHashesOnServerAreIncorrectContactMgamerz);
                    return UploadModResult.BAD_SERVER_HASHES_AFTER_VALIDATION;
                }
                else
                {
                    CurrentActionText = M3L.GetString(M3L.string_modUploadedToUpdaterService);
                    return UploadModResult.UPLOAD_OK;
                }
            }

            return UploadModResult.ABORTED_BY_USER;
        }

        private void AbortUpload()
        {
            CurrentActionText = M3L.GetString(M3L.string_uploadAborted);
        }

        private Dictionary<string, string> getServerHashes(SshClient sshClient, string serverFolderName, string serverModPath)
        {
            Dictionary<string, string> serverHashes = new Dictionary<string, string>();
            string commandStr = @"find " + serverModPath + @" -type f -exec md5sum '{}' \;";
            Log.Information(@"Hash command: " + commandStr);
            var command = sshClient.CreateCommand(commandStr);
            command.CommandTimeout = TimeSpan.FromMinutes(1);
            command.Execute();
            var answer = command.Result;
            //Log.Information(@"Command result ================\n"+answer);
            //Log.Information(@"=====================");
            if (CancelOperations) { AbortUpload(); return serverHashes; }

            foreach (var hashpair in answer.Split("\n")) //do not localize
            {
                if (string.IsNullOrWhiteSpace(hashpair)) continue; //last line will be blank
                string md5 = hashpair.Substring(0, 32);
                string path = hashpair.Substring(34);

                path = path.Substring(LZMAStoragePath.Length + 2 + serverFolderName.Length); //+ 2 for slashes
                serverHashes[path] = md5;
                Log.Information(md5 + @" MD5 for server file " + path);
            }

            return serverHashes;
        }

        private List<string> verifyHashes(ConcurrentDictionary<string, SourceFile> manifestHashes, Dictionary<string, string> serverhashes)
        {
            List<string> badHashes = new List<string>();
            foreach (var hashpair in manifestHashes)
            {
                var file = hashpair.Key;
                var manifestMD5 = hashpair.Value.lzmahash;
                if (serverhashes.TryGetValue(file.Replace(@"\", @"/") + @".lzma", out var serverMD5))
                {
                    if (manifestMD5 != serverMD5)
                    {
                        Log.Error(@"ERROR ON SERVER HASH FOR FILE " + file);
                        badHashes.Add(file);
                    }
                    else
                    {
                        Log.Information(@"Server hash OK for " + file);
                    }
                }
                else
                {
                    Log.Information(@"Extra file on server that is not present in manifest " + file);
                }
            }
            return badHashes;
        }


        private void UploadDirectory(SftpClient client, string localPath, string remotePath, Action<ulong> uploadCallback)
        {
            Log.Information($@"Uploading directory {localPath} to {remotePath}");

            IEnumerable<FileSystemInfo> infos =
                new DirectoryInfo(localPath).EnumerateFileSystemInfos();
            foreach (FileSystemInfo info in infos)
            {
                if (info.Attributes.HasFlag(FileAttributes.Directory))
                {
                    string subPath = remotePath + @"/" + info.Name;
                    if (!client.Exists(subPath))
                    {
                        client.CreateDirectory(subPath);
                    }
                    UploadDirectory(client, info.FullName, remotePath + @"/" + info.Name, uploadCallback);
                }
                else
                {
                    using (Stream fileStream = new FileStream(info.FullName, FileMode.Open))
                    {
                        Debug.WriteLine(
                            @"Uploading {0} ({1:N0} bytes)",
                            info.FullName, ((FileInfo)info).Length);

                        client.UploadFile(fileStream, remotePath + @"/" + info.Name, uploadCallback);
                    }
                }
            }
        }

    }
}
