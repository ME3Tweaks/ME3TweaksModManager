using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ME3TweaksCoreWPF.UI;

namespace ME3TweaksModManager.modmanager.objects.starterkit
{
    public static class FuncHelper
    {
        public static Predicate<T> ToPredicate<T>(this Func<T, bool> f)
        {
            return x => f(x);
        }
    }

    /// <summary>
    /// A feature with a command so that starter kit items can be shared in multiple places
    /// </summary>
    public class StarterKitAddinFeature
    {
        public StarterKitAddinFeature(string title, Action execute, Func<bool> canExecute = null, MEGame[] validGames = null)
        {
            DisplayString = title;
            AddFeatureCommand = new GenericCommand(execute, canExecute);
            ValidGames = validGames;
        }

        public StarterKitAddinFeature(string title, Action<object> execute, Func<object, bool> canExecute = null)
        {
            DisplayString = title;
            AddFeatureCommand = new RelayCommand(execute, canExecute?.ToPredicate());
        }

        /// <summary>
        /// String shown to user on the UI element
        /// </summary>
        public string DisplayString { get; init; }

        /// <summary>
        /// Command used for UI binding
        /// </summary>
        public ICommand AddFeatureCommand { get; init; }

        /// <summary>
        /// Games this feature is available for
        /// </summary>
        public MEGame[] ValidGames { get; init; }
    }
}
