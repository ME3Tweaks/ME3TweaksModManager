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
using PropertyChanged;
using Localization = ICSharpCode.AvalonEdit.Search.Localization;
using Path = System.IO.Path;

namespace LocalizationHelper
{
    /// <summary>
    /// Interaction logic for LocalizationTablesUI.xaml
    /// </summary>
    public partial class LocalizationTablesUI : Window, INotifyPropertyChanged
    {
        public Visibility LoadingVisibility { get; set; } = Visibility.Visible;
        private string[] FullySupportedLangs = { "deu", "rus", "pol", "bra", "ita", "fra" };

        public List<string> GlobalSupportedLanguages = new List<string>();

        public LocalizationTablesUI()
        {
            Title = $"ME3Tweaks Mod Manager Localizer {Assembly.GetExecutingAssembly().GetName().Version}";

            GlobalSupportedLanguages.AddRange(FullySupportedLangs);
            LoadCommands();
            InitializeComponent();

            // Load official languages
            Languages.Add(new LocalizationLanguage() { Selected = false, LangCode = "deu", FullName = "German" });
            Languages.Add(new LocalizationLanguage() { Selected = false, LangCode = "rus", FullName = "Russian" });
            Languages.Add(new LocalizationLanguage() { Selected = false, LangCode = "pol", FullName = "Polish" });
            Languages.Add(new LocalizationLanguage() { Selected = false, LangCode = "ita", FullName = "Italian" });
            Languages.Add(new LocalizationLanguage() { Selected = false, LangCode = "fra", FullName = "French" });

            //Load M3 localizations
            LoadLocalizations(true, @"ME3TweaksModManager", @"MassEffectModManagerCore/modmanager/localizations/", M3LocalizationBranches, M3LocalizationCategories);
            LoadLocalizations(false, @"ME3TweaksCore", @"ME3TweaksCore/Localization/Dictionaries/", M3CLocalizationBranches, M3CLocalizationCategories);
        }

        public static LocalizationLanguage CurrentLanguage { get; set; }
        public ObservableCollectionExtended<LocalizationLanguage> Languages { get; } = new();

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
                locSavePath = Path.Combine(GetAppDataFolder(), $"m3c-{lang}.xaml");
                File.WriteAllText(locSavePath, sb);

            }
            catch
            {
                // DO NOT CRASH
            }
        }

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


        public string PleaseWaitString { get; set; } = "Please wait, starting up";

        public ObservableCollectionExtended<string> M3LocalizationBranches { get; } =
            new ObservableCollectionExtended<string>();

        public ObservableCollectionExtended<string> M3CLocalizationBranches { get; } =
            new ObservableCollectionExtended<string>();

        public ObservableCollectionExtended<LocalizedString> LocalizedTips { get; } =
            new ObservableCollectionExtended<LocalizedString>();

        public ObservableCollectionExtended<LocalizedString> LocalizedTutorialService { get; } =
            new ObservableCollectionExtended<LocalizedString>();

        public string M3SelectedBranch { get; set; }
        public string M3CSelectedBranch { get; set; }

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
                WebClient client = new WebClient();
                foreach (var lang in GlobalSupportedLanguages.Concat(new[] { "int" }))
                {
                    PleaseWaitString = $"Fetching {branch} {lang}";

                    var url = endpoint + lang + ".xaml";
                    try
                    {

                        var dict = client.DownloadStringAwareOfEncoding(url);
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

                    if (oldStuff.TryGetValue(lineInfo.key, out var oldString))
                    {
                        var oldValue = new XText(oldString).ToString();
                        var newValue = new XText(lineInfo.text).ToString();
                        XDocument newV = XDocument.Parse("<text>" + lineInfo.text + "</text>");
                        if (oldString != newV.Root.Value)
                        {
                            if (ls.key == "string_modEndorsed") Debugger.Break();
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

                    // TUTORIAL SERVICE
                    PleaseWaitString = $"Fetching Tutorial Service";

                    string tutorialEndpoint = "https://me3tweaks.com/modmanager/services/tutorialservice";
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

                    PleaseWaitString = "";

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

                    autosaveTimer = new DispatcherTimer();
                    autosaveTimer.Tick += AutoSave;
                    autosaveTimer.Interval = new TimeSpan(0, 1, 0);
                    autosaveTimer.Start();
                }
            };
            bw.RunWorkerAsync();
        }


        public DispatcherTimer autosaveTimer;
        private string m3oldBranch = null;
        private string m3coldBranch = null;
        private Dictionary<string, string> dynamicHelpLocalizations = new Dictionary<string, string>();

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
        /// Parses a dictionary of 
        /// </summary>
        /// <param name="categories">List of categories to apply localizations to</param>
        /// <param name="langToXamlMap">map of languageCode to Xaml document text</param>
        private void parseLocalizations(List<LocalizationCategory> categories, Dictionary<string, string> langToXamlMap)
        {
            foreach (var lang in langToXamlMap.Keys)
            {
                var langLines = Regex.Split(langToXamlMap[lang], "\r\n|\r|\n");
                int numBlankLines = 0;
                for (int i = 3; i < langLines.Length - 2; i++) // star at line 3 and skip forward
                {
                    var line = langLines[i].Trim();
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

        public LocalizationCategory M3SelectedCategory { get; set; }
        public LocalizationCategory M3CSelectedCategory { get; set; }

        public ObservableCollectionExtended<LocalizationCategory> M3LocalizationCategories { get; } =
            new ObservableCollectionExtended<LocalizationCategory>();

        public ObservableCollectionExtended<LocalizationCategory> M3CLocalizationCategories { get; } =
            new ObservableCollectionExtended<LocalizationCategory>();

        public ICommand SaveLocalizationCommand { get; set; }
        public ICommand CopyLocalizationCommand { get; set; }
        public ICommand LoadLocalizationCommand { get; set; }
        public ICommand SaveTipsLocalizationCommand { get; set; }
        public ICommand LoadLocalizedHelpMenuCommand { get; set; }
        public ICommand SaveLocalizedHelpMenuCommand { get; set; }
        public ICommand SaveTutorialLocalizationCommand { get; set; }
        public ICommand OpenAutosaveDirCommand { get; set; }
        public ICommand AddLangCommand { get; set; }

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
        }

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

        private bool CanAddLang()
        {
            return M3LocalizationCategories != null && M3LocalizationCategories.Any() && M3CLocalizationCategories != null && M3CLocalizationCategories.Any();
        }

        private void OpenAutosavesLocation()
        {
            Process.Start("explorer.exe", GetAppDataFolder());
        }

        private void SaveLocalizedHelpMenu()
        {
            string lang = CurrentLanguage?.LangCode;

            XDocument doc = new XDocument();
            var localizations = new XElement("localizations");
            doc.Add(localizations);
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
                FileName = "latesthelp-localized.txt",
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, doc.ToString());
                MessageBox.Show(
                    "Saved. Upload this file to github at MassEffectModManagerCore/staticfiles/dynamichelp/latesthelp-localized.xml on your localization's fork (on the localization branch) and create a pull request against the latest localization branch.");
            }
        }

        private void LoadLocalizedHelpMenu()
        {
            string lang = CurrentLanguage?.LangCode;
            localizedEditor.Text = "";
            if (dynamicHelpLocalizations.TryGetValue(lang, out var text))
            {
                localizedEditor.Text = text;
            }
        }

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
        /// Opens a file
        /// </summary>
        private void LoadLocalization(object obj)
        {
            if (obj is bool m3core)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog()
                {
                    Title = "Select [lang].xaml file",
                    Filter = "Xaml files|*.xaml"
                };

                var categories = m3core ? M3CLocalizationCategories : M3CLocalizationCategories;
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
                    sb.AppendLine(); //blank line
#endif
                }

#if DEBUG
                sb.AppendLine($"\t<!-- {cat.CategoryName} -->");
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
                    // This reduces output size for non-english localizations since they are built off the INT version.
                    // We don't need these comments
                    if (!string.IsNullOrWhiteSpace(str.notes))
                    {
                        line = $"\t<!-- {str.notes} -->";
                        sb.AppendLine(line);
                    }
#endif
                }
            }

            sb.AppendLine("</ResourceDictionary>");
            return sb.ToString();

        }

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

        [DebuggerDisplay("LocCat {CategoryName} with {LocalizedStringsForSection.Count} entries")]
        [AddINotifyPropertyChangedInterface]
        public class LocalizationCategory
        {
            public string CategoryName { get; set; }
            public bool HasChangedStrings => LocalizedStringsForSection.Any(x => x.ChangedFromPrevious);

            public ObservableCollectionExtended<LocalizedString> LocalizedStringsForSection { get; } =
                new ObservableCollectionExtended<LocalizedString>();
        }

        public class LocalizedString : INotifyPropertyChanged
        {
            /// <summary>
            /// The ID of the string
            /// </summary>
            public string key { get; set; }

            /// <summary>
            /// If we should preserver whitespace (e.g. it has newlines)
            /// </summary>
            public bool preservewhitespace { get; set; }

            /// <summary>
            /// Notes about this string (if any)
            /// </summary>
            public string notes { get; set; }

            /// <summary>
            /// The english string
            /// </summary>
            public string EnglishString { get; init; }

            /// <summary>
            /// The dictionary containing each localized language's string
            /// </summary>
            public readonly Dictionary<string, string> Localizations = new();

            /// <summary>
            /// The localized string for the current language
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

            public bool ChangedFromPrevious { get; set; }

            public void OnCurrentLanguageChanged()
            {
                // Rebind
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedStr)));
            }

#pragma warning disable
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        public LocalizedString SelectedDataGridItem { get; set; }

        public string SearchText { get; set; } = "";

        public int SelectedTabIndex { get; set; }


        private void Find_Clicked(object sender, RoutedEventArgs e)
        {

            int indexOfCurrentCategory =
                M3SelectedCategory != null ? M3LocalizationCategories.IndexOf(M3SelectedCategory) : 0;
            Debug.WriteLine("Current cat index: " + indexOfCurrentCategory);

            int numCategories = M3LocalizationCategories.Count(); //might need to +1 this
            string searchTerm = SearchText.ToLower();
            if (string.IsNullOrEmpty(searchTerm)) return;
            LocalizedString itemToHighlight = null;
            LocalizationCategory catToHighlight = null;
            for (int i = 0; i < numCategories; i++)
            {
                bool found = false;
                LocalizationCategory cat =
                    M3LocalizationCategories[(i + indexOfCurrentCategory) % M3LocalizationCategories.Count()];
                int startSearchIndex = 0;
                int numToSearch = cat.LocalizedStringsForSection.Count();
                if (i == 0 && cat == M3SelectedCategory && SelectedDataGridItem != null)
                {
                    startSearchIndex = cat.LocalizedStringsForSection.IndexOf(SelectedDataGridItem) + 1;
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
                    if (CurrentLanguage.Contains(ls, searchTerm))
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
                M3SelectedCategory = catToHighlight;
                SelectedDataGridItem = itemToHighlight;
                DataGridTable.ScrollIntoView(SelectedDataGridItem);
            }
        }

        private void SeachBox_OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                Find_Clicked(null, null);
            }
        }

        private void Language_Clicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fw && fw.DataContext is LocalizationLanguage ll)
            {
                ChangeLanguage(ll);
            }
        }

        private void ChangeLanguage(LocalizationLanguage ll)
        {
            CurrentLanguage = ll;
            if (M3SelectedCategory != null)
            {
                foreach (var ls in M3SelectedCategory.LocalizedStringsForSection)
                {
                    ls.OnCurrentLanguageChanged();
                }
            }
            if (M3CSelectedCategory != null)
            {
                foreach (var ls in M3CSelectedCategory.LocalizedStringsForSection)
                {
                    ls.OnCurrentLanguageChanged();
                }
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

        }
    }
}