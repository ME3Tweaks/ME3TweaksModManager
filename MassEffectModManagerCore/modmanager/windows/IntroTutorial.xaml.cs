using System.ComponentModel;
using System.IO;
using System.Windows;
using LegendaryExplorerCore.Misc;
using MassEffectModManagerCore.modmanager.diagnostics;
using MassEffectModManagerCore.ui;

namespace MassEffectModManagerCore.modmanager.windows
{
    /// <summary>
    /// Interaction logic for IntroTutorial.xaml
    /// </summary>
    public partial class IntroTutorial : Window, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<TutorialStep> TutorialSteps { get; } = new ObservableCollectionExtended<TutorialStep>();

        public TutorialStep CurrentStep { get; set; }
        //public class TutorialStep
        //{
        //    public string StringKey { get; set; }
        //    public string UIString { get; set; }
        //    public string ImagePath { get; set; }
        //    public int ColumnIndex { get; set; }
        //    public int RowIndex { get; set; }
        //    public int ColumnSpan { get; set; }
        //    public int RowSpan { get; set; }
        //}


        public class TutorialStep
        {
            public string step { get; set; }
            public string internalname { get; set; }
            public string imagename { get; set; }
            public string imagemd5 { get; set; }
            public string columnindex { get; set; }
            public string rowindex { get; set; }
            public string columnspan { get; set; }
            public string rowspan { get; set; }
            public string lang_int { get; set; }
            public string lang_rus { get; set; }
            public string lang_pol { get; set; }
            public string lang_deu { get; set; }
            public string lang_fra { get; set; }
            public string lang_esn { get; set; }
            public string UIString { get; set; } //What's actually shown on screen.
            public string UIImagePath { get; set; } //image path

        }

        public int CurrentStepIndex { get; set; }
        public IntroTutorial()
        {
            DataContext = this;
            TutorialSteps.ReplaceAll(App.TutorialService);

            //Setup languages.
            foreach (var tutorialStep in TutorialSteps)
            {
                switch (Settings.Language)
                {
                    case @"int":
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
                    case @"esn":
                        tutorialStep.UIString = tutorialStep.lang_esn;
                        break;
                    case @"fra":
                        tutorialStep.UIString = tutorialStep.lang_fra;
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

                tutorialStep.UIString = tutorialStep.UIString.Replace(@"\n", "\n"); //do not localize
                tutorialStep.UIImagePath = Path.Combine(Utilities.GetTutorialServiceCache(), tutorialStep.imagename);
            }

            CurrentStep = TutorialSteps[0];
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            SkipTutorialCommand = new GenericCommand(Close);
            NextCommand = new GenericCommand(MoveForward, CanMoveForward);
            PreviousCommand = new GenericCommand(MoveBackwards, CanMoveBackwards);
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

        //Fody uses this property on weaving
#pragma warning disable
public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
