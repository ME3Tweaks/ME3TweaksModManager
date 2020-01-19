using System;
using System.Windows.Input;

namespace MassEffectModManagerCore.ui
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter)
        {
            bool result = _canExecute?.Invoke(parameter) ?? true;
            return result;
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class GenericCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        private ICommand debugReload;
        private Func<bool> canDebugReload;

        public GenericCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public GenericCommand(ICommand debugReload, Func<bool> canDebugReload)
        {
            this.debugReload = debugReload;
            this.canDebugReload = canDebugReload;
        }

        public bool CanExecute(object parameter)
        {
            bool result = _canExecute?.Invoke() ?? true;
            return result;
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}

