﻿using System;
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
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.ui;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using PropertyChanged;
using Serilog;

namespace MassEffectModManagerCore.modmanager.usercontrols
{
    /// <summary>
    /// Interaction logic for OptionsPanel.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class OptionsPanel : MMBusyPanelBase
    {
        public OptionsPanel()
        {
            LibraryDir = Utilities.GetModsDirectory();
            LoadCommands();
            InitializeComponent();
        }


        public string LibraryDir { get; set; }
        public ICommand CloseCommand { get; set; }
        public ICommand ChangeLibraryDirCommand { get; set; }

        private void LoadCommands()
        {
            CloseCommand = new GenericCommand(() => OnClosing(DataEventArgs.Empty));
            ChangeLibraryDirCommand = new GenericCommand(ChangeLibraryDir);
        }

        private void ChangeLibraryDir()
        {

            if (mainwindow.ChooseModLibraryPath(false))
            {
                Result.ReloadMods = true;
                LibraryDir = Settings.ModLibraryPath;
            }
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnClosing(DataEventArgs.Empty);
            }
        }

        public override void OnPanelVisible()
        {

        }

        private void ChangeTheme_Dark_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeTheme(true);
        }

        private void ChangeTheme_Light_Clicked(object sender, RoutedEventArgs e)
        {
            ChangeTheme(false);
        }

        private void ChangeTheme(bool dark)
        {
            if (Settings.DarkTheme ^ dark)
            {
                Settings.DarkTheme = !Settings.DarkTheme;
                //Settings.Save();
                mainwindow.SetTheme();
            }
        }

        private void ChangeSetting_Clicked(object sender, RoutedEventArgs e)
        {
            //When this method is called, the value has already changed. So check against the opposite boolean state.
            var callingMember = sender as FrameworkElement;

            if (callingMember == BetaMode_MenuItem && Settings.BetaMode)
            {
                var result = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_dialog_optingIntoBeta), M3L.GetString(M3L.string_enablingBetaMode), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    Settings.BetaMode = false; //turn back off.
                    return;
                }
            }
            else if (callingMember == EnableTelemetry_MenuItem && !Settings.EnableTelemetry)
            {
                //user trying to turn it off 
                var result = M3L.ShowDialog(mainwindow, M3L.GetString(M3L.string_dialogTurningOffTelemetry), M3L.GetString(M3L.string_turningOffTelemetry), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    Settings.EnableTelemetry = true; //keep on.
                    return;
                }

                Log.Warning(@"Turning off telemetry :(");
                //Turn off telemetry.
                Analytics.SetEnabledAsync(false);
                Crashes.SetEnabledAsync(false);
            }
            else if (callingMember == EnableTelemetry_MenuItem)
            {
                //turning telemetry on
                Log.Information(@"Turning on telemetry :)");
                Analytics.SetEnabledAsync(true);
                Crashes.SetEnabledAsync(true);
            }
            else
            {
                //unknown caller. Might just be settings on/off for logging.
            }
        }
    }
}
