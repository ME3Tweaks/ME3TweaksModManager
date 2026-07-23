using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.localizations;
using Microsoft.Win32;
using System.Windows;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>
    /// Contains utility methods for using Trilogy Save Editor
    /// </summary>
    internal class TrilogySaveEditorHelper
    {
        public static void OpenTSE(Window w, string saveFilePath = null)
        {
            void notInstalled()
            {
                M3L.ShowDialog(w, M3L.GetString(M3L.string_dialog_tseNotInstalled), M3L.GetString(M3L.string_tSENotInstalled), MessageBoxButton.OK, MessageBoxImage.Warning);
                M3Utilities.OpenWebpage(@"https://github.com/KarlitosVII/trilogy-save-editor/releases/latest");
            }

            var tseInstallPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\{6A0B979E-271B-4E50-A4C3-487C8E584070}_is1", @"Inno Setup: App Path", null);
            if (tseInstallPath == null)
            {
                notInstalled();
                return;
            }

            var tseExecutable = Path.Combine(tseInstallPath, @"trilogy-save-editor.exe");
            if (File.Exists(tseExecutable))
            {
                if (saveFilePath != null)
                {
                    // We put it in args so OS doesn't split on spaces
                    MUtilities.RunProcess(tseExecutable, [saveFilePath]);
                }
                else
                {
                    MUtilities.RunProcess(tseExecutable);
                }
            }
            else
            {
                notInstalled();
            }
        }
    }
}
