using ME3TweaksCore.Services;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.usercontrols;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ME3TweaksModManager
{
    public partial class MainWindow : Window
    {
        #region Properties

        /// <summary>
        /// Whether ME1 has been endorsed on NexusMods
        /// </summary>
        public bool ME1NexusEndorsed { get; set; }

        /// <summary>
        /// Whether ME2 has been endorsed on NexusMods
        /// </summary>
        public bool ME2NexusEndorsed { get; set; }

        /// <summary>
        /// Whether ME3 has been endorsed on NexusMods
        /// </summary>
        public bool ME3NexusEndorsed { get; set; }

        /// <summary>
        /// Whether Legendary Edition has been endorsed on NexusMods
        /// </summary>
        public bool LENexusEndorsed { get; set; }

        /// <summary>
        /// Text for endorsing M3 on NexusMods
        /// </summary>
        public string EndorseM3String { get; set; } = M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);

        /// <summary>
        /// The string shown at the top left of the main window for the NexusMods status
        /// </summary>
        public string NexusLoginInfoString { get; set; } // BLANK TO START = M3L.GetString(M3L.string_loginToNexusMods);

        /// <summary>
        /// The current endorsement status string for the selected mod
        /// </summary>
        public string CurrentModEndorsementStatus { get; private set; } = M3L.GetString(M3L.string_endorseMod);

        /// <summary>
        /// Whether the app is currently endorsing a mod
        /// </summary>
        public bool IsEndorsingMod { get; private set; }

        #endregion

        #region Commands

        public ICommand LoginToNexusCommand { get; set; }
        public GenericCommand EndorseSelectedModCommand { get; set; }
        public ICommand EndorseM3OnNexusCommand { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes NexusMods-related commands. Called from LoadCommands()
        /// </summary>
        private void LoadNexusCommands()
        {
            LoginToNexusCommand = new GenericCommand(ShowNexusPanel, CanShowNexusPanel);
            EndorseSelectedModCommand = new GenericCommand(EndorseWrapper, CanEndorseMod);
            EndorseM3OnNexusCommand = new GenericCommand(EndorseM3, CanEndorseM3);
        }

        /// <summary>
        /// Updates the Nexus Login status
        /// </summary>
        /// <param name="languageUpdateOnly">If we should only update the language text instead of a full update of API keys</param>
        public async Task RefreshNexusStatus(bool languageUpdateOnly = false)
        {
            if (NexusModsUtilities.HasAPIKey)
            {
                if (!languageUpdateOnly)
                {
                    var loggedIn = await AuthToNexusMods();
                    if (loggedIn == null)
                    {
                        M3Log.Error(
                            @"Error authorizing to NexusMods, did not get response from server or issue occurred while checking credentials. Setting not authorized");
                        SetNexusNotAuthorizedUI();
                    }
                }

                if (NexusModsUtilities.UserInfo != null)
                {
                    //prevent resetting ui to not authorized
                    NexusLoginInfoString = NexusModsUtilities.UserInfo.Name;
                    return;
                }
            }

            SetNexusNotAuthorizedUI();
        }

        /// <summary>
        /// Sets the UI to show not authorized state
        /// </summary>
        private void SetNexusNotAuthorizedUI()
        {
            NexusLoginInfoString = M3L.GetString(M3L.string_loginToNexusMods);
            ME1NexusEndorsed = ME2NexusEndorsed = ME3NexusEndorsed = LENexusEndorsed = false;
            EndorseM3String = M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
        }

        /// <summary>
        /// Authenticates to NexusMods and retrieves endorsement status
        /// </summary>
        /// <param name="languageUpdateOnly">If only updating language strings</param>
        /// <returns>User info from NexusMods, or null if authentication failed</returns>
        private async Task<Pathoschild.FluentNexus.Models.User> AuthToNexusMods(bool languageUpdateOnly = false)
        {
            if (languageUpdateOnly)
            {
                if (NexusModsUtilities.UserInfo != null)
                {
                    EndorseM3String = (ME1NexusEndorsed || ME2NexusEndorsed || ME3NexusEndorsed || LENexusEndorsed)
                        ? M3L.GetString(M3L.string_endorsedME3TweaksModManagerOnNexusMods)
                        : M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
                }
                else
                {
                    EndorseM3String = M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
                }

                return null;
            }

            M3Log.Information(@"Authenticating to NexusMods...");
            var userInfo = await NexusModsUtilities.AuthToNexusMods();
            if (userInfo != null)
            {
                M3Log.Information(@"Authenticated to NexusMods");

                //ME1
                var me1Status = await NexusModsUtilities.GetEndorsementStatusForFile(@"masseffect", 149);
                ME1NexusEndorsed = me1Status ?? false;

                //ME2
                var me2Status = await NexusModsUtilities.GetEndorsementStatusForFile(@"masseffect2", 248);
                ME2NexusEndorsed = me2Status ?? false;

                //ME3
                var me3Status = await NexusModsUtilities.GetEndorsementStatusForFile(@"masseffect3", 373);
                ME3NexusEndorsed = me3Status ?? false;

                //LE
                var leStatus = await NexusModsUtilities.GetEndorsementStatusForFile(@"masseffectlegendaryedition", 2);
                LENexusEndorsed = leStatus ?? false;

                EndorseM3String = (ME1NexusEndorsed || ME2NexusEndorsed || ME3NexusEndorsed || LENexusEndorsed)
                    ? M3L.GetString(M3L.string_endorsedME3TweaksModManagerOnNexusMods)
                    : M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
            }
            else
            {
                M3Log.Information(
                    @"Did not authenticate to NexusMods. May not be logged in or there was network issue");
                EndorseM3String = M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
            }

            return userInfo;
        }

        /// <summary>
        /// Shows the NexusMods login panel
        /// </summary>
        private bool CanShowNexusPanel()
        {
            return true; //might make some condition later.
        }

        /// <summary>
        /// Displays the NexusMods login panel
        /// </summary>
        private void ShowNexusPanel()
        {
            var nexusModsLoginPane = new NexusModsLogin();
            nexusModsLoginPane.Close += (a, b) => { ReleaseBusyControl(); };
            ShowBusyControl(nexusModsLoginPane);
        }

        /// <summary>
        /// Handles endorsement of the selected mod
        /// </summary>
        private void EndorseWrapper()
        {
            if (SelectedMod.IsEndorsed)
            {
                var unendorseresult = M3L.ShowDialog(this,
                    M3L.GetString(M3L.string_interp_unendorseMod, SelectedMod.ModName),
                    M3L.GetString(M3L.string_confirmUnendorsement), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (unendorseresult == MessageBoxResult.Yes)
                {
                    UnendorseMod();
                }
            }
            else
            {
                EndorseMod();
            }
        }

        /// <summary>
        /// Checks if the selected mod can be endorsed
        /// </summary>
        /// <returns>True if the mod can be endorsed</returns>
        private bool CanEndorseMod() => NexusModsUtilities.HasAPIKey && SelectedMod != null &&
                                        SelectedMod.NexusModID > 0 && SelectedMod.CanEndorse && !IsEndorsingMod;

        /// <summary>
        /// Endorses the selected mod on NexusMods
        /// </summary>
        private void EndorseMod()
        {
            if (SelectedMod != null)
            {
                M3Log.Information(@"Endorsing mod: " + SelectedMod.ModName);
                CurrentModEndorsementStatus = M3L.GetString(M3L.string_endorsing);
                IsEndorsingMod = true;
                SelectedMod.EndorseMod(EndorsementCallback, true);
            }
        }

        /// <summary>
        /// Unendorses the selected mod on NexusMods
        /// </summary>
        private void UnendorseMod()
        {
            if (SelectedMod != null)
            {
                M3Log.Information(@"Unendorsing mod: " + SelectedMod.ModName);
                CurrentModEndorsementStatus = M3L.GetString(M3L.string_unendorsing);
                IsEndorsingMod = true;
                SelectedMod.EndorseMod(EndorsementCallback, false);
            }
        }

        /// <summary>
        /// Callback for when mod endorsement completes
        /// </summary>
        /// <param name="m">The mod that was endorsed/unendorsed</param>
        /// <param name="isModNowEndorsed">Whether the mod is now endorsed</param>
        /// <param name="endorsementFailedMessage">Error message if endorsement failed</param>
        private void EndorsementCallback(Mod m, bool isModNowEndorsed, string endorsementFailedMessage)
        {
            IsEndorsingMod = false;
            if (SelectedMod == m)
            {
                UpdatedEndorsementString();
            }

            if (endorsementFailedMessage != null)
            {
                M3L.ShowDialog(this, endorsementFailedMessage, M3L.GetString(M3L.string_couldNotEndorseFile),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Updates the endorsement status string for the current mod
        /// </summary>
        private void UpdatedEndorsementString()
        {
            if (SelectedMod != null)
            {
                if (SelectedMod.IsEndorsed)
                {
                    CurrentModEndorsementStatus = M3L.GetString(M3L.string_modEndorsed);
                }
                else
                {
                    CurrentModEndorsementStatus = M3L.GetString(M3L.string_endorseMod);
                }
            }
        }

        /// <summary>
        /// Checks if M3 can be endorsed on NexusMods
        /// </summary>
        /// <returns>True if M3 can be endorsed</returns>
        private bool CanEndorseM3()
        {
            return NexusModsUtilities.UserInfo != null && (!ME1NexusEndorsed && !ME2NexusEndorsed && !ME3NexusEndorsed);
        }

        /// <summary>
        /// Endorses M3 on all applicable NexusMods game pages
        /// </summary>
        private void EndorseM3()
        {
            if (!ME1NexusEndorsed)
            {
                M3Log.Information(@"Endorsing M3 (ME1)");
                NexusModsUtilities.EndorseFile(@"masseffect", true, 149, (newStatus) =>
                {
                    ME1NexusEndorsed = newStatus;
                    EndorseM3String = (ME1NexusEndorsed || ME2NexusEndorsed || ME3NexusEndorsed)
                        ? M3L.GetString(M3L.string_endorsedME3TweaksModManagerOnNexusMods)
                        : M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
                });
            }

            if (!ME2NexusEndorsed)
            {
                M3Log.Information(@"Endorsing M3 (ME2)");
                NexusModsUtilities.EndorseFile(@"masseffect2", true, 248, (newStatus) =>
                {
                    ME2NexusEndorsed = newStatus;
                    EndorseM3String = (ME1NexusEndorsed || ME2NexusEndorsed || ME3NexusEndorsed)
                        ? M3L.GetString(M3L.string_endorsedME3TweaksModManagerOnNexusMods)
                        : M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
                });
            }

            if (!ME3NexusEndorsed)
            {
                M3Log.Information(@"Endorsing M3 (ME3)");
                NexusModsUtilities.EndorseFile(@"masseffect3", true, 373, (newStatus) =>
                {
                    ME3NexusEndorsed = newStatus;
                    EndorseM3String = (ME1NexusEndorsed || ME2NexusEndorsed || ME3NexusEndorsed)
                        ? M3L.GetString(M3L.string_endorsedME3TweaksModManagerOnNexusMods)
                        : M3L.GetString(M3L.string_endorseME3TweaksModManagerOnNexusMods);
                });
            }
        }

        private async void UpdateModEndorsementStatus()
        {
            if (NexusModsUtilities.HasAPIKey)
            {
                if (SelectedMod.NexusModID > 0)
                {
                    if (SelectedMod.IsOwnMod)
                    {
                        CurrentModEndorsementStatus = M3L.GetString(M3L.string_cannotEndorseOwnMod);
                    }
                    else
                    {
                        CurrentModEndorsementStatus = M3L.GetString(M3L.string_gettingEndorsementStatus);

                        var endorsed = await SelectedMod.GetEndorsementStatus();
                        if (endorsed != null)
                        {
                            if (SelectedMod != null)
                            {
                                //mod might have changed since we did BG thread wait.
                                if (SelectedMod.CanEndorse)
                                {
                                    UpdatedEndorsementString();
                                }
                                else
                                {
                                    CurrentModEndorsementStatus = M3L.GetString(M3L.string_cannotEndorseMod);
                                }
                            }
                        }
                        else
                        {
                            // null = self mod
                            CurrentModEndorsementStatus = M3L.GetString(M3L.string_cannotEndorseOwnMod);

                        }
                    }

                    CommandManager.InvalidateRequerySuggested();
                }
                else
                {
                    CurrentModEndorsementStatus =
                        $@"{M3L.GetString(M3L.string_cannotEndorseMod)} ({M3L.GetString(M3L.string_notLinkedToNexusMods)})";
                }
            }
            else
            {
                CurrentModEndorsementStatus =
                    $@"{M3L.GetString(M3L.string_cannotEndorseMod)} ({M3L.GetString(M3L.string_notAuthenticated)})";
            }
        }

        #endregion
    }
}
