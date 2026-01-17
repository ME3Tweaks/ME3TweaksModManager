using AdonisUI;
using FontAwesome5;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Localization;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml;

namespace ME3TweaksModManager
{
    /// <summary>
    /// Partial class for MainWindow - Localization and language management
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Sets the UI language synchronously, typically before we have a way to schedule onto the UI thread (e.g. UI thread has not started)
        /// </summary>
        /// <param name="lang">Language code</param>
        /// <param name="startup">If this is being called during startup</param>
        /// <param name="forcedDictionary">Optional external resource dictionary to load</param>
        public void SetApplicationLanguage(string lang, bool startup, ResourceDictionary forcedDictionary = null)
        {
            M3Log.Information(@"Setting language to " + lang);
            M3Localization.InternalSetLanguage(lang, forcedDictionary, startup).Wait();
            RefreshMainUIStrings(lang, startup);
        }

        /// <summary>
        /// Sets the UI language on a background thread.
        /// </summary>
        /// <param name="lang">Language code</param>
        /// <param name="startup">If this is being called during startup</param>
        /// <param name="forcedDictionary">Optional external resource dictionary to load</param>
        public void SetApplicationLanguageAsync(string lang, bool startup, ResourceDictionary forcedDictionary = null)
        {
            Stopwatch sw = new Stopwatch();
            System.Threading.Tasks.Task.Run(() =>
            {
                sw.Start();
                M3Localization.InternalSetLanguage(lang, forcedDictionary, startup).Wait();
            }).ContinueWithOnUIThread(x =>
            {
                RefreshMainUIStrings(lang, startup);
            });
        }

        /// <summary>
        /// Triggers UI strings to rebind when a language change has occurred
        /// </summary>
        /// <param name="lang">Language code</param>
        /// <param name="startup">If this is being called during startup</param>
        private void RefreshMainUIStrings(string lang, bool startup)
        {
            App.CurrentLanguage = Settings.Language = lang;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoModSelectedRichText)));
            RefreshNexusStatus(true);
            SelectedLaunchOption?.OnLanguageChanged();
            try
            {
                var localizedHelpItems = DynamicHelpService.GetHelpItems(lang);
                setDynamicHelpMenu(localizedHelpItems);
            }
            catch (Exception e)
            {
                M3Log.Error(@"Could not set localized dynamic help: " + e.Message);
            }

            if (SelectedMod != null)
            {
                // This will force strings to update
                var sm = SelectedMod;
                SelectedMod = null;
                SelectedMod = sm;
            }

            if (!startup)
            {
                AuthToNexusMods(languageUpdateOnly: true).Wait();
                M3LoadedMods.Instance.FailedMods.RaiseBindableCountChanged();
                CurrentOperationText = M3L.GetString(M3L.string_setLanguageToX);
                VisitWebsiteText = (SelectedMod != null && SelectedMod.ModWebsite != Mod.DefaultWebsite) ? M3L.GetString(M3L.string_interp_visitSelectedModWebSite, SelectedMod.ModName) : "";
            }
        }

        /// <summary>
        /// Loads an external localization dictionary from a file
        /// </summary>
        /// <param name="filepath">Path to the localization XAML file</param>
        private void LoadExternalLocalizationDictionary(string filepath)
        {
            string filename = System.IO.Path.GetFileNameWithoutExtension(filepath);
            string extension = System.IO.Path.GetExtension(filepath);
            if (M3Localization.SupportedLanguages.Contains(filename) && extension == @".xaml" && Settings.DeveloperMode)
            {
                try
                {
                    var extDictionary = (ResourceDictionary)XamlReader.Load(new XmlTextReader(filepath));
                    SetApplicationLanguage(filename, false, extDictionary);
                }
                catch (Exception e)
                {
                    M3Log.Error(@"Error loading external localization file: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Refreshes the dynamic help menu with localized items
        /// </summary>
        /// <param name="sortableHelpItems">List of help items to display</param>
        private void setDynamicHelpMenu(IReadOnlyList<SortableHelpElement> sortableHelpItems)
        {
            var dynamicMenuItems = RecursiveBuildDynamicHelpMenuItems(sortableHelpItems);

            // Clear old items out
            for (int i = HelpMenuItem.Items.Count - 1; i > 0; i--)
            {
                if (HelpMenuItem.Items[i] is MenuItem menuItem && menuItem.Tag is string str && str == @"DynamicHelp")
                {
                    HelpMenuItem.Items.Remove(menuItem);
                }
            }

            dynamicMenuItems.Reverse();
            var dynamicHelpHeaderIndex = HelpMenuItem.Items.IndexOf(DynamicHelp_MenuItem) + 1;
            foreach (var v in dynamicMenuItems)
            {
                HelpMenuItem.Items.Insert(dynamicHelpHeaderIndex, v);
            }
        }

        /// <summary>
        /// Recursively builds menu items for the dynamic help menu
        /// </summary>
        /// <param name="sortableHelpItems">List of help items</param>
        /// <returns>List of menu items to display</returns>
        private List<MenuItem> RecursiveBuildDynamicHelpMenuItems(IReadOnlyList<SortableHelpElement> sortableHelpItems)
        {
            List<MenuItem> dynamicMenuItems = new List<MenuItem>();
            foreach (var item in sortableHelpItems)
            {
                MenuItem m = new MenuItem()
                {
                    Header = item.Title,
                    ToolTip = item.ToolTip,
                    Tag = @"DynamicHelp"
                };
                if (!string.IsNullOrEmpty(item.URL))
                {
                    m.Click += (o, eventArgs) => M3Utilities.OpenWebpage(item.URL);
                }
                else if (!string.IsNullOrEmpty(item.ModalTitle))
                {
                    item.ModalText = M3Utilities.ConvertBrToNewline(item.ModalText);
                    m.Click += (o, eventArgs) =>
                    {
                        new DynamicHelpItemModalWindow(item) { Owner = this }.ShowDialog();
                    };
                }

                if (!string.IsNullOrWhiteSpace(item.FontAwesomeIconResource) &&
                    Enum.TryParse<EFontAwesomeIcon>(item.FontAwesomeIconResource, out var icon))
                {
                    var ia = new ImageAwesome()
                    {
                        Icon = icon,
                        Height = 16,
                        Width = 16,
                        Style = (Style)FindResource(@"EnableDisableImageStyle")
                    };
                    ia.SetResourceReference(ImageAwesome.ForegroundProperty, AdonisUI.Brushes.ForegroundBrush);
                    m.Icon = ia;
                }

                dynamicMenuItems.Add(m);

                if (item.Children.Count > 0)
                {
                    var children = RecursiveBuildDynamicHelpMenuItems(item.Children);
                    foreach (var v in children)
                    {
                        m.Items.Add(v);
                    }
                }
            }

            return dynamicMenuItems;
        }

        /// <summary>
        /// Handles language selection from the menu
        /// </summary>
        private void ChangeLanguage_Clicked(object sender, System.Windows.RoutedEventArgs e)
        {
            string lang = @"int";
            if (sender == LanguageINT_MenuItem)
            {
                lang = @"int";
            }
            else if (sender == LanguagePOL_MenuItem)
            {
                lang = @"pol";
            }
            else if (sender == LanguageRUS_MenuItem)
            {
                lang = @"rus";
            }
            else if (sender == LanguageDEU_MenuItem)
            {
                lang = @"deu";
            }
            else if (sender == LanguageBRA_MenuItem)
            {
                lang = @"bra";
            }
            else if (sender == LanguageITA_MenuItem)
            {
                lang = @"ita";
            }
            SetApplicationLanguageAsync(lang, false);
        }

        /// <summary>
        /// Placeholder for setting tips for the current language
        /// </summary>
        internal void SetTipsForLanguage()
        {
            // Placeholder method
        }
    }
}
