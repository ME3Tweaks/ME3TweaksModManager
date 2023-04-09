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

    public class StarterKitAddinFeature
    {
        public StarterKitAddinFeature(string title, Action execute, Func<bool> canExecute = null)
        {
            DisplayString = title;
            AddFeatureCommand = new GenericCommand(execute, canExecute);
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


    }
}
