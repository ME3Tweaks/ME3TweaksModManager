using AdonisUI.Controls;
using ME3TweaksCore.Helpers;

namespace ME3TweaksModManager.modmanager.localizations
{
    // The code implementation part of M3L - M3 localization
    // Do not put string keys here!
    /// <summary>
    /// M3L - M3 Localization. This class is responsible for fetching localized strings and showing message boxes in the correct language.
    /// </summary>
    public static partial class M3L
    {
        /// <summary>
        /// Displays a message dialog with the specified message and caption using default OK button.
        /// </summary>
        /// <param name="owner">The owner window of the dialog.</param>
        /// <param name="message">The message to display in the dialog.</param>
        /// <param name="caption">The caption text for the dialog title bar.</param>
        /// <returns>The result of the user's interaction with the message box.</returns>
        internal static System.Windows.MessageBoxResult ShowDialog(System.Windows.Window owner, string message, string caption)
        {
            return ShowDialog(owner, message, caption, System.Windows.MessageBoxButton.OK);
        }

        /// <summary>
        /// Displays a message dialog with the specified message, caption, and buttons.
        /// </summary>
        /// <param name="owner">The owner window of the dialog.</param>
        /// <param name="message">The message to display in the dialog.</param>
        /// <param name="caption">The caption text for the dialog title bar.</param>
        /// <param name="buttons">The buttons to display in the dialog.</param>
        /// <returns>The result of the user's interaction with the message box.</returns>
        internal static System.Windows.MessageBoxResult ShowDialog(System.Windows.Window owner, string message, string caption, System.Windows.MessageBoxButton buttons)
        {
            return ShowDialog(owner, message, caption, buttons, System.Windows.MessageBoxImage.None);
        }

        /// <summary>
        /// Displays a message dialog with the specified message, caption, buttons, and icon.
        /// </summary>
        /// <param name="owner">The owner window of the dialog.</param>
        /// <param name="message">The message to display in the dialog.</param>
        /// <param name="caption">The caption text for the dialog title bar.</param>
        /// <param name="buttons">The buttons to display in the dialog.</param>
        /// <param name="image">The icon to display in the dialog.</param>
        /// <returns>The result of the user's interaction with the message box.</returns>
        internal static System.Windows.MessageBoxResult ShowDialog(System.Windows.Window owner, string message, string caption, System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxImage image)
        {
            return ShowDialog(owner, message, caption, buttons, image, System.Windows.MessageBoxResult.None);
        }

        /// <summary>
        /// Displays a message dialog with the specified message and default caption and OK button.
        /// </summary>
        /// <param name="owner">The owner window of the dialog.</param>
        /// <param name="message">The message to display in the dialog.</param>
        /// <returns>The result of the user's interaction with the message box.</returns>
        internal static System.Windows.MessageBoxResult ShowDialog(System.Windows.Window owner, string message)
        {
            return ShowDialog(owner, message, "", System.Windows.MessageBoxButton.OK);
        }

        /// <summary>
        /// Displays a message dialog with full customization options including custom button labels.
        /// </summary>
        /// <param name="owner">The owner window of the dialog.</param>
        /// <param name="message">The message to display in the dialog.</param>
        /// <param name="caption">The caption text for the dialog title bar.</param>
        /// <param name="buttons">The buttons to display in the dialog.</param>
        /// <param name="image">The icon to display in the dialog.</param>
        /// <param name="defaultResult">The default button that is highlighted when the dialog appears.</param>
        /// <param name="yesContent">Custom label for the Yes button, if null uses localized default.</param>
        /// <param name="noContent">Custom label for the No button, if null uses localized default.</param>
        /// <param name="okContent">Custom label for the OK button, if null uses localized default.</param>
        /// <param name="cancelContent">Custom label for the Cancel button, if null uses localized default.</param>
        /// <returns>The result of the user's interaction with the message box.</returns>
        internal static System.Windows.MessageBoxResult ShowDialog(System.Windows.Window owner, string message, string caption, System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxImage image, System.Windows.MessageBoxResult defaultResult, 
            string yesContent = null, string noContent = null, string okContent = null, string cancelContent = null)
        {
            var button = translateWpfButton(buttons);
            var messageBox = new MessageBoxModel
            {
                Text = message,
                Caption = caption,
                Icon = translateWpfImage(image),
                Buttons = MessageBoxButtons.Create(button, buildLabelsArray(button, yesContent, noContent, okContent, cancelContent)),
                IsSoundEnabled = Settings.PlayDialogSounds
            };

            messageBox.SetDefaultButton(translateWpfResult(defaultResult));

            var result = MessageBox.Show(owner, messageBox);
            return translateAdonisResult(result);
        }

        /// <summary>
        /// Builds an array of button labels for the message box, using custom labels if provided or localized defaults.
        /// </summary>
        /// <param name="button">The button configuration for the message box.</param>
        /// <param name="yesContent">Custom label for the Yes button, if null uses localized default.</param>
        /// <param name="noContent">Custom label for the No button, if null uses localized default.</param>
        /// <param name="okContent">Custom label for the OK button, if null uses localized default.</param>
        /// <param name="cancelContent">Custom label for the Cancel button, if null uses localized default.</param>
        /// <returns>An array of button labels in the correct order for the specified button configuration.</returns>
        private static string[] buildLabelsArray(MessageBoxButton button, string yesContent, string noContent, string okContent, string cancelContent)
        {
            var labels = new List<string>();
            
            switch (button)
            {
                case MessageBoxButton.OK:
                    labels.Add(okContent ?? GetString(string_ok));
                    break;
                case MessageBoxButton.OKCancel:
                    labels.Add(okContent ?? GetString(string_ok));
                    labels.Add(cancelContent ?? GetString(string_cancel));
                    break;
                case MessageBoxButton.YesNo:
                    labels.Add(yesContent ?? GetString(string_yes));
                    labels.Add(noContent ?? GetString(string_no));
                    break;
                case MessageBoxButton.YesNoCancel:
                    labels.Add(yesContent ?? GetString(string_yes));
                    labels.Add(noContent ?? GetString(string_no));
                    labels.Add(cancelContent ?? GetString(string_cancel));
                    break;
            }

            return labels.ToArray();
        }

        /// <summary>
        /// Translates a WPF MessageBoxImage to an AdonisUI MessageBoxImage. This is necessary because the two libraries use different enums for the same concept.
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        private static MessageBoxImage translateWpfImage(System.Windows.MessageBoxImage image)
        {
            return image switch
            {
                System.Windows.MessageBoxImage.None => MessageBoxImage.None,
                System.Windows.MessageBoxImage.Error => MessageBoxImage.Error,
                System.Windows.MessageBoxImage.Question => MessageBoxImage.Question,
                System.Windows.MessageBoxImage.Exclamation => MessageBoxImage.Exclamation,
                System.Windows.MessageBoxImage.Asterisk => MessageBoxImage.Asterisk,
                _ => MessageBoxImage.None
            };
        }

        /// <summary>
        /// Translates a WPF MessageBoxButton to an AdonisUI MessageBoxButtons. This is necessary because the two libraries use different enums for the same concept.
        /// </summary>
        /// <param name="button"></param>
        /// <returns></returns>
        private static MessageBoxButton translateWpfButton(System.Windows.MessageBoxButton button)
        {
            return button switch
            {
                System.Windows.MessageBoxButton.OK => MessageBoxButton.OK,
                System.Windows.MessageBoxButton.OKCancel => MessageBoxButton.OKCancel,
                System.Windows.MessageBoxButton.YesNo => MessageBoxButton.YesNo,
                System.Windows.MessageBoxButton.YesNoCancel => MessageBoxButton.YesNoCancel,
                _ => MessageBoxButton.OK
            };
        }

        /// <summary>
        /// Translates a WPF MessageBoxResult to an AdonisUI MessageBoxResult. This is necessary because the two libraries use different enums for the same concept.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static MessageBoxResult translateWpfResult(System.Windows.MessageBoxResult result)
        {
            return result switch
            {
                System.Windows.MessageBoxResult.OK => MessageBoxResult.OK,
                System.Windows.MessageBoxResult.Cancel => MessageBoxResult.Cancel,
                System.Windows.MessageBoxResult.Yes => MessageBoxResult.Yes,
                System.Windows.MessageBoxResult.No => MessageBoxResult.No,
                System.Windows.MessageBoxResult.None => MessageBoxResult.None,
                _ => MessageBoxResult.None
            };
        }

        /// <summary>
        /// Translates an AdonisUI MessageBoxResult to a WPF MessageBoxResult for compatibility with WPF message box APIs.
        /// </summary>
        /// <param name="result">The AdonisUI message box result to translate.</param>
        /// <returns>The equivalent WPF MessageBoxResult value.</returns>
        private static System.Windows.MessageBoxResult translateAdonisResult(MessageBoxResult result)
        {
            return result switch
            {
                MessageBoxResult.OK => System.Windows.MessageBoxResult.OK,
                MessageBoxResult.Cancel => System.Windows.MessageBoxResult.Cancel,
                MessageBoxResult.Yes => System.Windows.MessageBoxResult.Yes,
                MessageBoxResult.No => System.Windows.MessageBoxResult.No,
                MessageBoxResult.None => System.Windows.MessageBoxResult.None,
                _ => System.Windows.MessageBoxResult.None
            };
        }

        /// <summary>
        /// Retrieves a localized string from the resource dictionary and formats it with the provided interpolation items.
        /// </summary>
        /// <param name="resourceKey">The resource key for the localized string. Must start with "string_".</param>
        /// <param name="interpolationItems">Optional parameters to format into the localized string.</param>
        /// <returns>The localized and formatted string, or an error message if the resource cannot be found.</returns>
        internal static string GetString(string resourceKey, params object[] interpolationItems)
        {
            if (System.Windows.Application.Current == null) return @"TESTRUN"; //running in test mode
            try
            {
                if (!resourceKey.StartsWith(@"string_")) throw new Exception(@"Localization keys must start with a string_ identifier!");
                var str = (string)System.Windows.Application.Current.FindResource(resourceKey);
                str = str.Replace(@"\n", Environment.NewLine);
                return string.Format(str, interpolationItems);
            }
            catch (Exception e)
            {
                M3Log.Error($@"Error fetching string with key {resourceKey}: {e.ToString()}.");
                TelemetryInterposer.TrackError(e, new Dictionary<string, string> { { @"String key", resourceKey } });
                return $@"Error fetching string with key {resourceKey}: {e.ToString()}! Please report this to Mgamerz";
            }
        }
    }
}
