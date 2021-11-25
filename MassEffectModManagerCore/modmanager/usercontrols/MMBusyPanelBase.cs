using MassEffectModManagerCore.ui;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MassEffectModManagerCore.modmanager.objects;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    public abstract class MMBusyPanelBase : UserControl, INotifyPropertyChanged
    {
        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

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
            OnPanelVisible();
        }

        /// <summary>
        /// Result of the panel on close
        /// </summary>
        public PanelResult Result = new();

        public abstract void HandleKeyPress(object sender, KeyEventArgs e);
        public abstract void OnPanelVisible();

        public event EventHandler<DataEventArgs> Close;
        protected virtual void OnClosing(DataEventArgs e)
        {
            Close?.Invoke(this, e);
            Application.Current.Dispatcher.Invoke(delegate
            {
                DataContext = null;
            });
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            window.KeyDown -= HandleKeyPress;
            window = null; //lose reference
        }

        public void TriggerPropertyChangedFor(string propertyname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// If the panel is still open. This can be used to prevent command from firing on closure.
        /// </summary>
        public bool IsPanelOpen => DataContext != null;
    }
}
