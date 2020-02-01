using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Linq;
using Path = System.IO.Path;

namespace LocalizationHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<string> SourceFiles { get; } = new ObservableCollectionExtended<string>();
        public string SelectedFile { get; set; }
        public MainWindow()
        {
            //var text = File.ReadAllLines(@"C:\users\mgame\desktop\unrealkeys");
            //foreach (var line in text)
            //{
            //    var unrealStr = line.Substring(0,line.IndexOf(" "));

            //    Debug.WriteLine($"case \"{unrealStr}\":\n\treturn \"{unrealStr}\";");
            //}
            //Environment.Exit(0);
            DataContext = this;
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var modmanagerroot = Path.Combine(solutionroot, "MassEffectModManagerCore");
            var rootLen = modmanagerroot.Length + 1;
            //localizable folders
            var usercontrols = Path.Combine(modmanagerroot, "modmanager", "usercontrols");
            var windows = Path.Combine(modmanagerroot, "modmanager", "windows");
            var me3tweaks = Path.Combine(modmanagerroot, "modmanager", "me3tweaks");
            var nexus = Path.Combine(modmanagerroot, "modmanager", "nexusmodsintegration");
            var objects = Path.Combine(modmanagerroot, "modmanager", "objects");

            List<string> files = new List<string>();
            files.AddRange(Directory.EnumerateFiles(usercontrols, "*.xaml*", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
            files.AddRange(Directory.EnumerateFiles(windows, "*.xaml*", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
            files.AddRange(Directory.EnumerateFiles(me3tweaks, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
            files.AddRange(Directory.EnumerateFiles(nexus, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));
            files.AddRange(Directory.EnumerateFiles(objects, "*.cs", SearchOption.AllDirectories).Select(x => x.Substring(rootLen)));

            //these files are not localized
            files.Remove(Path.Combine(modmanagerroot, "modmanager", "me3tweaks", "LogCollector.cs").Substring(rootLen));
            files.Remove(Path.Combine(modmanagerroot, "modmanager", "me3tweaks", "JPatch.cs").Substring(rootLen));
            files.Remove(Path.Combine(modmanagerroot, "modmanager", "me3tweaks", "DynamicHelp.cs").Substring(rootLen));
            files.Remove(Path.Combine(modmanagerroot, "modmanager", "usercontrols", "AboutPanel.xaml.cs").Substring(rootLen));
            files.Remove(Path.Combine(modmanagerroot, "modmanager", "usercontrols", "AboutPanel.xaml").Substring(rootLen));
            files.Add("MainWindow.xaml");
            files.Add("MainWindow.xaml.cs");
            files.Sort();
            SourceFiles.ReplaceAll(files);
            InitializeComponent();
        }

        public bool SelectedCS { get; set; }
        public bool SelectedXAML { get; set; }

        public void OnSelectedFileChanged()
        {
            SelectedCS = false;
            SelectedXAML = false;
            if (SelectedFile == null) return;
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var modmanagerroot = Path.Combine(solutionroot, "MassEffectModManagerCore");

            var selectedFilePath = Path.Combine(modmanagerroot, SelectedFile);
            if (File.Exists(selectedFilePath))
            {
                ResultTextBox.Text = "";
                StringsTextBox.Text = "";
                Debug.WriteLine("Loading " + selectedFilePath);
                if (selectedFilePath.EndsWith(".cs"))
                {
                    SelectedCS = true;
                    PullStringsFromCS(selectedFilePath, null);
                }

                if (selectedFilePath.EndsWith(".xaml"))
                {
                    SelectedXAML = true;
                    PullStringsFromXaml(selectedFilePath, null);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void PullStringsFromXaml(object sender, RoutedEventArgs e)
        {
            try
            {
                XDocument doc = XDocument.Parse(File.ReadAllText(sender as string));
                var menuitems = doc.Descendants().ToList();
                Dictionary<string, string> localizations = new Dictionary<string, string>();

                foreach (var item in menuitems)
                {
                    string header = (string)item.Attribute("Header");
                    string tooltip = (string)item.Attribute("ToolTip");
                    string content = (string)item.Attribute("Content");
                    string text = (string)item.Attribute("Text");
                    string watermark = (string)item.Attribute("Watermark");

                    if (header != null && !header.StartsWith("{"))
                    {
                        localizations[header] = $"string_{toCamelCase(header)}";
                        //item.Attribute("Header").Value = $"{{DynamicResource {localizations[header]}}}";
                    }

                    if (tooltip != null && !tooltip.StartsWith("{"))
                    {
                        localizations[tooltip] = $"string_tooltip_{toCamelCase(tooltip)}";
                        //item.Attribute("ToolTip").Value = $"{{DynamicResource {localizations[tooltip]}}}";
                    }

                    if (content != null && !content.StartsWith("{") && content.Length > 1 && !content.StartsWith("/images/"))
                    {
                        localizations[content] = $"string_{toCamelCase(content)}";
                        //item.Attribute("Content").Value = $"{{DynamicResource {localizations[content]}}}";
                    }

                    if (watermark != null && !watermark.StartsWith("{") && watermark.Length > 1 && !long.TryParse(watermark, out var _))
                    {
                        localizations[watermark] = $"string_{toCamelCase(watermark)}";
                       //item.Attribute("Watermark").Value = $"{{DynamicResource {localizations[watermark]}}}";
                    }

                    if (text != null && !text.StartsWith("{") && text.Length > 1
                        && text != "BioGame"
                        && text != "BioParty"
                        && text != "BioEngine" && text != "DLC_MOD_")
                    {
                        localizations[text] = $"string_{toCamelCase(text)}";
                        //item.Attribute("Text").Value = $"{{DynamicResource {localizations[text]}}}";
                    }
                }

                //ResultTextBox.Text = doc.ToString();
                StringBuilder sb = new StringBuilder();
                foreach (var v in localizations)
                {
                    sb.AppendLine("\t<system:String x:Key=\"" + v.Value.Substring(0, "string_".Length) + v.Value.Substring("string_".Length, 1).ToLower() + v.Value.Substring("string_".Length + 1) + "\">" + v.Key + "</system:String>");
                }
                StringsTextBox.Text = sb.ToString();
                if (string.IsNullOrEmpty(sb.ToString()))
                {
                    StringsTextBox.Text = "No strings needing localized in " + SelectedFile;
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void Synchronize_Clicked(object sender, RoutedEventArgs e)
        {
            //get out of project in debug mod
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var localizationsFolder = Path.Combine(solutionroot, "MassEffectModManagerCore", "modmanager", "localizations");
            var m3lFile = Path.Combine(localizationsFolder, "M3L.cs");
            var m3lTemplateFile = Path.Combine(localizationsFolder, "M3L_Template.txt");
            var intfile = Path.Combine(localizationsFolder, "int.xaml");

            var m3llines = File.ReadAllLines(m3lTemplateFile).ToList();

            var doc = XDocument.Load(intfile);
            XNamespace xnamespace = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var keys = doc.Descendants(xnamespace + "String");
            Debug.WriteLine(keys.Count());
            foreach (var key in keys)
            {
                var keyStr = key.Attribute(x + "Key").Value;
                m3llines.Add($"\t\tpublic static readonly string {keyStr} = \"{keyStr}\";");
                //Debug.WriteLine(keyStr);
            }
            //Write end of .cs file lines
            m3llines.Add("\t}");
            m3llines.Add("}");

            File.WriteAllLines(m3lFile, m3llines); //write back updated file

            //return;
            //Update all of the other xaml files
            return; //skip other langauges as it's now handled by localizer tool
            /*
            var localizationMapping = Directory.GetFiles(localizationsFolder, "*.xaml").Where(y => Path.GetFileName(y) != "int.xaml").ToDictionary(y => y, y => File.ReadAllLines(y).ToList());
            var intlines = File.ReadAllLines(intfile);
            for (int i = 3; i < intlines.Length - 1; i++) //-1 to avoid resource dictionary line
            {
                var line = intlines[i];
                if (!line.Trim().StartsWith("<system:String")) continue;
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line == "</ResourceDictionary>") continue; //EOF

                (bool preserveWhitespace, string key) strInfo = extractInfo(line);
                foreach (var l in localizationMapping)
                {
                    if (l.Value.Count > i)
                    {
                        var lline = l.Value[i];
                        if (string.IsNullOrWhiteSpace(lline)) continue;
                        if (lline == "</ResourceDictionary>") continue; //EOF
                        if (!lline.Trim().StartsWith("<system:String"))
                        {
                            Debug.WriteLine("Desync in " + Path.GetFileName(l.Key) + " on line " + i + ": " + lline);
                            continue;
                        }
                        var lstrInfo = extractInfo(lline);
                        if (strInfo.preserveWhitespace != lstrInfo.preserveWhitespace)
                        {
                            Debug.WriteLine(lstrInfo.key + " " + Path.GetFileName(l.Key) + " whitespace is wrong for key " + lstrInfo.key);
                        }
                        if (strInfo.key != lstrInfo.key)
                        {
                            Debug.WriteLine(lstrInfo.key + " " + l.Key + " mismatches int key! should be " + strInfo.key);
                        }

                    }
                }
            }*/

        }

        private (bool preserveWhitespace, string key) extractInfo(string line)
        {
            var closingTagIndex = line.IndexOf(">");
            var strInfo = line.Substring(0, closingTagIndex).Trim();
            bool preserveWhitespace = strInfo.Contains("xml:space=\"preserve\"");
            int keyPos = strInfo.IndexOf("x:Key=\"");
            string keyVal = strInfo.Substring(keyPos + "x:Key=\"".Length);
            keyVal = keyVal.Substring(0, keyVal.IndexOf("\""));
            return (preserveWhitespace, keyVal);
        }

        private void PullStringsFromCS(object sender, RoutedEventArgs e)
        {
            var regex = "([$@]*(\".+?\"))";
            Regex r = new Regex(regex);
            var filelines = File.ReadAllLines(sender as string);
            HashSet<string> s = new HashSet<string>();
            HashSet<string> origStrForSubsOnly = new HashSet<string>();
            bool sectionIsLocalizable = true;
            for (int x = 0; x < filelines.Length; x++)
            {
                var line = filelines[x];
                if (line.Contains("do not localize", StringComparison.InvariantCultureIgnoreCase)) continue; //ignore this line.
                if (line.Contains("Localizable(true)", StringComparison.InvariantCultureIgnoreCase))
                {
                    sectionIsLocalizable = true;
                    continue; //ignore this line.
                }

                if (line.Contains("Localizable(false)", StringComparison.InvariantCultureIgnoreCase))
                {
                    sectionIsLocalizable = false;
                    continue; //ignore this line.
                }

                if (!sectionIsLocalizable && !line.Contains("//force localize"))
                {
                    continue;
                }

                if (line.Contains("[DebuggerDisplay(")) continue; //skip these lines
                var commentIndex = line.IndexOf("//");
                var matches = r.Matches(line);
                foreach (var match in matches)
                {
                    bool xmlPreserve = false;
                    var matchIndex = line.IndexOf(match.ToString());
                    if (commentIndex >= 0 && matchIndex > commentIndex) continue; //this is a comment
                    var str = match.ToString();
                    if (str.StartsWith("@") || str.StartsWith("$@")) continue; //skip literals
                    var strname = "string_";
                    if (str.StartsWith("$")) strname = "string_interp_";
                    var newStr = match.ToString().TrimStart('$').Trim('"');
                    if (newStr.Length > 1)
                    {
                        if (newStr.Contains("\\n")) xmlPreserve = true;

                        strname += toCamelCase(newStr);

                        //Substitutions
                        int pos = 0;
                        int openbracepos = -1;
                        List<string> substitutions = new List<string>();
                        while (pos < newStr.Length)
                        {
                            if (openbracepos == -1)
                            {
                                if (newStr[pos] == '{')
                                {
                                    openbracepos = pos;
                                    continue;
                                }
                            }
                            else if (newStr[pos] == '}')
                            {
                                //closing!
                                substitutions.Add(newStr.Substring(openbracepos + 1, pos - (openbracepos + 1)));
                                openbracepos = -1;
                            }

                            //Debug.Write(newStr[pos]);
                            //Debug.Flush();
                            pos++;
                        }

                        int num = 0;
                        string comment = "";
                        string subbedStr = newStr;
                        foreach (var substitution in substitutions)
                        {
                            subbedStr = subbedStr.Replace(substitution, num.ToString()); //already in { }
                            comment += " " + num + "=" + substitution;
                            num++;
                        }

                        string commentStr = "";
                        if (comment.Length > 0) commentStr = "<!--" + comment + " -->";
                        Debug.WriteLine(x + "\t\t" + subbedStr);
                        s.Add($"    <system:String{(xmlPreserve ? " xml:space=\"preserve\"" : "")} x:Key=\"{strname}\">{subbedStr}</system:String> " + commentStr);
                        if (substitutions.Count > 0)
                        {
                            origStrForSubsOnly.Add($"    <system:String x:Key=\"{strname}\">{newStr}</system:String>");
                        }
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            foreach (var str in s)
            {
                sb.AppendLine(str);
            }

            if (origStrForSubsOnly.Count > 0)
            {
                sb.AppendLine("<!-- The follow items are only for letting this localizer replace the correct strings! Remove them when done and make sure keys are identical to the stripped versions-->");
            }
            foreach (var str in origStrForSubsOnly)
            {
                //interps
                sb.AppendLine(str);
            }
            StringsTextBox.Text = sb.ToString();
            if (string.IsNullOrEmpty(sb.ToString()))
            {
                StringsTextBox.Text = "No strings needing localized in " + SelectedFile;
            }
            //Debug.WriteLine("<!-- Subs only -->");


        }

        private string toCamelCase(string str)
        {
            var words = str.Split();
            var res = "";
            bool first = true;
            foreach (var word in words)
            {
                var cleanedWord = word.Replace(".", "");
                cleanedWord = cleanedWord.Replace("?", "Question");
                cleanedWord = cleanedWord.Replace("(", "");
                cleanedWord = cleanedWord.Replace(")", "");
                cleanedWord = cleanedWord.Replace(":", "");
                cleanedWord = cleanedWord.Replace("/", "");
                cleanedWord = cleanedWord.Replace("\\", "");
                cleanedWord = cleanedWord.Replace("{", "");
                cleanedWord = cleanedWord.Replace("}", "");
                cleanedWord = cleanedWord.Replace("-", "");
                cleanedWord = cleanedWord.Replace(",", "");
                if (first)
                {
                    res += caseFirst(cleanedWord, false);
                    first = false;
                }
                else
                {
                    res += caseFirst(cleanedWord, true);
                }
            }

            return res;
        }

        static string caseFirst(string s, bool upper)
        {
            // Check for empty string.
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            // Return char and concat substring.
            return (upper ? char.ToUpper(s[0]) : char.ToLower(s[0])) + s.Substring(1);
        }

        private void PushCSStrings_Clicked(object sender, RoutedEventArgs e)
        {
            var text = StringsTextBox.Text;
            text = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"  xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:system=\"clr-namespace:System;assembly=System.Runtime\" >" + text + "</ResourceDictionary>";
            XDocument xdoc = XDocument.Parse(text);
            XNamespace system = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var lstrings = xdoc.Root.Descendants(system + "String").ToList();
            foreach (var str in lstrings)
            {
                Debug.WriteLine(str.Value);
            }
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var M3folder = Path.Combine(solutionroot, "MassEffectModManagerCore");


            var regex = "([$@]*(\".+?\"))";
            Regex r = new Regex(regex);
            StringBuilder sb = new StringBuilder();

            var lines = File.ReadAllLines(Path.Combine(M3folder, SelectedFile));
            foreach (var line in lines)
            {
                var newline = line;
                var matches = r.Matches(line);
                var strings = new List<string>();
                foreach (var match in matches)
                {
                    var str = match.ToString();
                    if (str.StartsWith("@") || str.StartsWith("$@")) continue; //skip literals
                    var strippedStr = str.Trim('$', '"');

                    var localizedMatch = lstrings.FirstOrDefault(x => x.Value == strippedStr);
                    if (localizedMatch != null)
                    {

                        var m3lcodestr = "M3L.GetString(M3L." + localizedMatch.Attribute(x + "Key").Value;

                        int pos = 0;
                        int openbracepos = -1;
                        List<string> substitutions = new List<string>();
                        while (pos < str.Length)
                        {
                            if (openbracepos == -1)
                            {
                                if (str[pos] == '{')
                                {
                                    openbracepos = pos;
                                    continue;
                                }
                            }
                            else if (str[pos] == '}')
                            {
                                //closing!
                                substitutions.Add(str.Substring(openbracepos + 1, pos - (openbracepos + 1)));
                                openbracepos = -1;
                            }
                            pos++;
                        }
                        foreach (var subst in substitutions)
                        {
                            m3lcodestr += ", " + subst;
                        }
                        m3lcodestr += ")";
                        newline = newline.Replace(str, m3lcodestr);
                    }
                }
                sb.AppendLine(newline);
            }

            ResultTextBox.Text = sb.ToString();
        }

        private void PushXamlStrings_Clicked(object sender, RoutedEventArgs e)
        {
            var sourceStringsXaml = StringsTextBox.Text;
            sourceStringsXaml = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"  xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:system=\"clr-namespace:System;assembly=System.Runtime\" >" + sourceStringsXaml + "</ResourceDictionary>";
            XDocument xdoc = XDocument.Parse(sourceStringsXaml);
            XNamespace system = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var lstrings = xdoc.Root.Descendants(system + "String");
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var M3folder = Path.Combine(solutionroot, "MassEffectModManagerCore");

            var file = Path.Combine(M3folder, SelectedFile);
            string[] attributes = { "Header", "ToolTip", "Content", "Text", "Watermark" };
            try
            {
                XDocument doc = XDocument.Load(file);
                var xamlItems = doc.Descendants().ToList();
                Dictionary<string, string> localizations = new Dictionary<string, string>();

                foreach (var item in xamlItems)
                {
                    foreach (var attribute in attributes)
                    {
                        string attributeText = (string)item.Attribute(attribute);

                        if (!string.IsNullOrWhiteSpace(attributeText) && !attributeText.StartsWith("{"))
                        {
                            var matchingStr = lstrings.FirstOrDefault(x => x.Value == attributeText);
                            if (matchingStr != null)
                            {
                                item.Attribute(attribute).Value = "{DynamicResource " + matchingStr.Attribute(x + "Key").Value + "}";
                            }
                        }
                    }
                }
                ResultTextBox.Text = doc.ToString();

            }
            catch (Exception ex)
            {

            }

        }

        private void Check_Clicked(object sender, RoutedEventArgs e)
        {
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var M3folder = Path.Combine(solutionroot, "MassEffectModManagerCore");

            string[] dirs =
                {
                Path.Combine(M3folder, "modmanager", "usercontrols"),
                Path.Combine(M3folder, "modmanager", "objects"),
                Path.Combine(M3folder, "modmanager", "windows")
                };

            int i = 0;
            foreach (var dir in dirs)
            {
                var csFiles = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).ToList();
                if (i == 0)
                {
                    csFiles.Add(Path.Combine(M3folder, "MainWindow.xaml.cs"));
                }
                i++;
                foreach (var csFile in csFiles)
                {
                    Debug.WriteLine($" --------- FILE: {Path.GetFileName(csFile)} --------");
                    var regex = "([$@]*(\".+?\"))";
                    Regex r = new Regex(regex);
                    var filelines = File.ReadAllLines(csFile);
                    HashSet<string> s = new HashSet<string>();

                    foreach (var line in filelines)
                    {
                        var commentIndex = line.IndexOf("//");
                        var matches = r.Matches(line);
                        if (line.Contains("do not localize", StringComparison.InvariantCultureIgnoreCase)) continue; //ignore this line.
                        foreach (var match in matches)
                        {
                            var matchIndex = line.IndexOf(match.ToString());
                            if (commentIndex >= 0 && matchIndex > commentIndex) continue; //this is a comment
                            var str = match.ToString();
                            if (str.StartsWith("@") || str.StartsWith("$@")) continue; //skip literals
                            var strname = "string_";
                            if (str.StartsWith("$")) strname = "string_interp_";
                            var newStr = match.ToString().TrimStart('$').Trim('"');
                            if (newStr.Length > 1)
                            {
                                strname += toCamelCase(newStr);
                                s.Add($"    <system:String x:Key=\"{strname}\">{newStr}</system:String>");
                            }
                        }
                    }

                    foreach (var str in s)
                    {
                        Debug.WriteLine(str);
                    }
                }
            }
        }

        private void CheckXamls_Clicked(object sender, RoutedEventArgs e)
        {
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var M3folder = Path.Combine(solutionroot, "MassEffectModManagerCore");

            string[] dirs =
            {
                Path.Combine(M3folder, "modmanager", "usercontrols"),
                Path.Combine(M3folder, "modmanager", "windows")
            };

            int i = 0;
            foreach (var dir in dirs)
            {
                var xamlFiles = Directory.GetFiles(dir, "*.xaml", SearchOption.AllDirectories).ToList();
                if (i == 0)
                {
                    xamlFiles.Add(Path.Combine(M3folder, "MainWindow.xaml"));
                }

                i++;
                foreach (var xamlFile in xamlFiles)
                {
                    Debug.WriteLine($" --------- FILE: {Path.GetFileName(xamlFile)} --------");
                    if (Path.GetFileName(xamlFile) == "AboutPanel.xaml") continue; //skip this file as it has a lot of non-localizable strings
                    try
                    {
                        XDocument doc = XDocument.Parse(File.ReadAllText(xamlFile));
                        var xamlItems = doc.Descendants().ToList();
                        Dictionary<string, string> localizations = new Dictionary<string, string>();

                        foreach (var item in xamlItems)
                        {
                            string header = (string)item.Attribute("Header");
                            string tooltip = (string)item.Attribute("ToolTip");
                            string content = (string)item.Attribute("Content");
                            string text = (string)item.Attribute("Text");

                            if (header != null && !header.StartsWith("{")
                                               && header != "+"
                                && header != "Deutsch"
                                && header != "English"
                                && header != "French"
                                && header != "Polski"
                                && header != "Russian"
                                               && header != "Reload selected mod" //debug only
                                               && header != "русский"
                                && header != "ME1"
                                && header != "ME2"
                                               && header != "ME3"
                                               )
                            {
                                localizations[header] = $"string_{toCamelCase(header)}";
                                item.Attribute("Header").Value = $"{{DynamicResource {localizations[header]}}}";
                            }

                            if (tooltip != null && !tooltip.StartsWith("{"))
                            {
                                localizations[tooltip] = $"string_tooltip_{toCamelCase(tooltip)}";
                                item.Attribute("ToolTip").Value = $"{{DynamicResource {localizations[tooltip]}}}";
                            }

                            if (content != null && !content.StartsWith("{")
                                                && !content.StartsWith("/images")
                                                && content != "+"
                                                && content != "Deutsch"
                                                && content != "English"
                                                && content != "French"
                                                && content != "Polski"
                                                && content != "Russian"
                                                && content != "ME1"
                                                && content != "ME2"
                                                && content != "ME3")
                            {
                                localizations[content] = $"string_{toCamelCase(content)}";
                                item.Attribute("Content").Value = $"{{DynamicResource {localizations[content]}}}";
                            }

                            if (text != null && !text.StartsWith("{")
                                             && text != "Deutsch"
                                && text != "English"
                                && text != "French"
                                && text != "Polski"
                                && text != "Russian"
                                             && text != "+"
                                             && text != "!"
                                             && text != "DLC_MOD_"
                                             && text != "ME1"
                                && text != "ME2"
                                && text != "ME3"
                                             && text != "BioGame"
                                             && text != "BioParty"
                                             && text != "BioEngine")
                            {
                                localizations[text] = $"string_{toCamelCase(text)}";
                                item.Attribute("Text").Value = $"{{DynamicResource {localizations[text]}}}";
                            }
                        }

                        //ResultTextBox.Text = doc.ToString();
                        StringBuilder sb = new StringBuilder();
                        foreach (var v in localizations)
                        {
                            Debug.WriteLine("\t<system:String x:Key=\"" + v.Value.Substring(0, "string_".Length) + v.Value.Substring("string_".Length, 1).ToLower() + v.Value.Substring("string_".Length + 1) + "\">" + v.Key + "</system:String>");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("EXCEPTION!");
                    }
                }
            }
        }

        private void CheckXmlSpacePreserve_Clicked(object sender, RoutedEventArgs e)
        {
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var localizationsFolder = Path.Combine(solutionroot, "MassEffectModManagerCore", "modmanager", "localizations");
            var m3lFile = Path.Combine(localizationsFolder, "M3L.cs");
            var m3lTemplateFile = Path.Combine(localizationsFolder, "M3L_Template.txt");
            var intfile = Path.Combine(localizationsFolder, "int.xaml");

            var m3llines = File.ReadAllLines(m3lTemplateFile).ToList();

            var doc = XDocument.Load(intfile);
            XNamespace xnamespace = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            XNamespace xml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var keys = doc.Descendants(xnamespace + "String");
            foreach (var key in keys)
            {
                if (key.Value.Contains(@"\n"))
                {
                    //check for preserve space
                    var preserveSpace = key.Attribute(XNamespace.Xml + "space");
                    if (preserveSpace == null || preserveSpace.Value != "preserve")
                    {
                        Debug.WriteLine(key.Value);
                    }
                }
            }
        }
    }
}
