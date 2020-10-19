using System.Windows;
using System.Windows.Data;
using MassEffectModManagerCore.modmanager.converters;

namespace MassEffectModManagerCore.ui
{
    public static class WPFExtensions
    {
        /// <summary>
        /// Binds a property
        /// </summary>
        /// <param name="bound">object to create binding on</param>
        /// <param name="boundProp">property to create binding on</param>
        /// <param name="source">object being bound to</param>
        /// <param name="sourceProp">property being bound to</param>
        /// <param name="converter">optional value converter</param>
        /// <param name="parameter">optional value converter parameter</param>
        public static void bind(this FrameworkElement bound, DependencyProperty boundProp, object source,
            string sourceProp,
            IValueConverter converter = null, object parameter = null)
        {
            Binding b = new Binding { Source = source, Path = new PropertyPath(sourceProp) };
            if (converter != null)
            {
                b.Converter = converter;
                b.ConverterParameter = parameter;
            }

            bound.SetBinding(boundProp, b);
        }
    }

    public static class SharedUIBindings
    {
        public static bool GetVisibilityToEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(VisibilityToEnabledProperty);
        }

        public static void SetVisibilityToEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(VisibilityToEnabledProperty, value);
        }
        public static readonly DependencyProperty VisibilityToEnabledProperty =
            DependencyProperty.RegisterAttached("VisibilityToEnabled", typeof(bool), typeof(SharedUIBindings), new PropertyMetadata(false, OnVisibilityToEnabledChanged));

        private static void OnVisibilityToEnabledChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is FrameworkElement element)
            {
                if ((bool)args.NewValue)
                {
                    element.bind(UIElement.VisibilityProperty, element, nameof(FrameworkElement.IsEnabled), new BoolToVisibilityConverter());
                }
                else
                {
                    BindingOperations.ClearBinding(element, UIElement.VisibilityProperty);
                }
            }
        }
    }
}