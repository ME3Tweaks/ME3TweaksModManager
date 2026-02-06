using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.windows;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace ME3TweaksModManager.modmanager.helpers
{
    /// <summary>
    /// Static methods that are used to show a variety of dialogs that are invoked from ME3TweaksCore
    /// </summary>
    class M3PromptCallbacks
    {
        /// <summary>
        /// Shown when an error has occurred. Shows only the OK option.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        public static void BlockingActionOccurred(string title, string message)
        {
            var isUiThread = Dispatcher.CurrentDispatcher.Thread == Thread.CurrentThread;

            object syncObj = new object();
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is Window window)
                {
                    M3L.ShowDialog(window, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                }

                if (!isUiThread)
                {
                    lock (syncObj)
                    {
                        Monitor.Pulse(syncObj);
                    }
                }
            });
            if (!isUiThread)
            {
                lock (syncObj)
                {
                    Monitor.Wait(syncObj);
                }
            }
        }

        /// <summary>
        /// Shown when prompting for user input.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="buttons"></param>
        /// <param name="image"></param>
        /// <param name="defaultOption">The default selected option</param>
        public static MessageBoxResult GetUserChoiceCallback(string title, string message, MessageBoxButton buttons, MessageBoxImage image, MessageBoxResult defaultOption)
        {
            var isUiThread = Dispatcher.CurrentDispatcher.Thread == Thread.CurrentThread;

            object syncObj = new object();
            var res = defaultOption;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is Window window)
                {
                    res = M3L.ShowDialog(window, message, title, buttons, image, defaultOption);
                }

                if (!isUiThread)
                {
                    lock (syncObj)
                    {
                        Monitor.Pulse(syncObj);
                    }
                }
            });
            if (!isUiThread)
            {
                lock (syncObj)
                {
                    Monitor.Wait(syncObj);
                }
            }

            return res;
        }

        /// <summary>
        /// Shown when a warning message should appear. Shows only the OK option.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        public static bool ShowWarningYesNoCallback(string title, string message, bool defaultResponse, string yesMessage, string noMessage)
        {
            var isUiThread = Dispatcher.CurrentDispatcher.Thread == Thread.CurrentThread;

            object syncObj = new object();

            bool result = defaultResponse;
            //object syncObj = new object();
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is Window window)
                {
                    result = M3L.ShowDialog(window, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, defaultResponse ? MessageBoxResult.Yes : MessageBoxResult.No, yesContent: yesMessage, noContent: noMessage) == MessageBoxResult.Yes;
                }

                if (!isUiThread)
                {
                    lock (syncObj)
                    {
                        Monitor.Pulse(syncObj);
                    }
                }
            });
            if (!isUiThread)
            {
                lock (syncObj)
                {
                    Monitor.Wait(syncObj);
                }
            }

            return result;
        }


        /// <summary>
        /// Shown when an error has occurred, with an attached supplementary list of data.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="listItems"></param>
        public static void ShowErrorListCallback(string title, string message, List<string> listItems)
        {
            //object syncObj = new object();
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is Window window)
                {
                    ListDialog ld = new ListDialog(listItems, title, message, window);
                    ld.ShowDialog();
                    //lock (syncObj)
                    //{
                    //    Monitor.Pulse(syncObj);
                    //}
                }
            });
            //lock (syncObj)
            //{
            //    Monitor.Wait(syncObj);
            //}
        }

        /// <summary>
        /// Shown when M3 asks the user to select a game executable. The executable is used to load its enclosing game target.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        public static string SelectGameExecutable(MEGame game)
        {
            //object syncObj = new object();
            string result = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                result = M3Utilities.PromptForGameExecutable(new[] { game });
                //lock (syncObj)
                //{
                //    Monitor.Pulse(syncObj);
                //}
            });
            //lock (syncObj)
            //{
            //    Monitor.Wait(syncObj);
            //}
            return result;
        }

        public static string SelectDirectory(string dialogTitle)
        {
            string selectedPath = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Not sure if this has to be synced
                if (WineWorkarounds.WineDetected)
                {
                    var ofd = new OpenFolderDialog
                    {
                        ValidateNames = true,
                        Title = dialogTitle
                    };
                    if (ofd.ShowDialog() == true)
                    {
                        selectedPath = ofd.FolderName;
                    }
                }
                else
                {
                    CommonOpenFileDialog ofd = new CommonOpenFileDialog
                    {
                        Title = dialogTitle,
                        IsFolderPicker = true,
                        EnsurePathExists = true
                    };
                    if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        selectedPath = ofd.FileName;
                    }
                }
            });
            return selectedPath;
        }
    }
}
