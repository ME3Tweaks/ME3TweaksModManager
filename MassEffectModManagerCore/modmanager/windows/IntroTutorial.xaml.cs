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
                UIString = "In the Installation Information panel (accessed by clicking Manage Target), you can manage installed mods and modified basegame files.",
                ColumnIndex = 2,
                RowIndex = 6,
                ColumnSpan = 6,
                RowSpan = 2
            });

            // Menus
            TutorialSteps.Add(new TutorialStep
            {
                StringKey = "string_tutorialOptionsMenu",
                ImagePath = "/images/introtutorial/optionsmenu.png",
                UIString = "You can change settings for Mod Manager in the Options menu, in the Actions menu. Check them out at least once before you start modding.",
                ColumnIndex = 2,
                RowIndex = 8,
                ColumnSpan = 7,
                RowSpan = 2
            });
            TutorialSteps.Add(new TutorialStep
            {
                StringKey = "string_tutorialModManagementMenu",
                ImagePath = "/images/introtutorial/modmanagement.png",
                UIString = "The Mod Management menu has many useful things for managing and importing mods. Hover the mouse over items to get a description of what they do.\n\nYou can also generate a UI compatibility pack from this menu if you're using the Singleplayer Native Controller Support mod for Mass Effect 3.",
                ColumnIndex = 1,
                RowIndex = 7,
                ColumnSpan = 9,
                RowSpan = 3
            });
            TutorialSteps.Add(new TutorialStep
            {
                StringKey = "string_tutorialModUtilsMenu",
                ImagePath = "/images/introtutorial/modutils.png",
                UIString = "You can open the Mod Utils menu by right clicking a mod, or selecting it at the top menu bar when a mod is selected. You can do actions with the mod through this menu.\n\nThere are additional features in this menu (and others) if you're in Developer Mode.",
                ColumnIndex = 2,
                RowIndex = 8,
                ColumnSpan = 7,
                RowSpan = 2
            });
            TutorialSteps.Add(new TutorialStep
            {
                StringKey = "string_tutorialToolsMenu",
                ImagePath = "/images/introtutorial/toolsmenu.png",
                UIString = "The Tools menu contains many useful programs and utilities for modding, including AutoTOC for ME3, binkw32 bypass, and up to date modding tools if you're a mod developer.",
                ColumnIndex = 2,
                RowIndex = 8,
                ColumnSpan = 7,
                RowSpan = 2
            });
            TutorialSteps.Add(new TutorialStep
            {
                StringKey = "string_tutorialBackupRestoreMenu",
                ImagePath = "/images/introtutorial/backupmenu.png",
                UIString = "You can take full backups of your game with the Backup option. You can also do full restores, or game clones using the Restore option.\n\nBackups are an important step in modding - mistakes happen!",
                ColumnIndex = 3,
                RowIndex = 5,
                ColumnSpan = 5,
                RowSpan = 2
            });
            TutorialSteps.Add(new TutorialStep
            {
                StringKey = "string_tutorialHelpMenu",
                ImagePath = "/images/introtutorial/helpmenu.png",
                UIString = "The Help menu in Mod Manager is automatically updated from ME3Tweaks.com and contains useful information, diagnostics and logs, and other information about the program.",
                ColumnIndex = 2,
                RowIndex = 7,
                ColumnSpan = 7,
                RowSpan = 2
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
