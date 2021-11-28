using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;

namespace ME3TweaksModManager.ui
{

    //from https://stackoverflow.com/questions/10280763/why-is-canexecute-invoked-after-the-command-source-is-removed-from-the-ui
    // however I don't implement the dicitonary so the unsubscribes aren't useful.
    public class RelayCommand : ICommand, IDisposable
    {
        #region Fields

        List<EventHandler> _canExecuteSubscribers = new List<EventHandler>();
        readonly Action<object> _execute;
        readonly Predicate<object> _canExecute;

        #endregion // Fields

        #region Constructors

        public RelayCommand(Action<object> execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");

            _execute = execute;
            _canExecute = canExecute;
        }

        #endregion // Constructors

        #region ICommand

        [DebuggerStepThrough]
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add
            {
                CommandManager.RequerySuggested += value;
                _canExecuteSubscribers.Add(value);
            }
            remove
            {
                CommandManager.RequerySuggested -= value;
                _canExecuteSubscribers.Remove(value);
            }
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        #endregion // ICommand

        #region IDisposable

        public void Dispose()
        {
            _canExecuteSubscribers.ForEach(h => CanExecuteChanged -= h);
            _canExecuteSubscribers.Clear();
        }

        #endregion // IDisposable
    }



    public class GenericCommand : ICommand, IDisposable
    {
        #region Fields

        List<EventHandler> _canExecuteSubscribers = new List<EventHandler>();
        readonly Action _execute;
        readonly Func<bool> _canExecute;

        #endregion // Fields

        #region Constructors
        public GenericCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion // Constructors

        #region ICommand

        [DebuggerStepThrough]
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute();
        }

        public event EventHandler CanExecuteChanged
        {
            add
            {
                CommandManager.RequerySuggested += value;
                _canExecuteSubscribers.Add(value);
            }
            remove
            {
                CommandManager.RequerySuggested -= value;
                _canExecuteSubscribers.Remove(value);
            }
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        #endregion // ICommand

        #region IDisposable

        public void Dispose()
        {
            _canExecuteSubscribers.ForEach(h => CanExecuteChanged -= h);
            _canExecuteSubscribers.Clear();
        }

        #endregion // IDisposable
    }
}

