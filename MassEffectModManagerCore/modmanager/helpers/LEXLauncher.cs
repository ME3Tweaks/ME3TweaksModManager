using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.usercontrols;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.helpers
{
    internal class LEXLauncher
    {
        public static void LaunchLEX(Window w, string packageFile, int uindex = 0, Action<string> currentTaskCallback = null, Action<int> setPercentDoneCallback = null, Action launchCompleted = null)
        {
            NamedBackgroundWorker bw = new NamedBackgroundWorker(@"LEXLauncher");
            #region callbacks
            void failedToDownload(string failureMessage)
            {
                MessageBox.Show(w, $"Legendary Explorer failed to download: {failureMessage}");
                launchCompleted?.Invoke();
            }
            void launchTool(string exe)
            {
                var arguments = $"--open \"{packageFile}\" --UIndex {uindex}"; // do not localize
                M3Log.Information($@"Launching: {exe} {arguments}");
                try
                {
                    var psi = new ProcessStartInfo(exe, arguments)
                    {
                        WorkingDirectory = Directory.GetParent(exe).FullName,
                    };
                    Process.Start(psi);

                }
                catch (Exception e)
                {
                    M3Log.Error($@"Error launching tool {exe}: {e.Message}");
                }
                launchCompleted?.Invoke();
            }

            void errorExtracting(Exception e, string message, string caption)
            {
                launchCompleted?.Invoke();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    M3L.ShowDialog(w, message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            #endregion
            bw.DoWork += (a, b) => { ExternalToolLauncher.FetchAndLaunchTool(ExternalToolLauncher.LegendaryExplorer_Beta, currentTaskCallback, null, setPercentDoneCallback, launchTool, failedToDownload, errorExtracting); };
            bw.RunWorkerAsync();
        }
    }
}
