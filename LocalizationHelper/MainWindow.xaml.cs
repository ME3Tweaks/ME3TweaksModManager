using System;
using System.Collections.Generic;
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
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Convert_Clicked(object sender, RoutedEventArgs e)
        {
            try
            {
                XDocument doc = XDocument.Parse(SourceTextbox.Text);
                var menuitems = doc.Descendants().ToList();
                Dictionary<string, string> localizations = new Dictionary<string, string>();

                foreach (var item in menuitems)
                {
                    string header = (string)item.Attribute("Header");
                    string tooltip = (string)item.Attribute("ToolTip");
                    string content = (string)item.Attribute("Content");
                    string text = (string)item.Attribute("Text");

                    if (header != null && !header.StartsWith("{"))
                    {
                        localizations[header] = $"string_{header.Replace(" ", "")}";
                        item.Attribute("Header").Value = $"{{DynamicResource {localizations[header]}}}";
                    }

                    if (tooltip != null && !tooltip.StartsWith("{"))
                    {
                        localizations[tooltip] = $"string_{tooltip.Replace(" ", "")}";
                        item.Attribute("ToolTip").Value = $"{{DynamicResource {localizations[tooltip]}}}";
                    }

                    if (content != null && !content.StartsWith("{"))
                    {
                        localizations[content] = $"string_{content.Replace(" ", "")}";
                        item.Attribute("Content").Value = $"{{DynamicResource {localizations[content]}}}";
                    }

                    if (text != null && !text.StartsWith("{"))
                    {
                        localizations[text] = $"string_{text.Replace(" ", "")}";
                        item.Attribute("Text").Value = $"{{DynamicResource {localizations[text]}}}";
                    }
                }

                ResultTextBox.Text = doc.ToString();
                StringBuilder sb = new StringBuilder();
                foreach (var v in localizations)
                {
                    sb.AppendLine("\t<system:String x:Key=\"" + v.Value.Substring(0, "string_".Length) + v.Value.Substring("string_".Length, 1).ToLower() + v.Value.Substring("string_".Length + 1) + "\">" + v.Key + "</system:string>");
                }
                StringsTextBox.Text = sb.ToString();
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
                Debug.WriteLine(keyStr);
            }
            //Write end of .cs file lines
            m3llines.Add("\t}");
            m3llines.Add("}");

            File.WriteAllLines(m3lFile, m3llines); //write back updated file

            //Update all of the other xaml files
            var localizationMapping = Directory.GetFiles(localizationsFolder, "*.xaml").Where(y => Path.GetFileName(y) != "int.xaml").ToDictionary(y => y, y => File.ReadAllLines(y).ToList());
            var intlines = File.ReadAllLines(intfile);
            for (int i = 3; i < intlines.Length - 1; i++) //-1 to avoid resource dictionary line
            {
                var line = intlines[i];
                if (!line.StartsWith("    <system:String ")) continue;
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
                        var lstrInfo = extractInfo(lline);
                        if (strInfo.preserveWhitespace != lstrInfo.preserveWhitespace)
                        {
                            Debug.WriteLine(lstrInfo.key + " " + l.Key + " whitespace is wrong for key " + l);
                        }
                        if (strInfo.key != lstrInfo.key)
                        {
                            Debug.WriteLine(lstrInfo.key + " " + l.Key + " mismatches int key! should be " + strInfo.key);
                        }

                    }
                }
            }

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

        private void PullStrings_Clicked(object sender, RoutedEventArgs e)
        {
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var M3folder = Path.Combine(solutionroot, "MassEffectModManagerCore");

            var file = Path.Combine(M3folder, @"modmanager\usercontrols\InstallationInformation.xaml.cs");

            var regex = "([$@]*(\".+?\"))";
            Regex r = new Regex(regex);
            var filetext = File.ReadAllText(file);
            var matches = r.Matches(filetext);
            var strings = new List<string>();
            HashSet<string> s = new HashSet<string>();
            foreach (var match in matches)
            {
                var str = match.ToString();
                if (str.StartsWith("@") || str.StartsWith("$@")) continue; //skip literals
                var strname = "string_";
                if (str.StartsWith("$")) strname = "string_interp_";
                var newStr = match.ToString().TrimStart('$').Trim('"');
                if (newStr.Length > 1)
                {
                    s.Add($"    <system:String x:Key=\"{strname}\">{newStr}</system:String>");
                }
            }
            foreach (var str in s)
            {
                Debug.WriteLine(str);
            }

        }

        private void PushLocalizedStrings_Clicked(object sender, RoutedEventArgs e)
        {
            var text = SourceTextbox.Text;
            text = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"  xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:system=\"clr-namespace:System;assembly=System.Runtime\" >" + text + "</ResourceDictionary>";
            XDocument xdoc = XDocument.Parse(text);
            XNamespace system = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var lstrings = xdoc.Root.Descendants(system + "String");
            foreach (var str in lstrings)
            {
                Debug.WriteLine(str.Value);
            }
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var M3folder = Path.Combine(solutionroot, "MassEffectModManagerCore");

            var file = Path.Combine(M3folder, @"modmanager\usercontrols\InstallationInformation.xaml.cs");

            var regex = "([$@]*(\".+?\"))";
            Regex r = new Regex(regex);
            StringBuilder sb = new StringBuilder();

            var lines = File.ReadAllLines(file);
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
                        while (pos < str.Length - 1)
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
                                substitutions.Add(str.Substring(openbracepos + 1, pos - (openbracepos + +1)));
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
            var sourceStringsXaml = SourceTextbox.Text;
            sourceStringsXaml = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"  xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:system=\"clr-namespace:System;assembly=System.Runtime\" >" + sourceStringsXaml + "</ResourceDictionary>";
            XDocument xdoc = XDocument.Parse(sourceStringsXaml);
            XNamespace system = "clr-namespace:System;assembly=System.Runtime";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var lstrings = xdoc.Root.Descendants(system + "String");
            var solutionroot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName).FullName).FullName).FullName).FullName;
            var M3folder = Path.Combine(solutionroot, "MassEffectModManagerCore");

            var file = Path.Combine(M3folder, @"modmanager\usercontrols\InstallationInformation.xaml");
            string[] attributes = { "Header", "ToolTip", "Content", "Text" };
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
    }
}
