using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Interaction logic for CustomDLCEditorControl.xaml
    /// </summary>
    public partial class CustomDLCEditorControl : UserControl, INotifyPropertyChanged
    {
        public Mod EditingMod { get; set; }
        public void OnEditingModChanged()
        {
            if (EditingMod != null)
            {
                CustomDLCJob = EditingMod.GetJob(ModJob.JobHeader.CUSTOMDLC);
                if (CustomDLCJob != null)
                {
                    foreach (var v in CustomDLCJob.CustomDLCFolderMapping)
                    {
                        CustomDLCMapping.Add(new AlternateOption.Parameter(v.Key, v.Value));
                    }
                }
            }
            else
            {
                CustomDLCJob = null;
                CustomDLCMapping.ClearEx();
            }
        }

        public CustomDLCEditorControl()
        {
            DataContext = this;
            InitializeComponent();
        }

        public ModJob CustomDLCJob { get; set; }
        public ObservableCollectionExtended<AlternateOption.Parameter> CustomDLCMapping { get; } = new ObservableCollectionExtended<AlternateOption.Parameter>();

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
