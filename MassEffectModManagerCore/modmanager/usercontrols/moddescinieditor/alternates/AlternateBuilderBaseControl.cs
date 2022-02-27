using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LegendaryExplorerCore.Misc;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.alternates;

namespace ME3TweaksModManager.modmanager.usercontrols.moddescinieditor.alternates
{
    /// <summary>
    /// Base class for both AlternateFileBuilder and AlternateDLCBuilder
    /// </summary>
    public abstract class AlternateBuilderBaseControl : ModdescEditorControlBase, INotifyPropertyChanged
    {
        public AlternateBuilderBaseControl() { }

        /// <summary>
        /// ModJob this alternate editor control is associated with
        /// </summary>
        public ModJob AttachedJob { get; set; }

        /// <summary>
        /// List of alternate objects that can be edited. These are not updated into the job, the mod must be serialized and re-read for them to be bound to the job.
        /// </summary>
        public ObservableCollectionExtended<AlternateOption> Alternates { get; } = new();
    }
}