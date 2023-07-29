using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dark.Net;
using ME3TweaksModManager.modmanager.localizations;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Dialog that has copy button, designed for showing lists of short lines of text
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class ListDialog : Window
    {
        public int StartingHeight { get; set; } = 480;
        public int StartingWidth { get; set; } = 640;

        List<string> items;
        public ListDialog(List<string> listItems, string title, string message, Window owner, int width = 0, int height = 0)
        {
            InitializeComponent();
            this.ApplyDefaultTheming();
            Title = title;
            ListDialog_Message.Text = message;
            items = listItems;
            if (width != 0)
            {
                StartingWidth = width;
            }
            if (height != 0)
            {
                StartingHeight = height;
            }
            foreach (string str in listItems)
            {
                ListDialog_List.Items.Add(str);
            }
            Owner = owner;
        }

        private void CopyItemsToClipBoard_Click(object sender, RoutedEventArgs e)
        {
            string toClipboard = string.Join(Environment.NewLine, items);
            try
            {
                Clipboard.SetText(toClipboard);
                ListDialog_Status.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                //yes, this actually happens sometimes...
                M3L.ShowDialog(this, M3L.GetString(M3L.string_dialogCouldNotSetDataToClipboard) + ex.Message, M3L.GetString(M3L.string_errorCopyingDataToClipboard), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                string text = ((TextBlock)sender).Text;
                if (File.Exists(text))
                {
                    M3Utilities.HighlightInExplorer(text);
                }
            }
        }
    }
}
