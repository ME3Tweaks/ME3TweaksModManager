using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorerCore.Misc;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.extensions;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.objects.tutorial;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for IntroTutorial.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class IntroTutorial : Window, IClosableWindow
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

        /// <summary>
        /// Watcher for tutorial cache directory changes
        /// </summary>
        private FileSystemWatcher _tutorialWatcher;

        /// <summary>
        /// Timer for checking image availability
        /// </summary>
        private System.Timers.Timer _imageCheckTimer;

        public IntroTutorial(Window owner)
        {
            Owner = owner;
            PrepareSteps();

            if (TutorialSteps.Count > 0)
            {
                CurrentStep = TutorialSteps[0];
                LoadCommands();
                InitializeComponent();
                this.ApplyDarkNetWindowTheme();
                StartImageMonitoring();
                Closed += (a, b) => StopImageMonitoring(); // Handle window closing to prevent event handler leaks
            }
            else
            {
                M3Log.Warning(@"Cannot show tutorial: No tutorial steps were loaded");
            }
        }

        /// <summary>
        /// Prepares the step list from the tutorial service.
        /// </summary>
        private void PrepareSteps()
        {
            if (!TutorialService.ServiceLoaded)
                return; // Do not load

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
            if (TutorialSteps.Count > CurrentStepIndex)
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

        // Weaved by Fody
        /// <summary>
        /// Called when the current step changes. Triggers image download and refresh.
        /// </summary>
        private void OnCurrentStepChanged()
        {
            if (CurrentStep == null)
                return;

            // Start background task to ensure the image is available
            Task.Run(() =>
            {
                TutorialService.EnsureStepImageAvailable(CurrentStep);

                // Also ensure the next step's image to improve UX
                if (CurrentStepIndex + 1 < TutorialSteps.Count)
                {
                    TutorialService.EnsureStepImageAvailable(TutorialSteps[CurrentStepIndex + 1]);
                }
            });

            // Immediately check if image is available
            CheckCurrentStepImage();
        }

        public GenericCommand NextCommand { get; set; }
        public GenericCommand PreviousCommand { get; set; }
        public GenericCommand SkipTutorialCommand { get; set; }
        public GenericCommand ReloadTutorialCommand { get; set; }

        public bool AskToClose()
        {
            StopImageMonitoring();
            Close();
            return true;
        }

        /// <summary>
        /// Starts monitoring for image availability in the background.
        /// </summary>
        private void StartImageMonitoring()
        {
            try
            {
                var cacheDir = M3Filesystem.GetTutorialServiceCache();
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }

                // Set up file system watcher
                _tutorialWatcher = new FileSystemWatcher(cacheDir)
                {
                    Filter = "*.*",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                };

                _tutorialWatcher.Created += (s, e) => OnImageFileChanged(e.Name);
                _tutorialWatcher.Changed += (s, e) => OnImageFileChanged(e.Name);
                _tutorialWatcher.EnableRaisingEvents = true;

                // Set up timer to periodically check if current image is now available
                _imageCheckTimer = new System.Timers.Timer(500) // Check every 500ms
                {
                    AutoReset = true
                };
                _imageCheckTimer.Elapsed += (s, e) => CheckCurrentStepImage();
                _imageCheckTimer.Start();
            }
            catch (Exception ex)
            {
                M3Log.Warning($@"Failed to start tutorial image monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops monitoring for image availability.
        /// </summary>
        private void StopImageMonitoring()
        {
            _tutorialWatcher?.Dispose();
            _imageCheckTimer?.Stop();
            _imageCheckTimer?.Dispose();
        }

        /// <summary>
        /// Called when a file is detected in the tutorial cache directory.
        /// </summary>
        private void OnImageFileChanged(string fileName)
        {
            // Check if this is the image for the current step
            if (CurrentStep != null && fileName == CurrentStep.imagename)
            {
                CheckCurrentStepImage();
            }
        }

        /// <summary>
        /// Checks if the current step's image is now available and refreshes the binding if it is.
        /// </summary>
        private void CheckCurrentStepImage()
        {
            if (CurrentStep == null)
                return;

            var imagePath = CurrentStep.UIImagePath;
            if (string.IsNullOrWhiteSpace(imagePath))
                return;

            // Check if file now exists
            if (File.Exists(imagePath))
            {
                // Force a binding refresh by temporarily clearing and resetting the path
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var tempPath = CurrentStep.UIImagePath;
                    CurrentStep.UIImagePath = null;
                    CurrentStep.UIImagePath = tempPath;
                });
            }
        }
    }
}
