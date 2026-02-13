using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Navigation;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.extensions;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.me3tweaks.services;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for DynamicHelpWindow.xaml - Windows Help-style three-pane layout
    /// </summary>
    public partial class DynamicHelpWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<HelpItemViewModel> HelpItems { get; } = new ObservableCollection<HelpItemViewModel>();
        
        private HelpItemViewModel _currentContent;
        public HelpItemViewModel CurrentContent
        {
            get => _currentContent;
            set
            {
                _currentContent = value;
                OnPropertyChanged(nameof(CurrentContent));
                OnPropertyChanged(nameof(HasCurrentContent));
            }
        }

        public bool HasCurrentContent => CurrentContent != null;

        private Stack<HelpItemViewModel> _backStack = new Stack<HelpItemViewModel>();
        private Stack<HelpItemViewModel> _forwardStack = new Stack<HelpItemViewModel>();

        public GenericCommand NavigateBackCommand { get; }
        public GenericCommand NavigateForwardCommand { get; }
        public GenericCommand NavigateHomeCommand { get; }

        public DynamicHelpWindow()
        {
            DataContext = this;

            NavigateBackCommand = new GenericCommand(NavigateBack, CanNavigateBack);
            NavigateForwardCommand = new GenericCommand(NavigateForward, CanNavigateForward);
            NavigateHomeCommand = new GenericCommand(NavigateHome, () => HelpItems.Any());

            LoadHelpItems();
            
            InitializeComponent();
            this.ApplyDarkNetWindowTheme();

            if (HelpItems.Any())
            {
                NavigateHome();
            }
        }

        private void LoadHelpItems()
        {
            var language = App.CurrentLanguage ?? @"int";
            var helpElements = DynamicHelpService.GetHelpItems(language);

            foreach (var element in helpElements)
            {
                HelpItems.Add(new HelpItemViewModel(element));
            }
        }

        private void HelpTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is HelpItemViewModel selectedItem)
            {
                NavigateToItem(selectedItem);
            }
        }

        private void NavigateToItem(HelpItemViewModel item)
        {
            if (CurrentContent != null)
            {
                _backStack.Push(CurrentContent);
                _forwardStack.Clear();
            }

            CurrentContent = item;
            
            NavigateBackCommand.RaiseCanExecuteChanged();
            NavigateForwardCommand.RaiseCanExecuteChanged();
        }

        private void NavigateBack()
        {
            if (_backStack.Count > 0)
            {
                if (CurrentContent != null)
                {
                    _forwardStack.Push(CurrentContent);
                }

                CurrentContent = _backStack.Pop();
                
                NavigateBackCommand.RaiseCanExecuteChanged();
                NavigateForwardCommand.RaiseCanExecuteChanged();
            }
        }

        private bool CanNavigateBack() => _backStack.Count > 0;

        private void NavigateForward()
        {
            if (_forwardStack.Count > 0)
            {
                if (CurrentContent != null)
                {
                    _backStack.Push(CurrentContent);
                }

                CurrentContent = _forwardStack.Pop();
                
                NavigateBackCommand.RaiseCanExecuteChanged();
                NavigateForwardCommand.RaiseCanExecuteChanged();
            }
        }

        private bool CanNavigateForward() => _forwardStack.Count > 0;

        private void NavigateHome()
        {
            if (HelpItems.Any())
            {
                NavigateToItem(HelpItems[0]);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            M3Utilities.OpenWebpage(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ViewModel wrapper for SortableHelpElement to provide bindable properties
    /// </summary>
    public class HelpItemViewModel : INotifyPropertyChanged
    {
        private readonly SortableHelpElement _element;

        public HelpItemViewModel(SortableHelpElement element)
        {
            _element = element;
            
            foreach (var child in element.Children)
            {
                Children.Add(new HelpItemViewModel(child));
            }
        }

        public string Title => _element.Title;
        public string ToolTip => _element.ToolTip;
        public string URL => _element.URL;
        public string ModalTitle => _element.ModalTitle;
        public string ModalIcon => _element.ModalIcon;
        public string ModalText => _element.ModalText;
        public string FontAwesomeIconResource => _element.FontAwesomeIconResource;
        
        public bool HasIcon => !string.IsNullOrWhiteSpace(FontAwesomeIconResource);
        public bool HasModalIcon => !string.IsNullOrWhiteSpace(ModalIcon);
        public bool HasModalText => !string.IsNullOrWhiteSpace(ModalText);
        public bool HasURL => !string.IsNullOrWhiteSpace(URL);
        public bool HasResourceImage => !string.IsNullOrWhiteSpace(ResourceImagePath);

        public string ResourceImagePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_element.ResourceName))
                    return null;

                var resourcePath = Path.Combine(M3Filesystem.GetLocalHelpResourcesDirectory(), _element.ResourceName);
                return File.Exists(resourcePath) ? resourcePath : null;
            }
        }

        public ObservableCollection<HelpItemViewModel> Children { get; } = new ObservableCollection<HelpItemViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
