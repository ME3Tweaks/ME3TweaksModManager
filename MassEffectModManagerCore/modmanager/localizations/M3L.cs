using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using Serilog;

namespace MassEffectModManagerCore.modmanager.localizations
{
    /// <summary>
    /// ME3 - M3 Localizer. Call GetString() with one of it's listed static keys
    /// </summary>
    [Localizable(false)]
    public static class M3L
    {
        internal static string GetString(string resourceKey)
        {
            try
            {
                if (!resourceKey.StartsWith(@"string_")) throw new Exception(@"Localization keys must start with a string_ identifier!");
                return (string)Application.Current.FindResource(resourceKey);
            }
            catch (Exception e)
            {
                Log.Error($@"Error fetching string with key {resourceKey}: {e.ToString()}. Returning value of ERROR.");
                return "ERROR!";
            }
        }


        //Huge list of all keys

        //DO NOT MODIFY BELOW THIS LINE - Localizer tool operates on this this file and the following items are automatically generated from the int.xaml file. 
        //KEYLISTSTART
        public static readonly string string_startingUp = "string_startingUp";
        public static readonly string string_selectModOnLeftToGetStarted = "string_selectModOnLeftToGetStarted";
        public static readonly string string_applyMod = "string_applyMod";
        public static readonly string string_addTarget = "string_addTarget";
        public static readonly string string_startGame = "string_startGame";
        public static readonly string string_installationTarget = "string_installationTarget";

        public static readonly string string_binkAsiBypassNotInstalled = "string_binkAsiBypassNotInstalled";
        public static readonly string string_binkAsiBypassInstalled = "string_binkAsiBypassInstalled";
        public static readonly string string_binkAsiLoaderNotInstalled = "string_binkAsiLoaderNotInstalled";
        public static readonly string string_binkAsiLoaderInstalled = "string_binkAsiLoaderInstalled";
        public static readonly string string_gameNotInstalled = "string_gameNotInstalled";
        public static readonly string string_otherSavedTargets = "string_otherSavedTargets";

        public static readonly string string_cannotEndorseMod = "string_cannotEndorseMod";
        public static readonly string string_cannotEndorseOwnMod = "string_cannotEndorseOwnMod";
        public static readonly string string_notLinkedToNexusMods = "string_notLinkedToNexusMods";
        public static readonly string string_notAuthenticated = "string_notAuthenticated";
        public static readonly string string_gettingEndorsementStatus = "string_gettingEndorsementStatus";

        public static readonly string string_endorseMod = "string_endorseMod";
        public static readonly string string_modEndorsed = "string_modEndorsed";

    }
}
