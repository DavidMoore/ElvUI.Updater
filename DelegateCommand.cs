using System;
using System.Threading;
using System.Windows.Input;

namespace SadRobot.ElvUI
{
    /// <summary>
    /// Implements a command that when executed runs a delegate action.
    /// </summary>
    public class DelegateCommand : ICommand
    {
        readonly Action<object> action;
        readonly Func<object, bool> canExecute;
        readonly SynchronizationContext synchronizationContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand"/> class.
        /// </summary>
        /// <param name="action">The action to run when the command is executed.</param>
        public DelegateCommand(Action<object> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            synchronizationContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand"/> class.
        /// </summary>
        /// <param name="action">The action to run when the command is executed.</param>
        /// <param name="canExecute">The function to call to evaluate when the command can be executed.</param>
        public DelegateCommand(Action<object> action, Func<object, bool> canExecute) : this(action)
        {
            this.canExecute = canExecute;
        }

        /// <inheritdoc />
        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute(parameter);
        }

        /// <inheritdoc />
        public void Execute(object parameter)
        {
            if (CanExecute(parameter)) action(parameter);
        }

        /// <inheritdoc />
        public event EventHandler CanExecuteChanged;
        
        public void OnCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            if (handler == null) return;
            
            if (synchronizationContext != null && synchronizationContext != SynchronizationContext.Current)
            {
                synchronizationContext.Post(o => handler.Invoke(this, EventArgs.Empty), null);
            }
            else
            {
                handler.Invoke(this, EventArgs.Empty);
            }
        }
    }
}