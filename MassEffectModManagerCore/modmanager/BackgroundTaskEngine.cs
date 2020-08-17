using Serilog;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace MassEffectModManagerCore.modmanager
{
    [Localizable(false)]
    public class BackgroundTaskEngine
    {
        //No real concurrent list so i guess we'll use a dictionary
        private ConcurrentDictionary<int, BackgroundTask> backgroundJobs = new ConcurrentDictionary<int, BackgroundTask>();
        private Action<string> updateTextDelegate;
        private Action showIndicatorDelegate;
        private Action hideIndicatorDelegate;
        private int nextJobID = 1;
        private static object lockSubmitJob = new object();
        private static object lockReleaseJob = new object();


        public ConcurrentDictionary<int, BackgroundTask> getJobs() => backgroundJobs;


        public BackgroundTaskEngine(Action<string> updateTextDelegate, Action showIndicatorDelegate, Action hideIndicatorDelegate)
        {
            this.updateTextDelegate = updateTextDelegate;
            this.showIndicatorDelegate = showIndicatorDelegate;
            this.hideIndicatorDelegate = hideIndicatorDelegate;
        }

        public BackgroundTask SubmitBackgroundJob(string taskName, string uiText = null, string finishedUiText = null)
        {
            lock (lockSubmitJob)
            {
                if (uiText != null && finishedUiText == null || uiText == null && finishedUiText != null)
                {
                    throw new Exception("Internal error: Cannot submit background job only specifying start or end text without the specifying both.");
                }

                BackgroundTask bt = new BackgroundTask(taskName, ++nextJobID, uiText, finishedUiText);
                backgroundJobs.TryAdd(bt.jobID, bt);
                if (uiText != null)
                {
                    updateTextDelegate(uiText);
                }

                showIndicatorDelegate();
                Log.Information("Submitted a background task to engine: " + taskName);
                return bt;
            }
        }

        public void SubmitJobCompletion(BackgroundTask task)
        {
            lock (lockReleaseJob)
            {
                if (backgroundJobs.TryRemove(task.jobID, out BackgroundTask t))
                {
                    Log.Information("Completed a background task: " + t.taskName);
                    if (!backgroundJobs.Any())
                    {
                        hideIndicatorDelegate();
                        if (task.finishedUiText != null)
                        {
                            updateTextDelegate(task.finishedUiText);
                        }
                    }
                    else
                    {
                        backgroundJobs.First().Value.active = true;
                        updateTextDelegate(backgroundJobs.First().Value.uiText);
                    }
                }
            }
        }
    }

    public class BackgroundTask
    {
        internal string taskName; //Taskname is mostly useful for debugging.
        internal string uiText;
        internal string finishedUiText;
        internal int jobID;
        internal bool active;

        public BackgroundTask(string taskName, int jobID, string uiText = null, string finishedUiText = null)
        {
            this.taskName = taskName;
            this.uiText = uiText;
            this.finishedUiText = finishedUiText;
            this.jobID = jobID;
        }
    }
}
