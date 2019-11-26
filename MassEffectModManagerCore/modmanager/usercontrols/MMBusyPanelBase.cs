using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    public abstract class MMBusyPanelBase : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool panelVisible = false;
        protected MMBusyPanelBase()
        {
            Loaded += UserControl_Loaded;
            Unloaded += UserControl_Unloaded;
        }

        protected Window window;
        protected MainWindow mainwindow;
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            window = Window.GetWindow(this);
            mainwindow = window as MainWindow;
            window.KeyDown += HandleKeyPress;
        }

        public abstract void HandleKeyPress(object sender, KeyEventArgs e);
        public abstract void OnPanelVisible();

        public event EventHandler<DataEventArgs> Close;
        protected virtual void OnClosing(DataEventArgs e)
        {
            Close?.Invoke(this, e);
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Unloading.");
            window.KeyDown -= HandleKeyPress;
            window = null; //lose reference
        }
    }
}
