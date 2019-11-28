using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using MassEffectIniModder.classes;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for ME1IniModder.xaml
    /// </summary>
    public partial class ME1IniModder : Window, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<IniPropertyMaster> BioEngineEntries { get; } = new ObservableCollectionExtended<IniPropertyMaster>();
        public ObservableCollectionExtended<IniPropertyMaster> BioGameEntries { get; } = new ObservableCollectionExtended<IniPropertyMaster>();
        public ObservableCollectionExtended<IniPropertyMaster> BioPartyEntries { get; } = new ObservableCollectionExtended<IniPropertyMaster>();
        public ME1IniModder()
        {

            DataContext = this;
            InitializeComponent();

            string configFileFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\Config";
            if (Directory.Exists(configFileFolder))
            {
                Dictionary<string, ObservableCollectionExtended<IniPropertyMaster>> loadingMap = new Dictionary<string, ObservableCollectionExtended<IniPropertyMaster>>();
                loadingMap["BioEngine.xml"] = BioEngineEntries;
                loadingMap["BioGame.xml"] = BioGameEntries;
                loadingMap["BioParty.xml"] = BioPartyEntries;


                foreach (var kp in loadingMap)
                {
                    XElement rootElement = XElement.Parse(GetPropertyMap(kp.Key));

                    var linqlist = (from e in rootElement.Elements("Section")
                                    select new IniSection
                                    {
                                        SectionName = (string)e.Attribute("name"),
                                        BoolProperties = e.Elements("boolproperty").Select(f => new IniPropertyBool
                                        {
                                            CanAutoReset = f.Attribute("canautoreset") != null ? (bool)f.Attribute("canautoreset") : true,
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value

                                        }).ToList(),
                                        IntProperties = e.Elements("intproperty").Select(f => new IniPropertyInt
                                        {
                                            CanAutoReset = f.Attribute("canautoreset") != null ? (bool)f.Attribute("canautoreset") : true,
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),
                                        FloatProperties = e.Elements("floatproperty").Select(f => new IniPropertyFloat
                                        {
                                            CanAutoReset = f.Attribute("canautoreset") != null ? (bool)f.Attribute("canautoreset") : true,
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),
                                        EnumProperties = e.Elements("enumproperty").Select(f => new IniPropertyEnum
                                        {
                                            CanAutoReset = f.Attribute("canautoreset") != null ? (bool)f.Attribute("canautoreset") : true,
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            Choices = f.Elements("enumvalue").Select(g => new IniPropertyEnumValue
                                            {
                                                FriendlyName = (string)g.Attribute("friendlyname"),
                                                Notes = (string)g.Attribute("notes"),
                                                IniValue = g.Value
                                            }).ToList()
                                        }).ToList(),
                                        NameProperties = e.Elements("nameproperty").Select(f => new IniPropertyName
                                        {
                                            CanAutoReset = f.Attribute("canautoreset") != null ? (bool)f.Attribute("canautoreset") : true,
                                            PropertyName = (string)f.Attribute("propertyname"),
                                            FriendlyPropertyName = (string)f.Attribute("friendlyname"),
                                            Notes = (string)f.Attribute("notes"),
                                            OriginalValue = f.Value
                                        }).ToList(),

                                    }).ToList();

                    List<IniPropertyMaster> items = new List<IniPropertyMaster>();
                    foreach (IniSection sec in linqlist)
                    {
                        sec.PropogateOwnership();
                        items.AddRange(sec.GetAllProperties());
                    }

                    string inifilepath = Path.Combine(configFileFolder, Path.GetFileNameWithoutExtension(kp.Key) + ".ini");
                    if (File.Exists(inifilepath))
                    {
                        DuplicatingIni configIni = DuplicatingIni.LoadIni(inifilepath);
                        foreach (IniPropertyMaster prop in items)
                        {
                            prop.LoadCurrentValue(configIni);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Mass Effect Config file " + Path.GetFileNameWithoutExtension(kp.Key) + ".ini is missing. It should be located at " + inifilepath + ". Please run the game at least once to generate the default files.");
                        Environment.Exit(1);
                    }


                    kp.Value.ReplaceAll(items);
                    CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(kp.Value);
                    PropertyGroupDescription groupDescription = new PropertyGroupDescription("SectionFriendlyName");
                    view.GroupDescriptions.Add(groupDescription);
                }
            }
            else
            {
                MessageBox.Show("Mass Effect Config directory is missing. It should be located at " + configFileFolder + ". Please run the game at least once to generate the default files.");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string GetPropertyMap(string filename)
        {
            string result = string.Empty;

            using Stream stream = this.GetType().Assembly.
                GetManifestResourceStream($@"MassEffectModManagerCore.modmanager.meim.propertymaps.{filename}");
            using StreamReader sr = new StreamReader(stream);
            result = sr.ReadToEnd();
            return result;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
