using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LegendaryExplorerCore.Packages;

namespace ME3TweaksModManager.modmanager.converters
{
    [Localizable(false)]
    public class GameToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MEGame game && parameter is string gameStr)
            {
                Visibility v = Visibility.Visible;

                var splitparms = gameStr.Split('_');

                // Go by pairings
                for (int i = 0; i < splitparms.Length; i++)
                {
                    bool inverted = splitparms[i] == @"Not";
                    if (inverted) i++; // skip to next parm


                    if (Enum.TryParse(splitparms[i], out MEGame parameterGame))
                    {
                        if (inverted ^ parameterGame == game) continue; // OK, do not set to collapsed
                    }
                    else if (splitparms[i].StartsWith("Game"))
                    {
                        var gameId = gameStr[^1];
                        switch (gameId)
                        {
                            case '1':
                                if (inverted ^ game.IsGame1()) continue;
                                break;
                            case '2':
                                if (inverted ^ game.IsGame2()) continue;
                                break;
                            case '3':
                                if (inverted ^ game.IsGame3()) continue;
                                break;
                        }
                    }
                    else if (splitparms[i] == "LEGame")
                    {
                        if (inverted ^ game.IsLEGame()) continue;
                    }
                    else if (splitparms[i] == "OTGame")
                    {
                        if (inverted ^ game.IsOTGame()) continue;
                    }

                    // One of the above conditions did not register as true.
                    return Visibility.Collapsed;
                }

                // We are OK
                return Visibility.Visible;
            }

            // Set up incorrectly.
            Debug.WriteLine(@"Incorrect setup for GameToVisibilityConverter!");
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}