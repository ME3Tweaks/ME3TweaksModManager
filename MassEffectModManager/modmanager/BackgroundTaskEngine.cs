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

        public BackgroundTask SubmitBackgroundJob(string taskName, string uiText = null, string finishedUiText = null)
        {
            if (uiText != null && finishedUiText == null || uiText == null && finishedUiText != null)
            {
                throw new Exception("Internal error: Cannot submit background job only specifying start or end text without the specifying both.");
            }
            BackgroundTask bt = new BackgroundTask(taskName, ++nextJobID, uiText, finishedUiText);
            backgroundJobs.Add(bt);
            if (uiText != null)
            {
                updateTextDelegate(uiText);
            }
            showIndicatorDelegate();
            return bt;
        }

        public void SubmitJobCompletion(BackgroundTask task)
        {

            var finishingJob = backgroundJobs.RemoveAll(x => x.jobID == task.jobID);
            if (backgroundJobs.Count <= 0)
            {
                hideIndicatorDelegate();
                if (task.finishedUiText != null)
                {
                    updateTextDelegate(task.finishedUiText);
                }
            }
            else
            {
                backgroundJobs[0].active = true;
                updateTextDelegate(backgroundJobs[0].uiText);
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
