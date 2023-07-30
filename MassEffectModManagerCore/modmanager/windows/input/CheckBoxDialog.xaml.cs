using System.Linq;
using System.Windows;
using Dark.Net;
using LegendaryExplorerCore.Misc;
using PropertyChanged;
using CheckBoxSelectionPair = ME3TweaksModManager.ui.CheckBoxSelectionPair;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for CheckBoxDialog.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class CheckBoxDialog : Window
    {
        public SizeToContent RequestedSizeToContent { get; set; } = SizeToContent.Manual;
        public ObservableCollectionExtended<CheckBoxSelectionPair> Items { get; } = new();
        public CheckBoxDialog(Window owningWindow, string message, string caption, object[] options, object[] preselectedOptions = null, object[] disabledOptions = null, int requestedWidth = 400, int requestedHeight = 250, SizeToContent requestedSizeToContent = SizeToContent.Manual)
        {
            Owner = owningWindow;
            Message = message;
            Caption = caption;

            RequestedWidth = requestedWidth;
            RequestedHeight = requestedHeight;
            RequestedSizeToContent = requestedSizeToContent;

            foreach (var o in options)
            {
                CheckBoxSelectionPair c =
                    new CheckBoxSelectionPair(o, preselectedOptions != null && preselectedOptions.Contains(o), null)
                    {
                        IsEnabled = disabledOptions == null || !disabledOptions.Contains(o)
                    };
                Items.Add(c);
            }


            InitializeComponent();
            this.ApplyDefaultDarkNetWindowStyle();
        }

        public int RequestedHeight { get; set; }

        public int RequestedWidth { get; set; }

        public string Message { get; }
        public string Caption { get; }

        public object[] GetSelectedItems()
        {
            return Items.Where(x => x.IsChecked).Select(x => x.Item).ToArray();
        }

        private void CloseDialog(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CheckAll(object sender, RoutedEventArgs e)
        {
            foreach (var v in Items)
            {
                v.IsChecked = true;
            }
        }
    }
}
