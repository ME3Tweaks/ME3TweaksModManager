using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ModDescEditor.xaml
    /// </summary>
    public partial class ModDescEditor : Window, INotifyPropertyChanged
    {
        public Mod EditingMod { get; private set; }

        public ModDescEditor(Mod selectedMod)
        {
            DataContext = this;
            EditingMod = new Mod(selectedMod.ModDescPath, selectedMod.Game); //RELOAD MOD TO CREATE NEW OBJECT
            InitializeComponent();
            metadataEditor_control.EditingMod = EditingMod;
            alternateDlcEditor_control.EditingMod = EditingMod;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        // this should move into the control
        private void SerializeData_Click(object sender, RoutedEventArgs e)
        {
            string str = "altdlc = (";
            bool first = true;
            foreach (var v in alternateDlcEditor_control.AlternateDLCs)
            {
                string subItem = "";
                if (first)
                {
                    first = false;
                    subItem += "(";
                }
                else
                {
                    subItem += ",(";
                }

                bool subFirst = true;
                foreach (var i in v.ParameterMap)
                {
                    if (subFirst)
                        subFirst = false;
                    else
                    {
                        subItem += ",";
                    }
                    subItem += $"{i.Key}=";
                    if (i.Value.Contains(" "))
                    {
                        subItem += $"\"{i.Value}\"";
                    }
                    else
                    {
                        subItem += i.Value;
                    }
                }


                subItem += ")";

                str += subItem;
            }
            str += ")";
            Clipboard.SetText(str);
        }
    }
}
