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
using Path = System.IO.Path;

namespace LocalizationHelper
{
    /// <summary>
    /// Interaction logic for LocalizationTablesUI.xaml
    /// </summary>
    public partial class LocalizationTablesUI : Window, INotifyPropertyChanged
    {
        public Visibility LoadingVisibility { get; set; } = Visibility.Visible;
        private string[] FullySupportedLangs = { "deu", "rus", "pol", "bra" };

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
            Languages.Add(new LocalizationLanguage()
            { Selected = false, LangCode = "bra", FullName = "Portugeuse (Brazilian)" });



            //Load localizations
            LoadLocalizations();
        }

        public static LocalizationLanguage CurrentLanguage { get; set; }
        public ObservableCollectionExtended<LocalizationLanguage> Languages { get; } = new();

        public void AutoSave(object sender, EventArgs eventArgs)
        {
            try
            {
                string lang = CurrentLanguage?.LangCode;

                if (lang == null) return; // Do nothing
                var sb = CreateXamlDocument();

                var locSavePath = Path.Combine(GetAppDataFolder(), $"{lang}.xaml");
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

        public ObservableCollectionExtended<string> LocalizationBranches { get; } =
            new ObservableCollectionExtended<string>();

        public ObservableCollectionExtended<LocalizedString> LocalizedTips { get; } =
            new ObservableCollectionExtended<LocalizedString>();

        public ObservableCollectionExtended<LocalizedString> LocalizedTutorialService { get; } =
            new ObservableCollectionExtended<LocalizedString>();

        public string SelectedBranch { get; set; }

        private void LoadLocalizations(string branch = null)
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (x, y) =>
            {
                if (!LocalizationBranches.Any())
                {
                    PleaseWaitString = "Fetching remote localization branches";
                    var ghclient = new GitHubClient(new ProductHeaderValue(@"ME3TweaksModManager"));
                    try
                    {
                        var branches = ghclient.Repository.Branch.GetAll("ME3Tweaks", "ME3TweaksModManager").Result;
                        var locbranches = branches.Where(x => /*x.Name.Contains("master") ||*/
                            x.Name.Contains("-localization"));
                        System.Windows.Application.Current.Dispatcher.Invoke(delegate
                        {
                            LocalizationBranches.ReplaceAll(locbranches.Select(x => x.Name)
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
                if (LocalizationBranches.Any())
                {
                    if (branch == null)
                    {
                        branch = LocalizationBranches.First();
                        SelectedBranch = branch;
                        oldBranch = branch;
                        if (LocalizationBranches.Count() > 1)
                        {
                            oldBuildBranch = LocalizationBranches[1];
                        }
                    }
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(delegate
                    {
                        MessageBox.Show(
                            "Could not find any branches on ME3TweaksModManager repo containing name 'localization'");
                    });
                    return;
                }

                var dictionaries = new Dictionary<string, string>();
                string endpoint =
                    $"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/{branch}/MassEffectModManagerCore/modmanager/localizations/"; //make dynamic, maybe with octokit.
                WebClient client = new WebClient();
                foreach (var lang in GlobalSupportedLanguages.Concat(new[] { "int" }))
                {
                    PleaseWaitString = $"Fetching {branch} {lang}";

                    var url = endpoint + lang + ".xaml";
                    var dict = client.DownloadStringAwareOfEncoding(url);
                    dictionaries[lang] = dict;
                }

                if (oldBuildBranch != null)
                {
                    PleaseWaitString = $"Fetching {oldBuildBranch} int";

                    endpoint =
                        $"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/{oldBuildBranch}/MassEffectModManagerCore/modmanager/localizations/"; //make dynamic, maybe with octokit.
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
                            Debug.WriteLine("Changed: " + ls.key);
                            Debug.WriteLine("  OLD: " + oldString);
                            Debug.WriteLine("  NEW: " + lineInfo.text);
                            ls.ChangedFromPrevious = true;
                        }
                    }
                    else if (oldStuff.Any())
                    {
                        Debug.WriteLine("New: " + ls.key);
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

                System.Windows.Application.Current.Dispatcher.Invoke(delegate { LocalizedTips.ReplaceAll(locTips); });

                //DYNAMIC HELP
                PleaseWaitString = $"Fetching Dynamic Help";

                endpoint =
                    $"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/{branch}/MassEffectModManagerCore/staticfiles/dynamichelp/latesthelp-localized.xml";
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
            };
            bw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null && b.Result is List<LocalizationCategory> categories)
                {
                    LoadingVisibility = Visibility.Collapsed;
                    LocalizationCategories.ReplaceAll(categories.OrderBy(x => x.CategoryName));

                    autosaveTimer = new DispatcherTimer();
                    autosaveTimer.Tick += AutoSave;
                    autosaveTimer.Interval = new TimeSpan(0, 1, 0);
                    autosaveTimer.Start();
                }
            };
            bw.RunWorkerAsync();
        }


        public DispatcherTimer autosaveTimer;
        private string oldBranch = null;
        private Dictionary<string, string> dynamicHelpLocalizations = new Dictionary<string, string>();

        public void OnSelectedBranchChanged()
        {
            if (oldBranch != null)
            {
                if (SelectedBranch != null)
                {
                    LoadLocalizations(SelectedBranch);
                    oldBranch = SelectedBranch;
                }
                else
                {
                    LocalizationCategories.ClearEx();
                    LocalizedTips.ClearEx();
                    dynamicHelpLocalizations.Clear();
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

        public LocalizationCategory SelectedCategory { get; set; }

        public ObservableCollectionExtended<LocalizationCategory> LocalizationCategories { get; } =
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
            SaveLocalizationCommand = new GenericCommand(SaveLocalization, CanSaveLocalization);
            CopyLocalizationCommand = new GenericCommand(CopyLocalization, CanSaveLocalization);
            LoadLocalizationCommand = new GenericCommand(LoadLocalization, CanSaveLocalization);
            SaveTipsLocalizationCommand = new GenericCommand(SaveTipsLocalization, CanSaveLocalization);
            SaveTutorialLocalizationCommand = new GenericCommand(SaveTutorialLocalization, CanSaveLocalization);
            LoadLocalizedHelpMenuCommand = new GenericCommand(LoadLocalizedHelpMenu, CanSaveLocalization);
            SaveLocalizedHelpMenuCommand = new GenericCommand(SaveLocalizedHelpMenu, CanSaveLocalization);
        }

        private void AddLanguage()
        {
            var result = PromptDialog.Prompt(this, "Enter a 3 letter language code for your new language.",
                "Enter lang code").Replace(" ", "");
            if (result.Length != 3)
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
                CurrentLanguage = locLang;
            }
        }

        private bool CanAddLang()
        {
            return LocalizationCategories != null && LocalizationCategories.Any();
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

        private void CopyLocalization()
        {
            string lang = CurrentLanguage?.LangCode;

            var sb = CreateXamlDocument();
            Clipboard.SetText(sb);
            MessageBox.Show(
                $"The contents for the {lang}.xaml file have been copied to your clipboard. Paste into the github editor to update it, then submit a pull request. Once the request is approved, it will be reflected in this program's interface.");
        }

        /// <summary>
        /// Opens a file
        /// </summary>
        private void LoadLocalization()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Title = "Select [lang].xaml file",
                Filter = "Xaml files|*.xaml"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                var fname = openFileDialog.FileName;
                var langCode = Path.GetFileNameWithoutExtension(fname).ToLower();
                if (langCode.Length != 3)
                {
                    MessageBox.Show(
                        "Filename must be XXX.xaml, with XXX being your language code. The file selected does not match this system.");
                    return;
                }



                //Wipe existing strings for that lang
                foreach (var cat in LocalizationCategories)
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
                    parseLocalizations(LocalizationCategories.ToList(), localizationXamlDict);

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
                    MessageBox.Show("Loaded localization for " + langCode + ".");
                }
                catch (Exception e)
                {
                    MessageBox.Show(this,
                        $"Loading localization file {langCode}.xaml failed: {e.Message}. Contact Mgamerz and provide file being loaded");
                }
            }
        }

        private bool CanSaveLocalization()
        {
            if (!LocalizationCategories.Any()) return false;
            return true;
        }

        private string CreateXamlDocument()
        {
            string lang = CurrentLanguage?.LangCode;

            // Check interpolations
            foreach (var cat in LocalizationCategories)
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

            bool isFirst = true;
            foreach (var cat in LocalizationCategories)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    sb.AppendLine(); //blank line
                }

                sb.AppendLine($"\t<!-- {cat.CategoryName} -->");
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
                    if (!string.IsNullOrWhiteSpace(str.notes))
                    {
                        line = $"\t<!-- {str.notes} -->";
                        sb.AppendLine(line);
                    }
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

        private void SaveLocalization()
        {
            string lang = CurrentLanguage?.LangCode;

            var sb = CreateXamlDocument();

            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Title = "Save localization file",
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
                SelectedCategory != null ? LocalizationCategories.IndexOf(SelectedCategory) : 0;
            Debug.WriteLine("Current cat index: " + indexOfCurrentCategory);

            int numCategories = LocalizationCategories.Count(); //might need to +1 this
            string searchTerm = SearchText.ToLower();
            if (string.IsNullOrEmpty(searchTerm)) return;
            LocalizedString itemToHighlight = null;
            LocalizationCategory catToHighlight = null;
            for (int i = 0; i < numCategories; i++)
            {
                bool found = false;
                LocalizationCategory cat =
                    LocalizationCategories[(i + indexOfCurrentCategory) % LocalizationCategories.Count()];
                int startSearchIndex = 0;
                int numToSearch = cat.LocalizedStringsForSection.Count();
                if (i == 0 && cat == SelectedCategory && SelectedDataGridItem != null)
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
                SelectedCategory = catToHighlight;
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
            if (SelectedCategory != null)
            {
                foreach (var ls in SelectedCategory.LocalizedStringsForSection)
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