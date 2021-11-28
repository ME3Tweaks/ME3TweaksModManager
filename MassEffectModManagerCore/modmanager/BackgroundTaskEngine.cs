using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using MassEffectModManagerCore.modmanager.diagnostics;
using PropertyChanged;

namespace MassEffectModManagerCore.modmanager
{
    /// <summary>
    /// Controller for the bottom left text in the main window for M3
    /// </summary>
    [Localizable(false)]
    [AddINotifyPropertyChangedInterface]
    public class BackgroundTaskEngine
    {
        /// <summary>
        /// The instance of the BackgroundTaskEngine.
        /// </summary>
        public static BackgroundTaskEngine Instance { get; private set; }

        /// <summary>
        /// Submits an update for the text of the given task. It will update the text via the delegate.
        /// </summary>
        /// <param name="bt"></param>
        /// <param name="newStr"></param>
        public static void SubmitBackgroundTaskUpdate(BackgroundTask bt, string newStr) => Instance.InternalSubmitBackgroundTaskUpdate(bt, newStr);


        // PRIVATE VARIABLES
        //No real concurrent list so i guess we'll use a dictionary
        private ConcurrentDictionary<int, BackgroundTask> backgroundJobs = new();
        private int nextJobID = 1;
        private static object lockSubmitJob = new object();
        private static object lockReleaseJob = new object();
        private Action<string> updateTextDelegate { get; init; }
        /// <summary>
        /// Invoked when the activity indicator should be shown.
        /// </summary>
        private Action showIndicatorDelegate { get; init; }
        /// <summary>
        /// Invoked when the activity indicator should be hidden.
        /// </summary>
        private Action hideIndicatorDelegate { get; init; }

        /// <summary>
        /// The current active task. If there is no active task this will be the last finished task.
        /// </summary>
        public BackgroundTask ActiveTask { get; set; }
        public ConcurrentDictionary<int, BackgroundTask> getJobs() => backgroundJobs;



        public static void InitializeTaskEngine(Action<string> updateTextDelegate, Action showIndicatorDelegate, Action hideIndicatorDelegate)
        {
            Instance = new BackgroundTaskEngine(updateTextDelegate, showIndicatorDelegate, hideIndicatorDelegate);
        }

        
        private BackgroundTaskEngine(Action<string> updateTextDelegate, Action showIndicatorDelegate, Action hideIndicatorDelegate)
        {
            this.updateTextDelegate = updateTextDelegate;
            this.showIndicatorDelegate = showIndicatorDelegate;
            this.hideIndicatorDelegate = hideIndicatorDelegate;
        }

        private void InternalSubmitBackgroundTaskUpdate(BackgroundTask bt, string newStr)
        {
            bt.UIText = newStr;
            if (ActiveTask == bt)
            {
                updateTextDelegate(newStr);
            }
        }

        public static BackgroundTask SubmitBackgroundJob(string taskName, string uiText = null, string finishedUiText = null) => Instance.InternalSubmitBackgroundJob(taskName, uiText, finishedUiText);

        private BackgroundTask InternalSubmitBackgroundJob(string taskName, string uiText = null, string finishedUiText = null)
        {
            lock (lockSubmitJob)
            {
                if (uiText != null && finishedUiText == null || uiText == null && finishedUiText != null)
                {
                    throw new Exception(@"Internal error: Cannot submit background job only specifying start or end text without the specifying both.");
                }

                BackgroundTask bt = new BackgroundTask(taskName, ++nextJobID, uiText, finishedUiText);
                backgroundJobs.TryAdd(bt.TaskID, bt);
                if (uiText != null)
                {
                    updateTextDelegate(uiText);
                }
                ActiveTask = bt;
                showIndicatorDelegate();
                M3Log.Information(@"Submitted a background task to engine: " + taskName);
                return bt;
            }
        }


        public static void SubmitJobCompletion(BackgroundTask task) => Instance.InternalSubmitJobCompletion(task);

        private void InternalSubmitJobCompletion(BackgroundTask task)
        {
            lock (lockReleaseJob)
            {
                if (backgroundJobs.TryRemove(task.TaskID, out BackgroundTask t))
                {
                    M3Log.Information(@"Completed a background task: " + t.TaskName);
                    if (!backgroundJobs.Any())
                    {
                        hideIndicatorDelegate();
                        if (task.FinishedUIText != null)
                        {
                            updateTextDelegate(task.FinishedUIText);
                        }
                        ActiveTask = null;
                    }
                    else
                    {
                        ActiveTask = backgroundJobs.First().Value;
                        updateTextDelegate(ActiveTask.UIText);
                    }
                }
            }
        }

        private void OnActiveJobChanged(object oldValue, object newValue)
        {
            if (oldValue is BackgroundTask bto)
            {
                bto.Active = false;
            }

            if (newValue is BackgroundTask btn)
            {
                btn.Active = true;
            }
        }

        public event EventHandler NotifyBackgroundJobChanged;
    }

    /// <summary>
    /// Class that describes an ongoing operation for display in a UI.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class BackgroundTask
    {
        internal string TaskName; //Taskname is mostly useful for debugging.

        /// <summary>
        /// Text to bind to in the UI
        /// </summary>
        internal string UIText { get; set; }

        /// <summary>
        /// Text that will be set when the task completes
        /// </summary>
        internal string FinishedUIText { get; set; }
        /// <summary>
        /// The ID of the task.
        /// </summary>
        internal int TaskID { get; set; }

        /// <summary>
        /// If the task is currently active.
        /// </summary>
        internal bool Active { get; set; }

        /// <summary>
        /// Constructs a new BackgroundTask.
        /// </summary>
        /// <param name="taskName"></param>
        /// <param name="taskId"></param>
        /// <param name="uiText"></param>
        /// <param name="finishedUiText"></param>
        public BackgroundTask(string taskName, int taskId, string uiText = null, string finishedUiText = null)
        {
            this.TaskName = taskName;
            this.UIText = uiText;
            this.FinishedUIText = finishedUiText;
            this.TaskID = taskId;
        }
    }
}
