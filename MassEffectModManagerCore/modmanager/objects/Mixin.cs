using MassEffectModManagerCore.modmanager.me3tweaks;
using MassEffectModManagerCore.ui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Input;

namespace MassEffectModManagerCore.modmanager.objects
{
    [DebuggerDisplay("Mixin - {PatchName} - v{PatchVersion}")]
    /// <summary>
    /// MixIns are patches that can be stacked onto the same file multipe times as long as the file size does not change.
    /// They are powered by JojoDiff patch files and applied through the JPatch class
    /// </summary>
    public class Mixin : INotifyPropertyChanged
    {
        public event EventHandler UIStatusChanging;

        //UI ITEMS
        public void OnUISelectedForUseChanged() => UIStatusChanging?.Invoke(this, EventArgs.Empty);
        public bool UISelectedForUse { get; set; }
        public string UIText
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(PatchDesc);
                sb.AppendLine();
                sb.AppendLine("Applies to file " + TargetFile);
                sb.AppendLine("Part of " + TargetModule);
                sb.AppendLine("Version " + PatchVersion);
                sb.AppendLine("Developed by " + PatchDeveloper);
                sb.AppendLine(PatchFilename);
                if (IsFinalizer)
                {
                    sb.AppendLine();
                    sb.AppendLine("This is a finalizer Mixin. Finalizer Mixins change the filesize of the source file, and as such, only one finalizer mixin can be applied to a file. Once this Mixin is applied, you cannot apply other Mixins that apply to this file.");
                }
                if (!CanBeUsed)
                {
                    sb.AppendLine();
                    sb.AppendLine("This Mixin cannot be applied because the target module is not present in the game backup.");
                }
                return sb.ToString();
            }
        }

        public bool CanBeUsed { get; set; }

        //Manifest items
        public string PatchName { get; set; }
        public string PatchDesc { get; set; }
        public string PatchDeveloper { get; set; }
        public int PatchVersion { get; set; }
        //public string TargetVersion { get; set; }
        public ModJob.JobHeader TargetModule { get; set; }
        public string TargetFile { get; set; }
        public int TargetSize { get; set; }
        public bool IsFinalizer { get; set; }
        //public string patchurl { get; set; }
        public string FolderName { get; set; }
        public int ME3TweaksID { get; set; }
        public string PatchFilename { get; internal set; }
        public MemoryStream PatchData { get; internal set; }

        //public ICommand ToggleSelectedCommand { get; }
        public Mixin()
        {
            //ToggleSelectedCommand = new GenericCommand(ToggleUISelected);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        //private void ToggleUISelected()
        //{
        //    UISelectedForUse = !UISelectedForUse;
        //}
    }
}
