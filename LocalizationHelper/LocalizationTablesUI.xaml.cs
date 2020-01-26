using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
using Octokit;
using Application = Octokit.Application;

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
        public string SelectedBranch { get; set; }
        private void LoadLocalizations(string branch = null)
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += async (x, y) =>
            {
                if (!LocalizationBranches.Any())
                {
                    var ghclient = new GitHubClient(new ProductHeaderValue(@"ME3TweaksModManager"));
                    try
                    {
                        var branches = ghclient.Repository.Branch.GetAll("ME3Tweaks", "ME3TweaksModManager").Result;
                        var locbranches = branches.Where(x => x.Name.Contains("localization"));
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
                        if (ls.INT == null) Debugger.Break();
                        cat.LocalizedStringsForSection.Add(ls);
                    }

                    if (cat != null)
                    {
                        categories.Add(cat);
                    }

                    parseLocalizations(categories, dictionaries);
                    y.Result = categories;
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(delegate { MessageBox.Show("Could not find any branches on ME3TweaksModManager repo containing name 'localization'"); });
                }
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
                }
            }
        }

        private void parseLocalizations(List<LocalizationCategory> categories, Dictionary<string, string> dictionaries)
        {
            var langs = LocalizedString.Languages.Where(x => x != "int");
            foreach (var lang in langs)
            {
                var langLines = Regex.Split(dictionaries[lang], "\r\n|\r|\n");
                int numBlankLines = 0;
                LocalizationCategory cat = null;
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
                        if (numBlankLines > 0 || cat == null)
                        {
                            cat = categories.FirstOrDefault(x => x.CategoryName == line);
                            if (cat == null)
                            {
                                Debugger.Break();
                            }
                        }

                        //notes for previous item?

                        //We don't care in localizations about this, they just have to exist.
                        continue;
                    }

                    numBlankLines = 0;
                    var lineInfo = extractInfo(line);
                    LocalizedString ls = cat.LocalizedStringsForSection.FirstOrDefault(x => x.key == lineInfo.key);
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
        private void LoadCommands()
        {
            SaveLocalizationCommand = new GenericCommand(SaveLocalization, CanSaveLocalization);
        }

        private bool CanSaveLocalization()
        {
            int numChecked = 0;
            if (ShowGerman) numChecked++;
            if (ShowRussian) numChecked++;
            if (ShowPolish) numChecked++;
            if (ShowFrench) numChecked++;
            if (ShowSpanish) numChecked++;
            if (numChecked == 1) return true;
            return false;
        }

        private void SaveLocalization()
        {
            //throw new NotImplementedException();
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
            sb.AppendLine("\t\t\t\t\txmlns:system=\"clr-namespace:System;assembly=System.Runtime\"");

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
            Debug.WriteLine(sb.ToString());
            Clipboard.SetText(sb.ToString());
            MessageBox.Show($"The contents for the {lang}.xaml file have been copied to your clipboard. Paste into the github editor to update it, then submit a pull request. Once the request is approved, it will be reflected in this program's interface.");
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

        public string SearchText { get; set; }
        private void Find_Clicked(object sender, RoutedEventArgs e)
        {

            int indexOfCurrentCategory = SelectedCategory != null ? LocalizationCategories.IndexOf(SelectedCategory) : 0;
            Debug.WriteLine("Current cat index: " + indexOfCurrentCategory);

            int numCategories = LocalizationCategories.Count(); //might need to +1 this
            string searchTerm = SearchText.ToLower();
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
                    if (ShowGerman && ls.DEU.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //Russian
                    if (ShowRussian && ls.RUS.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //Polish
                    if (ShowPolish && ls.POL.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //French
                    if (ShowFrench && ls.FRA.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //found
                        found = true;
                        itemToHighlight = ls;
                        catToHighlight = cat;
                        break;
                    }

                    //Spanish
                    if (ShowSpanish && ls.ESN.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
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
    }
}