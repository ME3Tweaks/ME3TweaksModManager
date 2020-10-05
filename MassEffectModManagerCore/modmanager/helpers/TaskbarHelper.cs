using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Shell;
using Serilog;

namespace MassEffectModManagerCore.modmanager.helpers
{
    /// <summary>
    /// Helper for taskbar operations. Designed so it can avoid strange issues where setting taskbar stuff crashes the app instead
    /// due to some fun bugs in wpf
    /// </summary>
    public static class TaskbarHelper
    {
        private static bool initialized;
        private static TaskbarItemInfo helper;

        public static void Init(Window w)
        {
            if (initialized) return;
            try
            {
                helper = new TaskbarItemInfo();
                w.TaskbarItemInfo = helper;
            }
            catch (Exception e)
            {
                Log.Warning(@"Error initializing the taskbar helper. Progress in taskbar will not be displayed. Error:");
                Log.Information(App.FlattenException(e));
            }
            initialized = true;
        }

        public static void SetProgress(double progress)
        {
            if (helper != null)
            {
                helper.ProgressValue = progress;
            }
        }

        public static void SetProgressState(TaskbarItemProgressState state)
        {
            if (helper != null)
            {
                helper.ProgressState = state;
            }
        }
    }
}
