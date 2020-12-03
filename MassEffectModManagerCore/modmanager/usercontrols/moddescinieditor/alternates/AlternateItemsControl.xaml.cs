using System.ComponentModel;
using System.Windows.Controls;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor.alternates
{
    /// <summary>
    /// Interaction logic for AlternateItemsControl.xaml
    /// </summary>
    public partial class AlternateItemsControl : UserControl, INotifyPropertyChanged
    {
        public AlternateItemsControl()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
