using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MassEffectModManagerCore.modmanager.localizations;
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
        public class TutorialStep
        {
            public string StringKey { get; set; }
            public string UIString { get; set; }
            public string ImagePath { get; set; }
            public int ColumnIndex { get; set; }
            public int RowIndex { get; set; }
            public int ColumnSpan { get; set; }
            public int RowSpan { get; set; }
        }

        public int CurrentStepIndex { get; set; }
        public IntroTutorial()
        {
            DataContext = this;
            TutorialSteps.Add(new TutorialStep
            {
                StringKey = "string_tutorialModLibrary",
                ImagePath = "/images/introtutorial/modlibrary.png",
                UIString = "Your mod library is where mods are imported from their original archives. This makes it easy to reinstall mods, as well as be notified of updates to mods.\n\nMods shown in the mod library are NOT installed mods. You must press Apply Mod to install a mod to an Installation Target.",
                ColumnIndex = 4,
                RowIndex = 4,
                ColumnSpan = 5,
                RowSpan = 5
            });
            TutorialSteps.Add(new TutorialStep
            {
                StringKey = "string_tutorialTArgetDescription",
                ImagePath = "/images/introtutorial/target_description.png",
                UIString = "An Installation Target is a copy of the game and is shown at the top of the main window. Pressing Apply Mod will install the selected mod to this target.\n\nUsing the Manage Target button, you can manage what mods are installed.",
                ColumnIndex = 4,
                RowIndex = 5,
                ColumnSpan = 5,
                RowSpan = 4
            });
            TutorialSteps.Add(new TutorialStep
            {
                StringKey = "string_tutorialInstallationInformation",
                ImagePath = "/images/introtutorial/installation_information.png",
                UIString = "You can enable, disable, and delete DLC mods in this panel, if that feature is supported by that game. You can also restore files installed directly into the game by selecting the Modified basegame files and Modified SFARs (ME3 only) tabs.",
                ColumnIndex = 1,
                RowIndex = 5,
                ColumnSpan = 8,
                RowSpan = 4
            });

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

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
