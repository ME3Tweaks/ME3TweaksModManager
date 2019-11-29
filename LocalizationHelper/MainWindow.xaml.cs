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
                var menuitems = doc.Descendants("MenuItem").ToList();
                Dictionary<string, string> localizations = new Dictionary<string, string>();

                foreach (var item in menuitems)
                {
                    string header = (string)item.Attribute("Header");
                    string tooltip = (string)item.Attribute("ToolTip");

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
                }

                ResultTextBox.Text = doc.ToString();
                StringBuilder sb = new StringBuilder();
                foreach (var v in localizations)
                {
                    sb.AppendLine("\t<system:string x:Key=\"" + v.Value + "\">" + v.Key + "</system:string>");
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

            var file = Path.Combine(M3folder, @"modmanager\usercontrols\ExternalToolLauncher.xaml.cs");

            var regex = "([$@]*(\".+?\"))";
            Regex r = new Regex(regex);
            var filetext = File.ReadAllText(file);
            var matches = r.Matches(filetext);
            var strings = new List<string>();
            foreach (var match in matches)
            {
                var str = match.ToString();
                if (str.StartsWith("@") || str.StartsWith("$@")) continue; //skip literals
                var newStr = match.ToString().TrimStart('$').Trim('"');
                if (newStr.Length > 1)
                {
                    Debug.WriteLine($"    <system:String x:Key=\"string_\">{newStr}</system:String>");
                }
            }

        }
    }
}
