using FontAwesome5;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod;
using PropertyChanged;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ME3TweaksModManager.modmanager.objects.deployment.checks
{
    /// <summary>
    /// A single deployment checklist item and state
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class DeploymentChecklistItem : ReferenceCheckPackage, INotifyPropertyChanged
    {
        /// <summary>
        /// The mod being checked
        /// </summary>
        public Mod ModToValidateAgainst;

        // Bindings
        public string ItemText { get; set; }
        public SolidColorBrush Foreground { get; private set; }
        public EFontAwesomeIcon Icon { get; private set; }
        public bool Spinning { get; private set; }

        /// <summary>
        /// If there is a blocking issue with the deployment
        /// </summary>
        public bool DeploymentBlocking => GetBlockingErrors().Any();

        /// <summary>
        /// String to show when you mouse over the checklist item.
        /// </summary>
        public string ToolTip { get; set; }

        /// <summary>
        /// The function to execute to perform the validation for this check
        /// </summary>
        public Action<DeploymentChecklistItem> ValidationFunction { get; set; }

        /// <summary>
        /// The title of the list dialog if there are any messages
        /// </summary>
        public string DialogTitle { get; set; }

        /// <summary>
        /// The message to show if there's a need to show a dialog
        /// </summary>
        public string DialogMessage { get; set; }


        /// <summary>
        /// If this check is done; no more processing should occur (either due to completion or due to cancelation)
        /// </summary>
        public bool CheckDone { get; private set; }

        /// <summary>
        /// If there's any messages available to show
        /// </summary>
        public bool HasMessage => CheckDone && HasAnyMessages();

        /// <summary>
        /// Target to validate against if needed
        /// </summary>
        public GameTarget internalValidationTarget { get; internal set; }

        public DeploymentChecklistItem()
        {
            Initialize();
        }

        private void Initialize()
        {
            Icon = EFontAwesomeIcon.Solid_Spinner;
            Spinning = true;
            Foreground = Application.Current.FindResource(AdonisUI.Brushes.DisabledAccentForegroundBrush) as SolidColorBrush;
            ToolTip = M3L.GetString(M3L.string_validationInProgress);
        }

        public void SetDone()
        {
            CheckDone = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMessage)));
        }

        public void ExecuteValidationFunction()
        {
            Foreground = Application.Current.FindResource(AdonisUI.Brushes.HyperlinkBrush) as SolidColorBrush;
            ValidationFunction?.Invoke(this);
            Spinning = false;
            if (!HasAnyMessages())
            {
                Foreground = Brushes.Green;
                Icon = EFontAwesomeIcon.Solid_CheckCircle;
            }
            else if (GetBlockingErrors().Any())
            {
                Foreground = Brushes.Red;
                Icon = EFontAwesomeIcon.Solid_TimesCircle;
            }
            else if (GetSignificantIssues().Any())
            {
                Foreground = Brushes.Orange;
                Icon = EFontAwesomeIcon.Solid_ExclamationTriangle;
            }
            else if (GetInfoWarnings().Any())
            {
                Foreground = Brushes.DodgerBlue;
                Icon = EFontAwesomeIcon.Solid_InfoCircle;
            }

            SetDone();
        }

        //Fody uses this property on weaving
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        public bool HasAnyMessages() => GetInfoWarnings().Any() || GetSignificantIssues().Any() || GetBlockingErrors().Any();

        public void Reset()
        {
            ClearMessages();
            CheckDone = false;
            Initialize();
        }

        public void SetAbandoned()
        {
            CheckDone = true; // Mark no more processing
            Foreground = Brushes.Gray;
            Icon = EFontAwesomeIcon.Solid_Ban;
            Spinning = false;
        }
    }
}
