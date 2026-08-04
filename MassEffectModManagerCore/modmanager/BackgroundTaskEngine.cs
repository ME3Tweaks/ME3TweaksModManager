using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using ME3TweaksModManager.modmanager.diagnostics;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager
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
        private readonly ConcurrentDictionary<int, BackgroundTask> backgroundJobs = new();
        private int nextJobID = 0;
        private static readonly object lockSubmitJob = new object();
        private static readonly object lockReleaseJob = new object();
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
        
        /// <summary>
        /// Gets the currently running background jobs. For diagnostic purposes only.
        /// </summary>
        /// <returns></returns>
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
            if (ActiveTask == bt && !SuppressMessageUpdates)
            {
                updateTextDelegate(newStr);
            }
        }

        /// <summary>
        /// If message updates should occur
        /// </summary>
        private bool SuppressMessageUpdates { get; set; }

        public static BackgroundTask SubmitBackgroundJob(string taskName, string uiText = null, string finishedUiText = null) => Instance.InternalSubmitBackgroundJob(taskName, uiText, finishedUiText);

        private BackgroundTask InternalSubmitBackgroundJob(string taskName, string uiText = null, string finishedUiText = null)
        {
            lock (lockSubmitJob)
            {
                if (uiText != null && finishedUiText == null || uiText == null && finishedUiText != null)
                {
                    throw new Exception(@"Internal error: Cannot submit background job only specifying start or end text without the specifying both.");
                }

                int taskId = Interlocked.Increment(ref nextJobID);
                BackgroundTask bt = new BackgroundTask(taskName, taskId, uiText, finishedUiText);
                backgroundJobs.TryAdd(bt.TaskID, bt);
                if (uiText != null && !SuppressMessageUpdates)
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
                        if (task.FinishedUIText != null && !SuppressMessageUpdates)
                        {
                            updateTextDelegate(task.FinishedUIText);
                        }
                        ActiveTask = null;
                    }
                    else
                    {
                        // Get the task with the highest ID (most recently added)
                        ActiveTask = backgroundJobs.OrderByDescending(x => x.Key).First().Value;
                        if (!SuppressMessageUpdates)
                        {
                            updateTextDelegate(ActiveTask.UIText);
                        }
                    }
                }
            }
        }

        // This method is weaved by Fody
        private void OnActiveTaskChanged(object oldValue, object newValue)
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

        /// <summary>
        /// Prevents the bottom left text from updating
        /// </summary>
        public static void SuppressStatusMessageUpdates()
        {
            Instance.SuppressMessageUpdates = true;
        }

        /// <summary>
        /// Allows the bottom left text to update
        /// </summary>
        public static void AllowMessageUpdates()
        {
            Instance.SuppressMessageUpdates = false;
        }

        /// <summary>
        /// Suppresses status message updates and returns a disposable that will restore updates when disposed.
        /// Use with 'using' statement for automatic restoration.
        /// Usage: using var suppressionObject =  BackgroundTaskEngine.SuppressStatusMessageUpdatesScoped();
        /// </summary>
        /// <returns>A disposable that restores message updates when disposed</returns>
        public static IDisposable SuppressStatusMessageUpdatesScoped()
        {
            SuppressStatusMessageUpdates();
            return new DisposableAction(() => AllowMessageUpdates());
        }

        private class DisposableAction : IDisposable
        {
            private readonly Action action;
            private bool disposed;

            public DisposableAction(Action action)
            {
                this.action = action ?? throw new ArgumentNullException(nameof(action));
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    action();
                    disposed = true;
                }
            }
        }
    }

    /// <summary>
    /// Class that describes an ongoing operation for display in a UI.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class BackgroundTask
    {
        /// <summary>
        /// Task name is mostly useful for debugging.
        /// </summary>
        public string TaskName { get; }

        /// <summary>
        /// Text to bind to in the UI
        /// </summary>
        public string UIText { get; internal set; }

        /// <summary>
        /// Text that will be set when the task completes
        /// </summary>
        public string FinishedUIText { get; set; }
        
        /// <summary>
        /// The ID of the task.
        /// </summary>
        public int TaskID { get; }

        /// <summary>
        /// If the task is currently active.
        /// </summary>
        public bool Active { get; internal set; }

        /// <summary>
        /// Constructs a new BackgroundTask.
        /// </summary>
        /// <param name="taskName"></param>
        /// <param name="taskId"></param>
        /// <param name="uiText"></param>
        /// <param name="finishedUiText"></param>
        public BackgroundTask(string taskName, int taskId, string uiText = null, string finishedUiText = null)
        {
            this.TaskName = taskName ?? throw new ArgumentNullException(nameof(taskName));
            this.UIText = uiText;
            this.FinishedUIText = finishedUiText;
            this.TaskID = taskId;
        }
    }
}
