using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                foreach(var v in localizations)
                {
                    sb.AppendLine("\t<system:string x:Key=\""+v.Value+"\">"+v.Key+"</system:string>");
                }
                StringsTextBox.Text = sb.ToString();
            }
            catch (Exception ex)
            {

            }
        }
    }
}
