using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Win32;
using Newtonsoft.Json;
using Octokit;
using Path = System.IO.Path;

namespace LocalizationHelper
{
    /// <summary>
    /// Main window for the ME3Tweaks Mod Manager localization editor.
    /// Provides functionality for translating, editing, and managing localizations for both
    /// ME3TweaksModManager and ME3TweaksCore projects across multiple languages.
    /// </summary>
    public partial class LocalizationTablesUI : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Controls the visibility of the loading overlay during initialization.
        /// </summary>
        public Visibility LoadingVisibility { get; set; } = Visibility.Visible;
        
        /// <summary>
        /// Array of fully supported language codes that have complete translations.
        /// </summary>
        private string[] FullySupportedLangs = { "deu", "rus", /*"pol", "bra",*/ "ita", /*"fra"*/ };

        /// <summary>
        /// List of all supported language codes in the application.
        /// </summary>
        public List<string> GlobalSupportedLanguages = new List<string>();

        /// <summary>
        /// Initializes a new instance of the LocalizationTablesUI window.
        /// Sets up the UI, loads available languages, and fetches localization data from GitHub.
        /// </summary>
        public LocalizationTablesUI()
        {
            Title = $"ME3Tweaks Mod Manager Localizer {Assembly.GetExecutingAssembly().GetName().Version}";

            GlobalSupportedLanguages.AddRange(FullySupportedLangs);
            LoadCommands();
            InitializeComponent();

            // Load official languages
            Languages.Add(new LocalizationLanguage() { Selected = false, LangCode = "deu", FullName = "German" });
            Languages.Add(new LocalizationLanguage() { Selected = false, LangCode = "rus", FullName = "Russian" });
            // Languages.Add(new LocalizationLanguage() { Selected = false, LangCode = "pol", FullName = "Polish" });
            Languages.Add(new LocalizationLanguage() { Selected = false, LangCode = "ita", FullName = "Italian" });
            // Languages.Add(new LocalizationLanguage() { Selected = false, LangCode = "fra", FullName = "French" });

            //Load M3 localizations
            LoadLocalizations(true, @"ME3TweaksModManager", @"MassEffectModManagerCore/modmanager/localizations/", M3LocalizationBranches, M3LocalizationCategories);
            LoadLocalizations(false, @"ME3TweaksCore", @"ME3TweaksCore/Localization/Dictionaries/", M3CLocalizationBranches, M3CLocalizationCategories);
        }

        /// <summary>
        /// Gets or sets the currently selected language for editing.
        /// </summary>
        public static LocalizationLanguage CurrentLanguage { get; set; }
        
        /// <summary>
        /// Collection of available languages for localization.
        /// </summary>
        public ObservableCollectionExtended<LocalizationLanguage> Languages { get; } = new();

        /// <summary>
        /// Auto-save timer callback that periodically saves the current localization work.
        /// Saves both ME3TweaksModManager and ME3TweaksCore localizations to the AppData folder.
        /// </summary>
        /// <param name="sender">The timer that triggered the event.</param>
        /// <param name="eventArgs">Event arguments.</param>
        public void AutoSave(object sender, EventArgs eventArgs)
        {
            try
            {
                string lang = CurrentLanguage?.LangCode;

                if (lang == null) return; // Do nothing

                // Save M3
                var sb = CreateXamlDocument(false);
                var locSavePath = Path.Combine(GetAppDataFolder(), $"m3-{lang}.xaml");
                File.WriteAllText(locSavePath, sb);

                // Save M3C
                sb = CreateXamlDocument(true);
                locSavePath = Path.Combine(GetAppDataFolder(), $"m3c-{lang}.xaml");
                File.WriteAllText(locSavePath, sb);

            }
            catch
            {
                // DO NOT CRASH
            }
        }

        /// <summary>
        /// Gets the application data folder where localization files are stored.
        /// </summary>
        /// <param name="createIfMissing">If true, the folder will be created if it doesn't exist.</param>
        /// <returns>The application data folder path.</returns>
        internal static string GetAppDataFolder(bool createIfMissing = true)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ME3TweaksModManagerLocalizer");
            if (createIfMissing && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return folder;
        }


        /// <summary>
        /// Gets or sets the loading message displayed to the user during initialization.
        /// </summary>
        public string PleaseWaitString { get; set; } = "Please wait, starting up";

        /// <summary>
        /// Collection of available localization branches for ME3TweaksModManager.
        /// </summary>
        public ObservableCollectionExtended<string> M3LocalizationBranches { get; } =
            new ObservableCollectionExtended<string>();

        /// <summary>
        /// Collection of available localization branches for ME3TweaksCore.
        /// </summary>
        public ObservableCollectionExtended<string> M3CLocalizationBranches { get; } =
            new ObservableCollectionExtended<string>();

        /// <summary>
        /// Collection of localized tip strings from the tips service.
        /// </summary>
        public ObservableCollectionExtended<LocalizedString> LocalizedTips { get; } =
            new ObservableCollectionExtended<LocalizedString>();

        /// <summary>
        /// Collection of localized tutorial strings from the tutorial service.
        /// </summary>
        public ObservableCollectionExtended<LocalizedString> LocalizedTutorialService { get; } =
            new ObservableCollectionExtended<LocalizedString>();

        /// <summary>
        /// Gets or sets the currently selected branch for ME3TweaksModManager localizations.
        /// </summary>
        public string M3SelectedBranch { get; set; }
        
        /// <summary>
        /// Gets or sets the currently selected branch for ME3TweaksCore localizations.
        /// </summary>
        public string M3CSelectedBranch { get; set; }

        /// <summary>
        /// Loads localization data from GitHub for a specific repository.
        /// </summary>
        /// <param name="fullLoad">If true, also loads tips, tutorials, and dynamic help content.</param>
        /// <param name="repo">The GitHub repository name (e.g., "ME3TweaksModManager").</param>
        /// <param name="branchLocalizationPath">The path within the repository where localization files are stored.</param>
        /// <param name="branchDest">Collection to populate with available branches.</param>
        /// <param name="categoryDest">Collection to populate with localization categories.</param>
        /// <param name="branch">Optional specific branch to load. If null, uses the first available branch.</param>
        private void LoadLocalizations(bool fullLoad, string repo, string branchLocalizationPath,
            ObservableCollectionExtended<string> branchDest,
            ObservableCollectionExtended<LocalizationCategory> categoryDest,
            string branch = null)
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (x, y) =>
            {
                if (!branchDest.Any())
                {
                    PleaseWaitString = $"Fetching remote localization branches for {repo}";
                    var ghclient = new GitHubClient(new ProductHeaderValue(@"ME3TweaksModManager"));
                    try
                    {
                        var branches = ghclient.Repository.Branch.GetAll("ME3Tweaks", repo).Result;
                        var locbranches = branches.Where(x => /*x.Name.Contains("master") ||*/
                            x.Name.Contains("-localization"));
                        System.Windows.Application.Current.Dispatcher.Invoke(delegate
                        {
                            branchDest.ReplaceAll(locbranches.Select(x => x.Name)
                                .OrderByDescending(x => x));
                        });
                    }
                    catch (Exception e)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(delegate
                        {
                            MessageBox.Show("Error getting list of localization branches: " + e.Message);
                        });
                        return;
                    }
                }

                string oldBuildBranch = null;
                if (branchDest.Any())
                {
                    if (branch == null)
                    {
                        branch = branchDest.First();

                        // Todo: Make generic somehow, maybe with a callback?
                        if (repo == @"ME3TweaksModManager")
                        {
                            M3SelectedBranch = branch;
                            m3oldBranch = branch;
                            if (M3LocalizationBranches.Count() > 1)
                            {
                                oldBuildBranch = M3LocalizationBranches[1];
                            }
                        }
                        else if (repo == @"ME3TweaksCore")
                        {
                            M3CSelectedBranch = branch;
                            m3coldBranch = branch;
                            if (M3CLocalizationBranches.Count() > 1)
                            {
                                oldBuildBranch = M3CLocalizationBranches[1];
                            }
                        }
                    }
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(delegate
                    {
                        MessageBox.Show(
                            $"Could not find any branches on {repo} repo containing name 'localization'");
                    });
                    return;
                }

                var dictionaries = new Dictionary<string, string>();
                string endpoint = $"https://raw.githubusercontent.com/ME3Tweaks/{repo}/{branch}/{branchLocalizationPath}"; //make dynamic, maybe with octokit.
                using var client = new WebClient();
                foreach (var lang in GlobalSupportedLanguages.Concat(new[] { "int" }))
                {
                    PleaseWaitString = $"Fetching {branch} {lang}";

                    var url = endpoint + lang + $".xaml?random={DateTime.Now.Ticks}";
                    try
                    {

                        var dict = client.DownloadStringAwareOfEncoding(url);
                        Debug.WriteLine(url);
                        dictionaries[lang] = dict;
                    }
                    catch (Exception e)
                    {
                        dictionaries[lang] = "";
                    }
                }

                if (oldBuildBranch != null)
                {
                    PleaseWaitString = $"Fetching {repo} {oldBuildBranch} int";

                    endpoint =
                        $"https://raw.githubusercontent.com/ME3Tweaks/{repo}/{oldBuildBranch}/{branchLocalizationPath}"; //make dynamic, maybe with octokit.
                    var url = endpoint + "int.xaml";
                    Debug.WriteLine(url);
                    var dict = client.DownloadStringAwareOfEncoding(url);
                    dictionaries["int-prev"] = dict;
                }

                PleaseWaitString = $"Parsing main strings";

                Dictionary<string, string> oldStuff = new Dictionary<string, string>();
                if (dictionaries.TryGetValue("int-prev", out var oldStrXml))
                {
                    XDocument oldBuildDoc = XDocument.Parse(oldStrXml);
                    XNamespace system = "clr-namespace:System;assembly=System.Runtime";
                    XNamespace xk = "http://schemas.microsoft.com/winfx/2006/xaml";
                    var lstrings = oldBuildDoc.Root.Descendants(system + "String").ToList();
                    foreach (var lstring in lstrings)
                    {
                        oldStuff[lstring.Attribute(xk + "Key").Value] = lstring.Value;
                    }
                }


                //Parse INT.
                LocalizationCategory cat = null;
                int numBlankLines = 0;
                List<LocalizationCategory> categories = new List<LocalizationCategory>();
                var intLines = Regex.Split(dictionaries["int"], "\r\n|\r|\n");
                for (int i = 3; i < intLines.Length - 2; i++)
                {
                    var line = intLines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        numBlankLines++;
                        continue;
                    }

                    // Debug.WriteLine(line);
                    if (line.StartsWith("<!--") && line.EndsWith("-->"))
                    {
                        //Comment - parse
                        line = line.Substring(4);
                        line = line.Substring(0, line.Length - 3);
                        line = line.Trim();
                        if (numBlankLines > 0 || cat == null)
                        {
                            //New category?
                            if (cat != null)
                            {
                                categories.Add(cat);
                            }

                            cat = new LocalizationCategory()
                            {
                                CategoryName = line
                            };
                        }

                        //notes for previous item?
                        var prevItem = cat.LocalizedStringsForSection.LastOrDefault();
                        if (prevItem != null)
                        {
                            prevItem.notes = line;
                        }
                        //Debug.WriteLine(line);

                        //New Category
                        //line = line.
                        continue;
                    }



                    numBlankLines = 0;
                    var lineInfo = extractInfo(line);
                    LocalizedString ls = new LocalizedString()
                    {
                        key = lineInfo.key,
                        preservewhitespace = lineInfo.preserveWhitespace,
                        EnglishString = lineInfo.text
                    };

                    // if (ls.key == "string_failedToInstallBinkASILoader") Debugger.Break();

                    if (oldStuff.TryGetValue(lineInfo.key, out var oldString))
                    {
                        // var oldValue = new XText(oldString).ToString();
                        // var newValue = new XText(lineInfo.text).ToString();
                        XDocument newV = XDocument.Parse("<text>" + lineInfo.text + "</text>");
                        if (oldString != newV.Root.Value)
                        {
                            //Debug.WriteLine("Changed: " + ls.key);
                            //Debug.WriteLine("  OLD: " + oldString);
                            //Debug.WriteLine("  NEW: " + lineInfo.text);
                            ls.ChangedFromPrevious = true;
                        }
                    }
                    else if (oldStuff.Any())
                    {
                        //Debug.WriteLine("New: " + ls.key);
                        ls.ChangedFromPrevious = true;
                    }

                    if (lineInfo.key == null) Debugger.Break();
                    if (ls.EnglishString == null) Debugger.Break();
                    cat.LocalizedStringsForSection.Add(ls);
                }

                if (cat != null)
                {
                    categories.Add(cat);
                }

                parseLocalizations(categories, dictionaries);
                y.Result = categories;

                if (fullLoad)
                {
                    //TIPS SERVICE
                    PleaseWaitString = $"Fetching Tips Service";

                    string tipsEndpoint = "https://me3tweaks.com/modmanager/services/tipsservice";
                    var wc = new System.Net.WebClient();
                    var tipsJson = wc.DownloadString(tipsEndpoint);
                    var jsonObj = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(tipsJson);
                    var locTips = new List<LocalizedString>();
                    for (int i = 0; i < jsonObj["int"].Count; i++)
                    {
                        LocalizedString ls = new LocalizedString()
                        {
                            EnglishString = jsonObj["int"][i]
                        };
                        foreach (var lang in GlobalSupportedLanguages)
                        {
                            if (jsonObj.TryGetValue(lang, out var parsed))
                            {
                                if (parsed.Count <= i) continue; //skip
                                ls.Localizations[lang] = parsed[i];
                            }
                        }

                        locTips.Add(ls);
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(
                        delegate { LocalizedTips.ReplaceAll(locTips); });

                    //DYNAMIC HELP
                    PleaseWaitString = $"Fetching Dynamic Help";

                    endpoint =
                        $"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/staticfiles/liveservices/staticfiles/v1/dynamichelp/dynamichelp.xml";
                    var dynamicHelpXml = wc.DownloadString(endpoint);
                    XDocument doc = XDocument.Parse(dynamicHelpXml);
                    var intxml = doc.XPathSelectElement("/localizations/helpmenu[@lang='int']");
                    dynamicHelpLocalizations["int"] = intxml.ToString();

                    //Debug.WriteLine(doc.ToString());
                    foreach (var lang in GlobalSupportedLanguages)
                    {
                        var langxml = doc.XPathSelectElement($"/localizations/helpmenu[@lang='{lang}']");
                        if (langxml != null)
                        {
                            dynamicHelpLocalizations[lang] = langxml.ToString();
                        }
                    }

                    var nonLocalizedelements = doc.XPathSelectElements($"/localizations/*[not(self::helpmenu)]");
                    foreach (var section in nonLocalizedelements)
                    {
                        nonLocalizedHelpSections.Add(section.ToString());
                    }



                    // TUTORIAL SERVICE
                    PleaseWaitString = $"Fetching Tutorial Service";

                    string tutorialEndpoint = "https://me3tweaks.com/modmanager/services/tutorialservice2";
                    wc.Dispose();
                    wc = new System.Net.WebClient();
                    var tutorialJson = wc.DownloadString(tutorialEndpoint);
                    var TSjsonObj = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(tutorialJson);
                    var locTutorial = new List<LocalizedString>();
                    for (int i = 0; i < TSjsonObj.Count; i++)
                    {
                        LocalizedString ls = new LocalizedString()
                        {
                            EnglishString = TSjsonObj[i]["lang_int"]
                        };
                        foreach (var lang in GlobalSupportedLanguages)
                        {
                            if (TSjsonObj[i].TryGetValue($"lang_{lang}", out var parsed))
                            {
                                ls.Localizations[lang] = parsed;
                            }
                        }

                        locTutorial.Add(ls);
                    }

                    PleaseWaitString = "Loading editor";

                    System.Windows.Application.Current.Dispatcher.Invoke(delegate
                    {
                        LocalizedTutorialService.ReplaceAll(locTutorial);
                        intViewer.Text = intxml.ToString();
                    });
                }
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null && b.Result is List<LocalizationCategory> categories)
                {
                    LoadingVisibility = Visibility.Collapsed;
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        categoryDest.ReplaceAll(categories.OrderBy(x => x.CategoryName));
                    });
                    PleaseWaitString = "";
                    autosaveTimer = new DispatcherTimer();
                    autosaveTimer.Tick += AutoSave;
                    autosaveTimer.Interval = new TimeSpan(0, 1, 0);
                    autosaveTimer.Start();
                }
            };
            bw.RunWorkerAsync();
        }


        /// <summary>
        /// Timer used for automatic saving of localization work in progress.
        /// </summary>
        public DispatcherTimer autosaveTimer;
        
        /// <summary>
        /// Stores the previously selected ME3TweaksModManager branch to detect changes.
        /// </summary>
        private string m3oldBranch = null;
        
        /// <summary>
        /// Stores the previously selected ME3TweaksCore branch to detect changes.
        /// </summary>
        private string m3coldBranch = null;
        
        /// <summary>
        /// Dictionary mapping language codes to their localized dynamic help XML content.
        /// </summary>
        private Dictionary<string, string> dynamicHelpLocalizations = new Dictionary<string, string>();
        
        /// <summary>
        /// List of non-localized help sections that should be preserved when saving dynamic help.
        /// </summary>
        private List<string> nonLocalizedHelpSections = new List<string>();
        
        /// <summary>
        /// Called when the ME3TweaksModManager selected branch changes.
        /// Reloads localizations for the new branch.
        /// </summary>
        public void OnM3SelectedBranchChanged()
        {
            if (m3oldBranch != null)
            {
                if (M3SelectedBranch != null)
                {
                    LoadLocalizations(false, @"ME3TweaksModManager", @"MassEffectModManagerCore/modmanager/localizations/", M3LocalizationBranches, M3LocalizationCategories, M3SelectedBranch);
                    m3oldBranch = M3SelectedBranch;
                }
                else
                {
                    M3LocalizationCategories.ClearEx();
                }
            }
        }

        /// <summary>
        /// Called when the ME3TweaksCore selected branch changes.
        /// Reloads localizations for the new branch.
        /// </summary>
        public void OnM3CSelectedBranchChanged()
        {
            if (m3coldBranch != null)
            {
                if (M3CSelectedBranch != null)
                {
                    LoadLocalizations(false, @"ME3TweaksCore", @"ME3TweaksCore/Localization/Dictionaries/", M3CLocalizationBranches, M3CLocalizationCategories, M3CSelectedBranch);
                    m3coldBranch = M3CSelectedBranch;
                }
                else
                {
                    M3CLocalizationCategories.ClearEx();
                }
            }
        }

        /// <summary>
        /// Parses localization XAML documents and applies translations to the category structure.
        /// </summary>
        /// <param name="categories">List of categories to apply localizations to.</param>
        /// <param name="langToXamlMap">Dictionary mapping language codes to XAML document text.</param>
        private void parseLocalizations(List<LocalizationCategory> categories, Dictionary<string, string> langToXamlMap)
        {
            foreach (var lang in langToXamlMap.Keys)
            {
                if (lang != "INT")
                {
                    // Clear all values first
                    foreach (var v in categories.SelectMany(x => x.LocalizedStringsForSection))
                    {
                        v.Localizations[lang] = null; // CLEAR
                    }
                }

                var langLines = Regex.Split(langToXamlMap[lang], "\r\n|\r|\n");
                int numBlankLines = 0;
                for (int i = 3; i < langLines.Length - 2; i++) // start at line 3 and skip forward
                {
                    var line = langLines[i].Trim();
                    if (line == "</ResourceDictionary>")
                    {
                        // Not something we care about.
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        numBlankLines++;
                        continue;
                    }

                    if (line.StartsWith("<!--") && line.EndsWith("-->"))
                    {
                        //Comment - parse
                        line = line.Substring(4);
                        line = line.Substring(0, line.Length - 3);
                        line = line.Trim();
                        if (numBlankLines > 0)
                        {
                            continue; //skip this line. Only INT determines categories
                        }

                        //We don't care in localizations about this, they just have to exist.
                        continue;
                    }

                    numBlankLines = 0;
                    var lineInfo = extractInfo(line);
                    var t = categories
                        .Select(x => x.LocalizedStringsForSection.FirstOrDefault(y => y.key == lineInfo.key))
                        .Where(x => x != null).ToList();
                    LocalizedString ls = t.FirstOrDefault();
                    if (ls != null)
                    {
                        ls.Localizations[lang] = lineInfo.text;
                    }
                }
            }
        }

        /// <summary>
        /// Extracts localization information from a single line of XAML.
        /// </summary>
        /// <param name="line">The XAML line to parse.</param>
        /// <returns>A tuple containing preserve whitespace flag, key, and text value.</returns>
        private (bool preserveWhitespace, string key, string text) extractInfo(string line)
        {
            var closingTagIndex = line.IndexOf(">");
            var strInfo = line.Substring(0, closingTagIndex).Trim();
            bool preserveWhitespace = strInfo.Contains("xml:space=\"preserve\"");
            int keyPos = strInfo.IndexOf("x:Key=\"");
            string keyVal = strInfo.Substring(keyPos + "x:Key=\"".Length);
            keyVal = keyVal.Substring(0, keyVal.IndexOf("\""));

            int startPos = line.IndexOf(">") + 1;
            string text = line.Substring(startPos);
            text = text.Substring(0, text.LastIndexOf("<"));

            return (preserveWhitespace, keyVal, text);
        }

        /// <summary>
        /// Gets or sets the currently selected category for ME3TweaksModManager.
        /// </summary>
        public LocalizationCategory M3SelectedCategory { get; set; }
        
        /// <summary>
        /// Gets or sets the currently selected category for ME3TweaksCore.
        /// </summary>
        public LocalizationCategory M3CSelectedCategory { get; set; }

        /// <summary>
        /// Collection of localization categories for ME3TweaksModManager.
        /// </summary>
        public ObservableCollectionExtended<LocalizationCategory> M3LocalizationCategories { get; } =
            new ObservableCollectionExtended<LocalizationCategory>();

        /// <summary>
        /// Collection of localization categories for ME3TweaksCore.
        /// </summary>
        public ObservableCollectionExtended<LocalizationCategory> M3CLocalizationCategories { get; } =
            new ObservableCollectionExtended<LocalizationCategory>();

        /// <summary>
        /// Command to save the current localization to a file.
        /// </summary>
        public ICommand SaveLocalizationCommand { get; set; }
        
        /// <summary>
        /// Command to copy the current localization to the clipboard.
        /// </summary>
        public ICommand CopyLocalizationCommand { get; set; }
        
        /// <summary>
        /// Command to load a localization from a file.
        /// </summary>
        public ICommand LoadLocalizationCommand { get; set; }
        
        /// <summary>
        /// Command to save tips localization to a file.
        /// </summary>
        public ICommand SaveTipsLocalizationCommand { get; set; }
        
        /// <summary>
        /// Command to load the localized help menu for the current language.
        /// </summary>
        public ICommand LoadLocalizedHelpMenuCommand { get; set; }
        
        /// <summary>
        /// Command to save the localized help menu.
        /// </summary>
        public ICommand SaveLocalizedHelpMenuCommand { get; set; }
        
        /// <summary>
        /// Command to save tutorial localization to a file.
        /// </summary>
        public ICommand SaveTutorialLocalizationCommand { get; set; }
        
        /// <summary>
        /// Command to open the auto-save directory in Windows Explorer.
        /// </summary>
        public ICommand OpenAutosaveDirCommand { get; set; }
        
        /// <summary>
        /// Command to add a new language to the localization editor.
        /// </summary>
        public ICommand AddLangCommand { get; set; }

        /// <summary>
        /// Command to check for missing localization strings.
        /// </summary>
        public ICommand CheckMissingStringsCommand { get; set; }

        /// <summary>
        /// Initializes all command bindings for the application.
        /// </summary>
        private void LoadCommands()
        {
            OpenAutosaveDirCommand = new GenericCommand(OpenAutosavesLocation);
            AddLangCommand = new GenericCommand(AddLanguage, CanAddLang);
            SaveLocalizationCommand = new RelayCommand(SaveLocalization, CanSaveLocalization);
            CopyLocalizationCommand = new RelayCommand(CopyLocalization, CanSaveLocalization);
            LoadLocalizationCommand = new RelayCommand(LoadLocalization, CanSaveLocalization);
            SaveTipsLocalizationCommand = new GenericCommand(SaveTipsLocalization, CanAddLang);
            SaveTutorialLocalizationCommand = new GenericCommand(SaveTutorialLocalization, CanAddLang);
            LoadLocalizedHelpMenuCommand = new GenericCommand(LoadLocalizedHelpMenu, CanAddLang);
            SaveLocalizedHelpMenuCommand = new GenericCommand(SaveLocalizedHelpMenu, CanAddLang);
            CheckMissingStringsCommand = new RelayCommand(CheckMissingStrings, CanSaveLocalization);
        }

        /// <summary>
        /// Prompts the user to add a new language and adds it to the available languages.
        /// </summary>
        private void AddLanguage()
        {
            var result = PromptDialog.Prompt(this, "Enter a 3 letter language code for your new language.",
                "Enter lang code")?.Replace(" ", "");
            if (result == null || result.Length != 3)
                return;
            LocalizationLanguage locLang = Languages.FirstOrDefault(x => x.LangCode == result);
            if (locLang == null)
            {
                locLang = new LocalizationLanguage() { Selected = false, FullName = result, LangCode = result };
                Languages.Add(locLang);
                foreach (var lang in Languages)
                {
                    lang.Selected = false;
                }
                locLang.Selected = true;
                ChangeLanguage(locLang);
            }
        }

        /// <summary>
        /// Determines if a new language can be added (requires loaded localizations).
        /// </summary>
        /// <returns>True if localizations are loaded; otherwise, false.</returns>
        private bool CanAddLang()
        {
            return M3LocalizationCategories != null && M3LocalizationCategories.Any() && M3CLocalizationCategories != null && M3CLocalizationCategories.Any();
        }

        /// <summary>
        /// Checks the current language against INT to find missing localization strings.
        /// </summary>
        /// <param name="obj">Boolean indicating if this is for ME3TweaksCore (true) or ME3TweaksModManager (false).</param>
        private void CheckMissingStrings(object obj)
        {
            if (obj is bool m3core)
            {
                if (CurrentLanguage == null)
                {
                    MessageBox.Show("Please select a language first.", "No Language Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var categories = m3core ? M3CLocalizationCategories : M3LocalizationCategories;
                var projectName = m3core ? "ME3TweaksCore" : "ME3Tweaks Mod Manager";
                
                var missingStrings = new List<(string category, string key, string englishString)>();
                
                foreach (var cat in categories)
                {
                    foreach (var str in cat.LocalizedStringsForSection)
                    {
                        // Check if the localized string is missing or empty for the current language
                        if (!str.Localizations.TryGetValue(CurrentLanguage.LangCode, out var localizedValue) || 
                            string.IsNullOrWhiteSpace(localizedValue))
                        {
                            missingStrings.Add((cat.CategoryName, str.key, str.EnglishString));
                        }
                    }
                }

                if (missingStrings.Any())
                {
                    // Find and highlight the first missing string before showing the dialog
                    var firstMissing = missingStrings[0];
                    var firstCategory = categories.FirstOrDefault(c => c.CategoryName == firstMissing.category);
                    if (firstCategory != null)
                    {
                        // Ensure the correct tab is selected
                        SelectedTabIndex = m3core ? 1 : 0;
                        
                        // Select the category
                        if (m3core)
                        {
                            M3CSelectedCategory = firstCategory;
                        }
                        else
                        {
                            M3SelectedCategory = firstCategory;
                        }

                        // Find the specific string in the category
                        var firstString = firstCategory.LocalizedStringsForSection.FirstOrDefault(s => s.key == firstMissing.key);
                        if (firstString != null)
                        {
                            // Use the find functionality to locate and highlight it
                            SearchText = firstMissing.key;
                            
                            // Delay the find operation to allow UI to update
                            Dispatcher.InvokeAsync(() =>
                            {
                                Find_Clicked(null, null);
                            }, System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"Found {missingStrings.Count} missing localization strings in {projectName} for language '{CurrentLanguage.FullName}' ({CurrentLanguage.LangCode}):");
                    sb.AppendLine();
                    
                    int displayCount = Math.Min(3, missingStrings.Count);
                    for (int i = 0; i < displayCount; i++)
                    {
                        var (category, key, englishString) = missingStrings[i];
                        sb.AppendLine($"Category: {category}");
                        sb.AppendLine($"  Key: {key}");
                        sb.AppendLine($"  English: {englishString}");
                        sb.AppendLine();
                    }

                    if (missingStrings.Count > 3)
                    {
                        sb.AppendLine($"... and {missingStrings.Count - 3} more");
                    }

                    var result = MessageBox.Show(sb.ToString(), 
                        "Missing Localization Strings", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"All strings in {projectName} are localized for '{CurrentLanguage.FullName}' ({CurrentLanguage.LangCode})!", 
                        "Complete Localization", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// Opens the auto-save directory in Windows Explorer.
        /// </summary>
        private void OpenAutosavesLocation()
        {
            Process.Start("explorer.exe", GetAppDataFolder());
        }

        /// <summary>
        /// Saves the localized dynamic help menu XML to a file.
        /// </summary>
        private void SaveLocalizedHelpMenu()
        {
            string lang = CurrentLanguage?.LangCode;

            XDocument doc = new XDocument();
            var localizations = new XElement("localizations");
            doc.Add(localizations);

            // Add the non-localized items first
            foreach (var v in nonLocalizedHelpSections)
            {
                localizations.Add(XElement.Parse(v));
            }


            try
            {
                foreach (var v in dynamicHelpLocalizations)
                {
                    if (v.Key == lang)
                    {
                        localizations.Add(XElement.Parse(localizedEditor.Text));
                    }
                    else
                    {
                        localizations.Add(XElement.Parse(v.Value));
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error saving XML: " + e.Message);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Title = "Save latesthelp-localized.xml file",
                Filter = "XML files|*.xml",
                FileName = "latesthelp-localized.xml",
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, doc.ToString());
                MessageBox.Show(
                    "Saved. Upload this file to github at ME3TweaksModManager/blob/staticfiles/liveservices/staticfiles/v1/dynamichelp/dynamichelp.xml on your localization's fork (on the localization branch) and create a pull request against the latest localization branch.");
            }
        }

        /// <summary>
        /// Loads the localized help menu for the current language into the editor.
        /// </summary>
        private void LoadLocalizedHelpMenu()
        {
            string lang = CurrentLanguage?.LangCode;
            localizedEditor.Text = "";
            if (dynamicHelpLocalizations.TryGetValue(lang, out var text))
            {
                localizedEditor.Text = text;
            }
        }

        /// <summary>
        /// Saves tutorial localizations to a text file for upload to the tutorial service.
        /// </summary>
        private void SaveTutorialLocalization()
        {
            string lang = CurrentLanguage?.LangCode;
            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Title = "Save tutorial localization file",
                Filter = "Text files|*.txt",
                FileName = $"localizedtutorial_{lang}.txt"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < LocalizedTutorialService.Count; i++)
                {
                    var str = LocalizedTutorialService[i].LocalizedStr;
                    if (string.IsNullOrWhiteSpace(str)) str = "NULL";
                    sb.AppendLine(str.Replace("\r\n", "\\n").Replace("\n", "\\n"));
                    sb.AppendLine(); //add space between lines

                }

                File.WriteAllText(saveFileDialog.FileName, sb.ToString());
                MessageBox.Show(
                    "Saved. Send this file to Mgamerz to upload into the ME3Tweaks tutorial service database.");
            }
        }

        /// <summary>
        /// Saves tips localizations to a text file for upload to the tips service.
        /// </summary>
        private void SaveTipsLocalization()
        {
            string lang = CurrentLanguage?.LangCode;

            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Title = "Save tips localization file",
                Filter = "Text files|*.txt",
                FileName = $"localizedtips_{lang}.txt"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < LocalizedTips.Count; i++)
                {
                    var str = LocalizedTips[i].LocalizedStr;
                    if (string.IsNullOrWhiteSpace(str)) str = "NULL";
                    sb.AppendLine(str.Replace("\r\n", "\\n").Replace("\n", "\\n"));
                    sb.AppendLine(); //add space between lines
                }

                File.WriteAllText(saveFileDialog.FileName, sb.ToString());
                MessageBox.Show("Saved. Send this file to Mgamerz to upload into the ME3Tweaks tips database.");
            }
        }

        /// <summary>
        /// Copies the current localization XAML to the clipboard.
        /// </summary>
        /// <param name="obj">Boolean indicating if this is for ME3TweaksCore (true) or ME3TweaksModManager (false).</param>
        private void CopyLocalization(object obj)
        {
            string lang = CurrentLanguage?.LangCode;

            if (obj is bool m3core)
            {
                var sb = CreateXamlDocument(m3core);
                Clipboard.SetText(sb);
                MessageBox.Show(
                    $"The contents for the {(m3core ? "ME3TweaksCore" : "ME3Tweaks Mod Manager")} {lang}.xaml file have been copied to your clipboard. Paste into the github editor to update it, then submit a pull request. Once the request is approved, it will be reflected in this program's interface.");
            }
        }

        /// <summary>
        /// Opens a file dialog to load a localization XAML file.
        /// </summary>
        /// <param name="obj">Boolean indicating if this is for ME3TweaksCore (true) or ME3TweaksModManager (false).</param>
        private void LoadLocalization(object obj)
        {
            if (obj is bool m3core)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog()
                {
                    Title = "Select [lang].xaml file",
                    Filter = "Xaml files|*.xaml"
                };

                var categories = m3core ? M3CLocalizationCategories : M3LocalizationCategories;
                if (openFileDialog.ShowDialog() == true)
                {
                    var fname = openFileDialog.FileName;
                    var langCode = Path.GetFileNameWithoutExtension(fname).ToLower();
                    if (langCode.StartsWith("m3c-")) langCode = langCode.Substring(4); // Remove autosave m3c-
                    if (langCode.StartsWith("m3-")) langCode = langCode.Substring(3); // Remove autosave m3-
                    if (langCode.Length != 3)
                    {
                        MessageBox.Show(
                            "Filename must be XXX.xaml, with XXX being your language code. The file selected does not match this system.");
                        return;
                    }

                    //Wipe existing strings for that lang
                    foreach (var cat in categories)
                    {
                        foreach (var ls in cat.LocalizedStringsForSection)
                        {
                            ls.Localizations.Remove(langCode);
                        }
                    }

                    //Load lang from file
                    var localizationXamlDict = new Dictionary<string, string>();
                    localizationXamlDict[langCode] = File.ReadAllText(fname);
                    try
                    {
                        parseLocalizations(categories.ToList(), localizationXamlDict);

                        LocalizationLanguage locLang = Languages.FirstOrDefault(x => x.LangCode == langCode);
                        if (locLang == null)
                        {
                            locLang = new LocalizationLanguage()
                            { Selected = false, FullName = langCode, LangCode = langCode };
                            Languages.Add(locLang);
                        }

                        foreach (var lang in Languages)
                        {
                            lang.Selected = false;
                        }

                        locLang.Selected = true;
                        CurrentLanguage = locLang;
                        MessageBox.Show($"Loaded {(m3core ? "ME3TweaksCore" : "ME3Tweaks Mod Manager")} localization for {langCode}.");
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(this,
                            $"Loading {(m3core ? "ME3TweaksCore" : "ME3Tweaks Mod Manager")} localization file {langCode}.xaml failed: {e.Message}. Contact Mgamerz and provide file being loaded");
                    }
                }
            }
        }

        /// <summary>
        /// Determines if a localization can be saved (requires loaded categories).
        /// </summary>
        /// <param name="obj">Boolean indicating which project to check.</param>
        /// <returns>True if localizations are loaded; otherwise, false.</returns>
        private bool CanSaveLocalization(object obj)
        {
            if (obj is bool m3core)
            {
                if (!m3core && M3LocalizationCategories.Any()) return true;
                if (m3core && M3CLocalizationCategories.Any()) return true;
                return false;
            }

            return false;
        }

        /// <summary>
        /// Creates a complete XAML ResourceDictionary document from the current localizations.
        /// Validates interpolations and formats the output according to XAML standards.
        /// </summary>
        /// <param name="m3core">If true, generates for ME3TweaksCore; otherwise, for ME3TweaksModManager.</param>
        /// <returns>The complete XAML document as a string.</returns>
        private string CreateXamlDocument(bool m3core)
        {
            string lang = CurrentLanguage?.LangCode;
            var categories = m3core ? M3CLocalizationCategories : M3LocalizationCategories;

            // Check interpolations
            foreach (var cat in categories)
            {
                foreach (var str in cat.LocalizedStringsForSection)
                {
                    var lstr = str.LocalizedStr;
                    if (!string.IsNullOrEmpty(lstr))
                    {
                        var checkRes = checkInterpolations(lstr);
                        if (!checkRes.ok)
                        {
                            MessageBox.Show(
                                $"Error in localized string:\nCategory: {cat.CategoryName}\nString ID: {str.key}\n\nError: {checkRes.failurereason}");
                        }
                    }
#if DEBUG
                    else if (FullySupportedLangs.Contains(lang) && lstr == null)
                    {
                        Debug.WriteLine($"{lang} is missing string {str.key}");
                    }
#endif
                }
            }

            StringBuilder sb = new StringBuilder();
            //Add header
            sb.AppendLine("<ResourceDictionary\txmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"");
            sb.AppendLine("\t\t\t\t\txmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
            sb.AppendLine("\t\t\t\t\txmlns:system=\"clr-namespace:System;assembly=System.Runtime\">");

            // the if debug statements strip out extra comments and whitespace in the generated document.
            bool isFirst = true;
            foreach (var cat in categories)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
#if DEBUG
                    if (lang == "int")
                    {
                        sb.AppendLine(); //blank line
                    }
#endif
                }

#if DEBUG
                if (lang == "int")
                {
                    sb.AppendLine($"\t<!-- {cat.CategoryName} -->");
                }
#endif
                foreach (var str in cat.LocalizedStringsForSection)
                {
                    if (str.LocalizedStr == null) continue; //don't even bother
                    string line = $"\t<system:String x:Key=\"{str.key}\"";
                    if (str.preservewhitespace)
                    {
                        line += " xml:space=\"preserve\"";
                    }

                    line += $">{str.LocalizedStr.Trim()}</system:String>";
                    sb.AppendLine(line);
#if DEBUG
                    if (lang == "int")
                    {
                        // This reduces output size for non-english localizations since they are built off the INT version.
                        // We don't need these comments
                        if (!string.IsNullOrWhiteSpace(str.notes))
                        {
                            line = $"\t<!-- {str.notes} -->";
                            sb.AppendLine(line);
                        }
                    }
#endif
                }
            }

            sb.AppendLine("</ResourceDictionary>");
            return sb.ToString();

        }

        /// <summary>
        /// Validates that string interpolations (e.g., {0}, {1}) are correctly formatted.
        /// </summary>
        /// <param name="lstr">The localized string to check.</param>
        /// <returns>A tuple indicating success and a failure reason if validation fails.</returns>
        private (bool ok, string failurereason) checkInterpolations(string lstr)
        {
            // Check for { and } with items in them that are not 0-9.
            int i = -1; //will index to 1 on start
            int openBracePos = -1;
            while (i < lstr.Length - 1)
            {
                i++;
                if (lstr[i] == '{')
                {
                    if (openBracePos != -1)
                    {
                        return (false, "Unclosed opening {");
                    }

                    openBracePos = i;
                    continue;
                }

                if (lstr[i] == '}')
                {
                    if (openBracePos == -1)
                    {
                        return (false, "Found closing }, however no matching opening {");
                    }

                    var contentsOfInterp = lstr.Substring(openBracePos + 1, i - openBracePos - 1);
                    if (!int.TryParse(contentsOfInterp, out var _))
                    {
                        return (false, $"Contents of interpolated item must be integer, found '{contentsOfInterp}'");
                    }

                    openBracePos = -1;

                    continue;
                }
            }

            if (openBracePos != -1)
            {
                return (false, "Unclosed opening {");
            }

            return (true, null);
        }

        /// <summary>
        /// Saves the current localization to a XAML file via a file dialog.
        /// </summary>
        /// <param name="obj">Boolean indicating if this is for ME3TweaksCore (true) or ME3TweaksModManager (false).</param>
        private void SaveLocalization(object obj)
        {
            if (obj is bool m3core)
            {
                string lang = CurrentLanguage?.LangCode;

                var sb = CreateXamlDocument(m3core);

                SaveFileDialog saveFileDialog = new SaveFileDialog()
                {
                    Title = $"Save {(m3core ? "ME3TweaksCore" : "ME3Tweaks Mod Manager")} localization file",
                    Filter = "Xaml files|*.xaml",
                    FileName = $"{lang}.xaml"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    if (Path.GetFileNameWithoutExtension(saveFileDialog.FileName).Length != 3)
                    {
                        MessageBox.Show($"Filename must match localization 3 character name ({lang}).");
                        return;
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb);
                    MessageBox.Show($"Saved.");
                }
            }
        }

        /// <summary>
        /// Represents a category grouping of localized strings.
        /// </summary>
        [DebuggerDisplay("LocCat {CategoryName} with {LocalizedStringsForSection.Count} entries")]
        public class LocalizationCategory : INotifyPropertyChanged
        {
            /// <summary>
            /// Gets or sets the name of this localization category.
            /// </summary>
            public string CategoryName { get; set; }
            
            /// <summary>
            /// Gets whether any strings in this category have changed from the previous version or are not localized.
            /// </summary>
            public bool HasChangedStrings => LocalizedStringsForSection.Any(x => x.ChangedFromPrevious || x.LocalStringNotLocalized);

            /// <summary>
            /// Collection of localized strings within this category.
            /// </summary>
            public ObservableCollectionExtended<LocalizedString> LocalizedStringsForSection { get; } =
                new ObservableCollectionExtended<LocalizedString>();

            /// <summary>
            /// Notifies that the language has changed and UI should update.
            /// </summary>
            public void OnLanguageChanged()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasChangedStrings)));
            }

            /// <summary>
            /// Occurs when a property value changes.
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;
        }

        /// <summary>
        /// Represents a single localizable string with translations in multiple languages.
        /// </summary>
        public class LocalizedString : INotifyPropertyChanged
        {
            /// <summary>
            /// Gets or sets the unique identifier key for this localized string.
            /// </summary>
            public string key { get; set; }

            /// <summary>
            /// Gets or sets whether whitespace (including newlines) should be preserved in the output.
            /// </summary>
            public bool preservewhitespace { get; set; }

            /// <summary>
            /// Gets or sets optional notes or comments about this string for translators.
            /// </summary>
            public string notes { get; set; }

            /// <summary>
            /// Gets or initializes the English (INT) version of this string.
            /// </summary>
            public string EnglishString { get; init; }

            /// <summary>
            /// Dictionary containing translations for each language code.
            /// </summary>
            public readonly Dictionary<string, string> Localizations = new();

            /// <summary>
            /// Gets or sets the localized string for the currently selected language.
            /// </summary>
            public string LocalizedStr
            {
                get
                {
                    if (LocalizationTablesUI.CurrentLanguage == null) return null;
                    if (Localizations.TryGetValue(LocalizationTablesUI.CurrentLanguage.LangCode, out var str))
                    {
                        return str;
                    }

                    return null;
                }
                set
                {
                    if (LocalizationTablesUI.CurrentLanguage == null) return;
                    Localizations[LocalizationTablesUI.CurrentLanguage.LangCode] = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedStr)));
                }
            }

            /// <summary>
            /// Gets or sets whether this string has changed from the previous build.
            /// </summary>
            public bool ChangedFromPrevious { get; set; }
            
            /// <summary>
            /// Gets whether the current language's string is missing or empty.
            /// </summary>
            public bool LocalStringNotLocalized
            {
                get { return LocalizationTablesUI.CurrentLanguage != null && string.IsNullOrWhiteSpace(LocalizedStr); }
            }

            /// <summary>
            /// Notifies that the current language has changed and properties should rebind.
            /// </summary>
            public void OnCurrentLanguageChanged()
            {
                // Rebind
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedStr)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalStringNotLocalized)));
            }

            /// <summary>
            /// Occurs when a property value changes.
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        
        /// <summary>
        /// Gets or sets the currently selected item in the ME3TweaksModManager data grid.
        /// </summary>
        public LocalizedString SelectedDataGridItem { get; set; }
        
        /// <summary>
        /// Gets or sets the currently selected item in the ME3TweaksCore data grid.
        /// </summary>
        public LocalizedString SelectedDataGridItemM3C { get; set; }

        /// <summary>
        /// Gets or sets the text to search for in localizations.
        /// </summary>
        public string SearchText { get; set; } = "";

        /// <summary>
        /// Gets or sets the currently selected tab index (0 = M3, 1 = M3C, etc.).
        /// </summary>
        public int SelectedTabIndex { get; set; }


        /// <summary>
        /// Handles the Find button click event. Searches for the next occurrence of the search text
        /// in keys, English strings, or localized strings.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Find_Clicked(object sender, RoutedEventArgs e)
        {
            var m3core = SelectedTabIndex == 1;
            var categories = m3core ? M3CLocalizationCategories : M3LocalizationCategories;
            var currentcategory = m3core ? M3CSelectedCategory : M3SelectedCategory;
            var currentSelectedItem = m3core ? SelectedDataGridItemM3C : SelectedDataGridItem;
            int indexOfCurrentCategory = currentcategory != null ? categories.IndexOf(currentcategory) : 0;
            Debug.WriteLine("Current cat index: " + indexOfCurrentCategory);

            int numCategories = categories.Count(); //might need to +1 this
            string searchTerm = SearchText.ToLower();
            if (string.IsNullOrEmpty(searchTerm)) return;
            LocalizedString itemToHighlight = null;
            LocalizationCategory catToHighlight = null;
            for (int i = 0; i < numCategories; i++)
            {
                bool found = false;
                LocalizationCategory cat = categories[(i + indexOfCurrentCategory) % categories.Count()];
                int startSearchIndex = 0;
                int numToSearch = cat.LocalizedStringsForSection.Count();
                if (i == 0 && cat == currentcategory && currentSelectedItem != null)
                {
                    startSearchIndex = cat.LocalizedStringsForSection.IndexOf(currentSelectedItem) + 1;
                    numToSearch -= startSearchIndex;
                }

                Debug.WriteLine(cat.CategoryName);
                for (int j = 0; j < numToSearch; j++)
                {
                    var ls = cat.LocalizedStringsForSection[
                        (j + startSearchIndex) % cat.LocalizedStringsForSection.Count];

                    //Key
                    if (ls.key.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //English
                    if (ls.EnglishString.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //Lang
                    if (CurrentLanguage != null && CurrentLanguage.Contains(ls, searchTerm))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }
                }

                if (found)
                {
                    break;
                }
            }

            if (itemToHighlight == null)
            {
                SystemSounds.Beep.Play();
            }
            else
            {
                if (m3core)
                {
                    M3CSelectedCategory = catToHighlight;
                    SelectedDataGridItemM3C = itemToHighlight;
                    M3CCategoriesListBox.ScrollIntoView(catToHighlight);
                    DataGridTableM3C.ScrollIntoView(SelectedDataGridItemM3C);
                }
                else
                {
                    M3SelectedCategory = catToHighlight;
                    SelectedDataGridItem = itemToHighlight;
                    M3CategoriesListBox.ScrollIntoView(catToHighlight);
                    DataGridTable.ScrollIntoView(SelectedDataGridItem);
                }
            }
        }

        /// <summary>
        /// Handles the KeyDown event in the search box to trigger search on Enter key.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Keyboard event arguments.</param>
        private void SeachBox_OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                Find_Clicked(null, null);
            }
        }

        /// <summary>
        /// Handles language selection click events from the UI.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Language_Clicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fw && fw.DataContext is LocalizationLanguage ll)
            {
                ChangeLanguage(ll);
            }
        }

        /// <summary>
        /// Changes the active language and updates all localized strings in the UI.
        /// </summary>
        /// <param name="ll">The language to switch to.</param>
        private void ChangeLanguage(LocalizationLanguage ll)
        {
            CurrentLanguage = ll;
            if (M3SelectedCategory != null)
            {
                foreach (var ls in M3SelectedCategory.LocalizedStringsForSection)
                {
                    ls.OnCurrentLanguageChanged();
                }

                M3SelectedCategory.OnLanguageChanged();
            }
            if (M3CSelectedCategory != null)
            {
                foreach (var ls in M3CSelectedCategory.LocalizedStringsForSection)
                {
                    ls.OnCurrentLanguageChanged();
                }
                M3CSelectedCategory.OnLanguageChanged();
            }
            foreach (var lts in LocalizedTutorialService)
            {
                lts.OnCurrentLanguageChanged();
            }
            foreach (var ltip in LocalizedTips)
            {
                ltip.OnCurrentLanguageChanged();
            }
            LoadLocalizedHelpMenu();

            // update all sections to accurately reflect the substrings.
            foreach (var sect in M3LocalizationCategories.Concat(M3CLocalizationCategories))
            {
                sect.OnLanguageChanged(); // Refresh 
            }

        }
    }
}