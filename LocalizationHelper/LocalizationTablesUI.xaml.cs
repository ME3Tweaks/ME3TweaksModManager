using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Win32;
using Newtonsoft.Json;
using Octokit;
using Application = Octokit.Application;
using Path = System.IO.Path;

namespace LocalizationHelper
{
    /// <summary>
    /// Interaction logic for LocalizationTablesUI.xaml
    /// </summary>
    public partial class LocalizationTablesUI : Window, INotifyPropertyChanged
    {
        public LocalizationTablesUI()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();

            //Load localizations
            LoadLocalizations();
        }

        public bool ShowGerman { get; set; }
        public bool ShowRussian { get; set; }
        public bool ShowPolish { get; set; }
        public bool ShowFrench { get; set; }
        public bool ShowSpanish { get; set; }
        public ObservableCollectionExtended<string> LocalizationBranches { get; } = new ObservableCollectionExtended<string>();
        public ObservableCollectionExtended<LocalizedString> LocalizedTips { get; } = new ObservableCollectionExtended<LocalizedString>();
        public string SelectedBranch { get; set; }
        private void LoadLocalizations(string branch = null)
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (x, y) =>
            {
                if (!LocalizationBranches.Any())
                {
                    var ghclient = new GitHubClient(new ProductHeaderValue(@"ME3TweaksModManager"));
                    try
                    {
                        var branches = ghclient.Repository.Branch.GetAll("ME3Tweaks", "ME3TweaksModManager").Result;
                        var locbranches = branches.Where(x => x.Name.Contains("master") || x.Name.Contains("-localization"));
                        System.Windows.Application.Current.Dispatcher.Invoke(delegate { LocalizationBranches.ReplaceAll(locbranches.Select(x => x.Name)); });
                    }
                    catch (Exception e)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(delegate { MessageBox.Show("Error getting list of localization branches: " + e.Message); });
                        return;
                    }
                }

                if (LocalizationBranches.Any())
                {
                    if (branch == null)
                    {
                        branch = LocalizationBranches.First();
                        SelectedBranch = branch;
                        oldBranch = branch;
                    }
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(delegate { MessageBox.Show("Could not find any branches on ME3TweaksModManager repo containing name 'localization'"); });
                    return;
                }

                var dictionaries = new Dictionary<string, string>();
                string endpoint = $"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/{branch}/MassEffectModManagerCore/modmanager/localizations/"; //make dynamic, maybe with octokit.
                WebClient client = new WebClient();
                foreach (var lang in LocalizedString.Languages)
                {
                    var url = endpoint + lang + ".xaml";
                    var dict = client.DownloadStringAwareOfEncoding(url);
                    dictionaries[lang] = dict;
                }

                //Parse INT.
                int currentLine = 3; //Skip header.
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
                        INT = lineInfo.text
                    };
                    if (lineInfo.key == null) Debugger.Break();
                    if (ls.INT == null) Debugger.Break();
                    cat.LocalizedStringsForSection.Add(ls);
                }

                if (cat != null)
                {
                    categories.Add(cat);
                }

                parseLocalizations(categories, dictionaries);
                y.Result = categories;

                //TIPS SERVICE
                string tipsEndpoint = "https://me3tweaks.com/modmanager/services/tipsservice";
                string contents;
                using var wc = new System.Net.WebClient();
                var tipsJson = wc.DownloadString(tipsEndpoint);
                var jsonObj = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(tipsJson);
                var langs = LocalizedString.Languages.Where(x => x != "int");
                var locTips = new List<LocalizedString>();
                for (int i = 0; i < jsonObj["int"].Count; i++)
                {
                    LocalizedString ls = new LocalizedString()
                    {
                        INT = jsonObj["int"][i]
                    };
                    foreach (var lang in langs)
                    {
                        if (jsonObj[lang].Count <= i) continue; //skip
                        switch (lang)
                        {
                            case "rus":
                                ls.RUS = jsonObj["rus"][i];
                                break;
                            case "deu":
                                ls.DEU = jsonObj["deu"][i];
                                break;
                            case "pol":
                                ls.POL = jsonObj["pol"][i];
                                break;
                            case "fra":
                                ls.FRA = jsonObj["fra"][i];
                                break;
                            case "esn":
                                ls.ESN = jsonObj["esn"][i];
                                break;
                        }
                    }
                    locTips.Add(ls);
                }
                System.Windows.Application.Current.Dispatcher.Invoke(delegate
                {
                    LocalizedTips.ReplaceAll(locTips);
                });

                //DYNAMIC HELP
                endpoint = $"https://raw.githubusercontent.com/ME3Tweaks/ME3TweaksModManager/{branch}/MassEffectModManagerCore/staticfiles/dynamichelp/latesthelp-localized.xml";
                var dynamicHelpXml = wc.DownloadString(endpoint);
                XDocument doc = XDocument.Parse(dynamicHelpXml);
                var intxml = doc.XPathSelectElement("/localizations/helpmenu[@lang='int']");
                dynamicHelpLocalizations["int"] = intxml.ToString();

                Debug.WriteLine(doc.ToString());
                foreach (var lang in langs)
                {
                    var langxml = doc.XPathSelectElement($"/localizations/helpmenu[@lang='{lang}']");
                    if (langxml != null)
                    {
                        dynamicHelpLocalizations[lang] = langxml.ToString();
                    }
                }
                /*
                var langs = LocalizedString.Languages.Where(x => x != "int");
                var locTips = new List<LocalizedString>();
                for (int i = 0; i < jsonObj["int"].Count; i++)
                {
                    LocalizedString ls = new LocalizedString()
                    {
                        INT = jsonObj["int"][i]
                    };
                    foreach (var lang in langs)
                    {
                        if (jsonObj[lang].Count <= i) continue; //skip
                        switch (lang)
                        {
                            case "rus":
                                ls.RUS = jsonObj["rus"][i];
                                break;
                            case "deu":
                                ls.DEU = jsonObj["deu"][i];
                                break;
                            case "pol":
                                ls.POL = jsonObj["pol"][i];
                                break;
                            case "fra":
                                ls.FRA = jsonObj["fra"][i];
                                break;
                            case "esn":
                                ls.ESN = jsonObj["esn"][i];
                                break;
                        }
                    }
                    locTips.Add(ls);
                }
                */
                System.Windows.Application.Current.Dispatcher.Invoke(delegate
                {
                    intViewer.Text = intxml.ToString();
                    LocalizedTips.ReplaceAll(locTips);
                });
            };
            bw.RunWorkerCompleted += (a, b) =>
                {
                    if (b.Error == null && b.Result is List<LocalizationCategory> categories)
                    {
                        LocalizationCategories.ReplaceAll(categories);
                    }
                };
            bw.RunWorkerAsync();
        }

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

        private void parseLocalizations(List<LocalizationCategory> categories, Dictionary<string, string> dictionaries)
        {
            var langs = LocalizedString.Languages.Where(x => x != "int");
            foreach (var lang in langs)
            {
                if (dictionaries.ContainsKey(lang))
                {
                    var langLines = Regex.Split(dictionaries[lang], "\r\n|\r|\n");
                    int numBlankLines = 0;
                    for (int i = 3; i < langLines.Length - 2; i++)
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
                        var t = categories.Select(x => x.LocalizedStringsForSection.FirstOrDefault(y => y.key == lineInfo.key)).Where(x => x != null).ToList();
                        LocalizedString ls = t.FirstOrDefault();
                        if (ls != null)
                        {
                            switch (lang)
                            {
                                case "rus":
                                    ls.RUS = lineInfo.text;
                                    break;
                                case "deu":
                                    ls.DEU = lineInfo.text;
                                    break;
                                case "pol":
                                    ls.POL = lineInfo.text;
                                    break;
                                case "fra":
                                    ls.FRA = lineInfo.text;
                                    break;
                                case "esn":
                                    ls.ESN = lineInfo.text;
                                    break;
                            }
                        }
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
        public ObservableCollectionExtended<LocalizationCategory> LocalizationCategories { get; } = new ObservableCollectionExtended<LocalizationCategory>();
        public ICommand SaveLocalizationCommand { get; set; }
        public ICommand CopyLocalizationCommand { get; set; }
        public ICommand LoadLocalizationCommand { get; set; }
        public ICommand SaveTipsLocalizationCommand { get; set; }
        public ICommand LoadLocalizedHelpMenuCommand { get; set; }
        public ICommand SaveLocalizedHelpMenuCommand { get; set; }
        private void LoadCommands()
        {
            SaveLocalizationCommand = new GenericCommand(SaveLocalization, CanSaveLocalization);
            CopyLocalizationCommand = new GenericCommand(CopyLocalization, CanSaveLocalization);
            LoadLocalizationCommand = new GenericCommand(LoadLocalization, CanSaveLocalization);
            SaveTipsLocalizationCommand = new GenericCommand(SaveTipsLocalization, CanSaveLocalization);
            LoadLocalizedHelpMenuCommand = new GenericCommand(LoadLocalizedHelpMenu, CanSaveLocalization);
            SaveLocalizedHelpMenuCommand = new GenericCommand(SaveLocalizedHelpMenu, CanSaveLocalization);
        }

        private void SaveLocalizedHelpMenu()
        {
            string lang = null;
            if (ShowGerman) lang = "deu";
            if (ShowRussian) lang = "rus";
            if (ShowPolish) lang = "pol";
            if (ShowFrench) lang = "fra";
            if (ShowSpanish) lang = "esn";

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
                MessageBox.Show("Saved. Upload this file to github at MassEffectModManagerCore/staticfiles/dynamichelp/latesthelp-localized.xml and create a pull request.");
            }
        }

        private void LoadLocalizedHelpMenu()
        {
            string lang = null;
            if (ShowGerman) lang = "deu";
            if (ShowRussian) lang = "rus";
            if (ShowPolish) lang = "pol";
            if (ShowFrench) lang = "fra";
            if (ShowSpanish) lang = "esn";
            localizedEditor.Text = "";
            if (dynamicHelpLocalizations.TryGetValue(lang, out var text))
            {
                localizedEditor.Text = text;
            }
        }

        private void SaveTipsLocalization()
        {
            string lang = null;
            if (ShowGerman) lang = "deu";
            if (ShowRussian) lang = "rus";
            if (ShowPolish) lang = "pol";
            if (ShowFrench) lang = "fra";
            if (ShowSpanish) lang = "esn";

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
                    var str = LocalizedTips[i].GetString(lang);
                    if (string.IsNullOrWhiteSpace(str)) str = "NULL";
                    sb.AppendLine(str);
                }
                File.WriteAllText(saveFileDialog.FileName, sb.ToString());
                MessageBox.Show("Saved. Send this file to Mgamerz to upload into the ME3Tweaks tips database.");
            }
        }

        private void CopyLocalization()
        {
            string lang = null;
            if (ShowGerman) lang = "deu";
            if (ShowRussian) lang = "rus";
            if (ShowPolish) lang = "pol";
            if (ShowFrench) lang = "fra";
            if (ShowSpanish) lang = "esn";

            var sb = CreateXamlDocument();
            Clipboard.SetText(sb);
            MessageBox.Show($"The contents for the {lang}.xaml file have been copied to your clipboard. Paste into the github editor to update it, then submit a pull request. Once the request is approved, it will be reflected in this program's interface.");
        }

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
                var basename = Path.GetFileNameWithoutExtension(fname);
                if (!LocalizedString.Languages.Contains(basename, StringComparer.InvariantCultureIgnoreCase))
                {
                    MessageBox.Show("Filename must be XXX.xaml, with XXX being your language code. The file selected does not match a supported language.");
                    return;
                }

                //Wipe existing strings for that lang
                foreach (var cat in LocalizationCategories)
                {
                    foreach (var ls in cat.LocalizedStringsForSection)
                    {
                        switch (basename)
                        {
                            case "rus":
                                ls.RUS = null;
                                break;
                            case "deu":
                                ls.DEU = null;
                                break;
                            case "pol":
                                ls.POL = null;
                                break;
                            case "fra":
                                ls.FRA = null;
                                break;
                            case "esn":
                                ls.ESN = null;
                                break;
                        }
                    }
                }

                //Load lang from file
                var dict = new Dictionary<string, string>();
                dict[basename] = File.ReadAllText(fname);
                parseLocalizations(LocalizationCategories.ToList(), dict);
                MessageBox.Show("Loaded localization for " + basename + ".");
            }
        }

        private bool CanSaveLocalization()
        {
            if (!LocalizationCategories.Any()) return false;
            int numChecked = 0;
            if (ShowGerman) numChecked++;
            if (ShowRussian) numChecked++;
            if (ShowPolish) numChecked++;
            if (ShowFrench) numChecked++;
            if (ShowSpanish) numChecked++;
            if (numChecked == 1) return true;
            return false;
        }

        private string CreateXamlDocument()
        {
            string lang = null;
            if (ShowGerman) lang = "deu";
            if (ShowRussian) lang = "rus";
            if (ShowPolish) lang = "pol";
            if (ShowFrench) lang = "fra";
            if (ShowSpanish) lang = "esn";

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
                    string line = $"    <system:String x:Key=\"{str.key}\"";
                    if (str.preservewhitespace)
                    {
                        line += " xml:space=\"preserve\"";
                    }
                    line += $">{str.GetString(lang)}</system:String>";
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

        private void SaveLocalization()
        {
            string lang = null;
            if (ShowGerman) lang = "deu";
            if (ShowRussian) lang = "rus";
            if (ShowPolish) lang = "pol";
            if (ShowFrench) lang = "fra";
            if (ShowSpanish) lang = "esn";

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
        public class LocalizationCategory : INotifyPropertyChanged
        {
            public string CategoryName { get; set; }
            public event PropertyChangedEventHandler PropertyChanged;
            public ObservableCollectionExtended<LocalizedString> LocalizedStringsForSection { get; } = new ObservableCollectionExtended<LocalizedString>();
        }

        public class LocalizedString : INotifyPropertyChanged
        {
            public static string[] Languages = { "int", "deu", "rus", "pol", "fra", "esn" };
            public string key { get; set; }
            public bool preservewhitespace { get; set; }
            public string notes { get; set; }

            public string INT { get; set; }
            public string DEU { get; set; }
            public string RUS { get; set; }
            public string POL { get; set; }
            public string FRA { get; set; }
            public string ESN { get; set; }

            public string GetString(string lang)
            {
                lang = lang.ToLower();
                switch (lang)
                {
                    case "int":
                        return INT;
                    case "deu":
                        return DEU;
                    case "rus":
                        return RUS;
                    case "pol":
                        return POL;
                    case "fra":
                        return FRA;
                    case "esn":
                        return ESN;
                    default:
                        throw new NotImplementedException("Langauge not supported by this tool: " + lang);
                }
            }


            public event PropertyChangedEventHandler PropertyChanged;

        }

        public event PropertyChangedEventHandler PropertyChanged;

        public LocalizedString SelectedDataGridItem { get; set; }

        public string SearchText { get; set; } = "";
        private void Find_Clicked(object sender, RoutedEventArgs e)
        {

            int indexOfCurrentCategory = SelectedCategory != null ? LocalizationCategories.IndexOf(SelectedCategory) : 0;
            Debug.WriteLine("Current cat index: " + indexOfCurrentCategory);

            int numCategories = LocalizationCategories.Count(); //might need to +1 this
            string searchTerm = SearchText.ToLower();
            if (string.IsNullOrEmpty(searchTerm)) return;
            LocalizedString itemToHighlight = null;
            LocalizationCategory catToHighlight = null;
            for (int i = 0; i < numCategories; i++)
            {
                bool found = false;
                LocalizationCategory cat = LocalizationCategories[(i + indexOfCurrentCategory) % LocalizationCategories.Count()];
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
                    var ls = cat.LocalizedStringsForSection[(j + startSearchIndex) % cat.LocalizedStringsForSection.Count];

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
                    if (ls.INT.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //German
                    if (ShowGerman && ls.DEU != null && ls.DEU.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //Russian
                    if (ShowRussian && ls.RUS != null && ls.RUS.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //Polish
                    if (ShowPolish && ls.POL != null && ls.POL.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //French
                    if (ShowFrench && ls.FRA != null && ls.FRA.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //Spanish
                    if (ShowSpanish && ls.ESN != null && ls.ESN.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
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
    }
}