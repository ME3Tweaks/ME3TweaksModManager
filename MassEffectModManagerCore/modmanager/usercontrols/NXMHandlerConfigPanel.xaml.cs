using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for LogUploader.xaml
    /// </summary>
    public partial class NXMHandlerConfigPanel : MMBusyPanelBase
    {
        public NXMHandlerConfigPanel()
        {
            DataContext = this;
            LoadCommands();
        }

        /// <summary>
        /// If the values should serialize on close or be discarded
        /// </summary>
        private bool SaveOnClose;
        public ICommand AddOtherAppCommand { get; set; }
        public ICommand CloseCommand { get; set; }
        public ICommand RegisterCommand { get; set; }
        public ICommand RemoveAppCommand { get; set; }
        public ICommand CancelCommand { get; set; }

        private void LoadCommands()
        {
            RemoveAppCommand = new RelayCommand(RemoveApp);
            RegisterCommand = new GenericCommand(RegisterM3);
            AddOtherAppCommand = new GenericCommand(AddNXMApp, CanAddNXMApp);
            CloseCommand = new GenericCommand(() =>
            {
                SaveOnClose = true;
                OnClosing(DataEventArgs.Empty);
            }, CanClose);
            CancelCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty));
        }

        private void RegisterM3()
        {
            NexusModsUtilities.SetupNXMHandling();
            M3L.ShowDialog(window, M3L.GetString(M3L.string_dialog_setM3AsNXMHandler), M3L.GetString(M3L.string_configuredNxmHandling), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveApp(object obj)
        {
            if (obj is NexusDomainHandler ndh)
            {
                OtherGameHandlers.Remove(ndh);
            }
        }

        private bool CanClose()
        {
            if (ValidateAll())
            {
                return true;
            }

            return false;
        }

        private void AddNXMApp()
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = $@"{M3L.GetString(M3L.string_executables)}|*.exe",
                CheckFileExists = true,
            };
            var result = ofd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                OtherGameHandlers.Add(new NexusDomainHandler() { ProgramPath = ofd.FileName, Arguments = GetDefaultArgumentsForApp(Path.GetFileNameWithoutExtension(ofd.FileName)) });
            }
        }

        private string GetDefaultArgumentsForApp(string appExeName)
        {
            switch (appExeName.ToLower())
            {
                case @"vortex":
                    return @"-d %1";
                case @"kortex x64":
                case @"kortex":
                    return "-DownloadLink \"%1\""; // do not localize
                case @"nexusclient":
                default:
                    return @"%1";
            }
        }

        private bool CanAddNXMApp()
        {
            return ValidateAll();
        }

        private bool ValidateAll()
        {
            bool hasWildcard = false;
            foreach (var d in OtherGameHandlers)
            {
                if (!d.Validate())
                {
                    TriggerResize();
                    return false;
                }

                // Check only one wildcard
                // We have to use editable as the Domains object is not committed
                if (d.DomainsEditable.Trim() == @"*")
                {
                    if (hasWildcard)
                    {
                        TriggerResize();
                        d.ValidationMessage = M3L.GetString(M3L.string_cannotHaveMultipleEntriesThatUseWildcard);
                        return false;
                    }

                    hasWildcard = true;
                }
            }

            TriggerResize();
            return true;
        }


        public ObservableCollectionExtended<NexusDomainHandler> OtherGameHandlers { get; } = new();

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                // Do not save on close.
                SaveOnClose = false;
                OnClosing(DataEventArgs.Empty);
            }
        }

        protected override void OnClosing(DataEventArgs args)
        {
            if (SaveOnClose && ValidateAll())
            {
                // Save
                SaveAndCommit();
            }
            base.OnClosing(args);
        }

        private void SaveAndCommit()
        {
            foreach (var v in OtherGameHandlers)
            {
                v.Domains = v.DomainsEditable.Split(',').Select(x => x.Trim()).ToList();
            }
            App.NexusDomainHandlers.ReplaceAll(OtherGameHandlers);
            File.WriteAllText(M3Filesystem.GetExternalNexusHandlersFile(), JsonConvert.SerializeObject(OtherGameHandlers));
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();
            OtherGameHandlers.AddRange(App.NexusDomainHandlers);
            foreach (var handler in OtherGameHandlers)
            {
                handler.LoadEditable();
            }
        }
    }
}
