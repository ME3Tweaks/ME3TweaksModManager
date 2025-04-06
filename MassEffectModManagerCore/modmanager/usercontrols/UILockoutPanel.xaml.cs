using System.Windows.Input;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Used to lock up the user interface to prevent interaction
    /// </summary>
    public partial class UILockoutPanel : MMBusyPanelBase
    {
        public string StatusText { get; set; }


        /// <summary>
        /// Closes this panel, thus unlocking the UI.
        /// </summary>
        public void UnlockUI()
        {
            OnClosing(DataEventArgs.Empty);
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            
        }

        public override void OnPanelVisible()
        {

        }
    }
}
