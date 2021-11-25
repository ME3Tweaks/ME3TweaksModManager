using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.usercontrols.moddescinieditor
{
    /// <summary>
    /// Editor control for a multilist
    /// </summary>
    public partial class SingleMultilistEditorControl : UserControl, INotifyPropertyChanged
    {
        ///// <summary>
        ///// The list index (as in moddesc.ini) of this multilist
        ///// </summary>
        //public int ListIndex { get; set; }
        public SingleMultilistEditorControl()
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            AddFileCommand = new GenericCommand(AddFile);
        }

        private void AddFile()
        {
            if (DataContext is MDMultilist ml)
            {
                if (!ml.Files.Any() || !string.IsNullOrWhiteSpace(ml.Files.Last().Value))
                {
                    ml.Files.Add(new SingleMultilistEditorItem()
                    {
                        ItemIndex = ml.Files.Count + 1 // we use 1 based UI indexing
                    });
                }
            }

        }

        public GenericCommand AddFileCommand { get; set; }

        //public ObservableCollectionExtended<SingleMultilistEditorItem> ml.Files { get; } = new ObservableCollectionExtended<SingleMultilistEditorItem>();

        //Fody uses this property on weaving
#pragma warning disable
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

    public class SingleMultilistEditorItem : INotifyPropertyChanged
    {
        // The index of the file in the list
        public int ItemIndex { get; set; }
        // The value of the multilist item
        public string Value { get; set; }
        //Fody uses this property on weaving
#pragma warning disable
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
