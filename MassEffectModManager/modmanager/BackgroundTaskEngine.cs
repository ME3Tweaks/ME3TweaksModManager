using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.ShellExtensions.Interop;

namespace MassEffectModManager.modmanager
{
    public class BackgroundTaskEngine
    {
        private List<BackgroundTask> backgroundJobs = new List<BackgroundTask>();
        private Action<string> updateTextDelegate;
        private Action showIndicatorDelegate;
        private Action hideIndicatorDelegate;
        private int nextJobID = 1;

        public BackgroundTaskEngine(Action<string> updateTextDelegate, Action showIndicatorDelegate, Action hideIndicatorDelegate)
        {
            this.updateTextDelegate = updateTextDelegate;
            this.showIndicatorDelegate = showIndicatorDelegate;
            this.hideIndicatorDelegate = hideIndicatorDelegate;
        }

        public BackgroundTask SubmitBackgroundJob(string taskName, string uiText = null)
        {
            BackgroundTask bt = new BackgroundTask(taskName, ++nextJobID, uiText);
            backgroundJobs.Add(bt);
            if (uiText != null)
            {
                updateTextDelegate(uiText);
            }
            showIndicatorDelegate();
            return bt;
        }

        public void SubmitJobCompletion(int jobID)
        {
            var finishingJob = backgroundJobs.RemoveAll(x => x.jobID == jobID);
            if (backgroundJobs.Count <= 0)
            {
                hideIndicatorDelegate();
            }
            else
            {
                backgroundJobs[0].active = true;
                updateTextDelegate(backgroundJobs[0].uiText);
            }
        }

        public void SubmitJobCompletion(BackgroundTask bt)
        {
            SubmitJobCompletion(bt.jobID);
        }
    }

    public class BackgroundTask
    {
        internal string taskName; //Taskname is mostly useful for debugging.
        internal string uiText;
        internal int jobID;
        internal bool active;

        public BackgroundTask(string taskName, int jobID, string uiText = null)
        {
            this.taskName = taskName;
            this.uiText = uiText;
            this.jobID = jobID;
        }
    }
}
