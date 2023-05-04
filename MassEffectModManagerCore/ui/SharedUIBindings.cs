using System.Windows;
using System.Windows.Data;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.converters;

namespace ME3TweaksModManager.ui
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
            DependencyProperty.RegisterAttached(@"VisibilityToEnabled", typeof(bool), typeof(SharedUIBindings), new PropertyMetadata(false, OnVisibilityToEnabledChanged));

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


        // GENERATIONS
        #region OT

        #region BASE OT
        public static bool GetGenerationOT(DependencyObject obj)
        {
            return (bool)obj.GetValue(GenerationOTProperty);
        }

        public static void SetGenerationOT(DependencyObject obj, bool value)
        {
            obj.SetValue(GenerationOTProperty, value);
        }
        public static readonly DependencyProperty GenerationOTProperty =
            DependencyProperty.RegisterAttached(@"GenerationOT", typeof(bool), typeof(SharedUIBindings), new PropertyMetadata(false, OnGenerationOTVisibilityChanged));

        private static void OnGenerationOTVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is FrameworkElement element)
            {
                if ((bool)args.NewValue)
                {
                    var propertyInfo = typeof(Settings).GetProperty(nameof(Settings.GenerationSettingOT));
                    var propertyPath = new PropertyPath(@"(0)", propertyInfo);
                    var binding = new Binding() { Path = propertyPath, Mode = BindingMode.TwoWay, Converter = new BoolToVisibilityConverter() };
                    element.SetBinding(UIElement.VisibilityProperty, binding);
                }
                else
                {
                    BindingOperations.ClearBinding(element, UIElement.VisibilityProperty);
                }
            }
        }
        #endregion

        #region 

        

        #endregion
        public static bool GetDevModeGenerationOT(DependencyObject obj)
        {
            return (bool)obj.GetValue(GenerationDevModeOTProperty);
        }

        public static void SetDevModeGenerationOT(DependencyObject obj, bool value)
        {
            obj.SetValue(GenerationDevModeOTProperty, value);
        }
        public static readonly DependencyProperty GenerationDevModeOTProperty =
            DependencyProperty.RegisterAttached(@"GenerationDevModeOT", typeof(bool), typeof(SharedUIBindings), new PropertyMetadata(false, OnGenerationDevModeOTVisibilityChanged));

        private static void OnGenerationDevModeOTVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is FrameworkElement element)
            {
                if ((bool)args.NewValue)
                {
                    var propertyInfo1 = typeof(Settings).GetProperty(nameof(Settings.GenerationSettingOT));
                    var propertyPath1 = new PropertyPath(@"(0)", propertyInfo1);

                    var propertyInfo2 = typeof(Settings).GetProperty(nameof(Settings.DeveloperMode));
                    var propertyPath2 = new PropertyPath(@"(0)", propertyInfo2);


                    var binding = new MultiBinding();
                    binding.Bindings.Add(new Binding(){ Path = propertyPath1, Mode = BindingMode.OneWay});
                    binding.Bindings.Add(new Binding(){ Path = propertyPath2, Mode = BindingMode.OneWay});
                    binding.Converter = new MultiBoolToVisibilityConverter();
                    element.SetBinding(UIElement.VisibilityProperty, binding);
                }
                else
                {
                    BindingOperations.ClearBinding(element, UIElement.VisibilityProperty);
                }
            }
        }


        #endregion

        #region LE
        public static bool GetGenerationLE(DependencyObject obj)
        {
            return (bool)obj.GetValue(GenerationLEProperty);
        }

        public static void SetGenerationLE(DependencyObject obj, bool value)
        {
            obj.SetValue(GenerationLEProperty, value);
        }
        public static readonly DependencyProperty GenerationLEProperty =
            DependencyProperty.RegisterAttached(@"GenerationLE", typeof(bool), typeof(SharedUIBindings), new PropertyMetadata(false, OnGenerationLEVisibilityChanged));

        private static void OnGenerationLEVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is FrameworkElement element)
            {
                if ((bool)args.NewValue)
                {
                    var propertyInfo = typeof(Settings).GetProperty(nameof(Settings.GenerationSettingLE));
                    var propertyPath = new PropertyPath(@"(0)", propertyInfo);
                    var binding = new Binding() { Path = propertyPath, Mode = BindingMode.TwoWay, Converter = new BoolToVisibilityConverter() };
                    element.SetBinding(UIElement.VisibilityProperty, binding);
                }
                else
                {
                    BindingOperations.ClearBinding(element, UIElement.VisibilityProperty);
                }
            }
        }
        #endregion
    }
}