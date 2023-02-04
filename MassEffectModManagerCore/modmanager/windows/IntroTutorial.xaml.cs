using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects.tutorial;
using ME3TweaksModManager.ui;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for IntroTutorial.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class IntroTutorial : Window
    {
        /// <summary>
        /// The list of all steps in the tutorial
        /// </summary>
        public ObservableCollectionExtended<TutorialStep> TutorialSteps { get; } = new ObservableCollectionExtended<TutorialStep>();

        /// <summary>
        /// The current step the tutorial is on
        /// </summary>
        public TutorialStep CurrentStep { get; set; }

        /// <summary>
        /// The index of the current step (for stepping through the step list)
        /// </summary>
        public int CurrentStepIndex { get; set; }


        public IntroTutorial(Window owner)
        {
            Owner = owner;
            PrepareSteps();

            CurrentStep = TutorialSteps[0];
            LoadCommands();
            InitializeComponent();
        }

        /// <summary>
        /// Prepares the step list from the tutorial service.
        /// </summary>
        private void PrepareSteps()
        {
            TutorialSteps.ReplaceAll(TutorialService.GetTutorialSteps());
            //Setup languages.
            foreach (var tutorialStep in TutorialSteps)
            {
                switch (Settings.Language)
                {
                    case @"int":
                        //Debug.WriteLine(tutorialStep.lang_int);
                        tutorialStep.UIString = tutorialStep.lang_int;
                        break;
                    case @"rus":
                        tutorialStep.UIString = tutorialStep.lang_rus;
                        break;
                    case @"deu":
                        tutorialStep.UIString = tutorialStep.lang_deu;
                        break;
                    case @"pol":
                        tutorialStep.UIString = tutorialStep.lang_pol;
                        break;
                    case @"ita":
                        tutorialStep.UIString = tutorialStep.lang_ita;
                        break;
                    case @"bra":
                        tutorialStep.UIString = tutorialStep.lang_bra;
                        break;
                    default:
                        M3Log.Error(@"Tutorial doesn't support language: " + Settings.Language);
                        break;
                }

                if (string.IsNullOrWhiteSpace(tutorialStep.UIString))
                {
                    // failover to int
                    tutorialStep.UIString = tutorialStep.lang_int;
                }

                if (tutorialStep.UIString != null)
                {
                    tutorialStep.UIString = tutorialStep.UIString.Replace(@"\n", "\n"); //do not localize
                }

                tutorialStep.UIImagePath = Path.Combine(M3Filesystem.GetTutorialServiceCache(), tutorialStep.imagename);
#if DEBUG
                if (!File.Exists(tutorialStep.UIImagePath))
                {
                    Debug.WriteLine($@"File not found for tutorial: {tutorialStep.UIImagePath}");
                }
                else
                {
                    Debug.WriteLine($@"OK -- File found for tutorial: {tutorialStep.UIImagePath}");
                }
#endif
            }
        }

        private void LoadCommands()
        {
            SkipTutorialCommand = new GenericCommand(Close);
            NextCommand = new GenericCommand(MoveForward, CanMoveForward);
            PreviousCommand = new GenericCommand(MoveBackwards, CanMoveBackwards);
            ReloadTutorialCommand = new GenericCommand(ReloadTutorial, CanReloadTutorial);
        }

        private void ReloadTutorial()
        {
#if DEBUG
            TutorialService.LoadService(true);
            PrepareSteps();

            // Restore the state
            if (TutorialSteps.Count >= CurrentStepIndex)
            {
                CurrentStep = TutorialSteps[CurrentStepIndex];
            }
            else
            {
                CurrentStep = TutorialSteps[0];
                CurrentStepIndex = 0;
            }
#endif
        }
        private bool CanReloadTutorial()
        {
            return App.IsDebug;
        }

        private void MoveBackwards()
        {
            CurrentStepIndex--;
            CurrentStep = TutorialSteps[CurrentStepIndex];
        }

        private bool CanMoveBackwards() => CurrentStepIndex > 0;

        private bool CanMoveForward() => CurrentStepIndex < TutorialSteps.Count - 1;

        private void MoveForward()
        {
            CurrentStepIndex++;
            CurrentStep = TutorialSteps[CurrentStepIndex];
        }

        public GenericCommand NextCommand { get; set; }
        public GenericCommand PreviousCommand { get; set; }
        public GenericCommand SkipTutorialCommand { get; set; }
        public GenericCommand ReloadTutorialCommand { get; set; }
    }
}
