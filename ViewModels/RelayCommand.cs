using System;
using System.Windows.Input;

namespace LinearCutWpf.ViewModels
{
    /// <summary>
    /// Реализация интерфейса ICommand для привязки команд (Commands) в паттерне MVVM.
    /// Позволяет делегировать логику выполнения команд методам ViewModel.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        /// <summary>
        /// Инициализирует новый экземпляр класса RelayCommand.
        /// </summary>
        /// <param name="execute">Метод, выполняемый при вызове команды.</param>
        /// <param name="canExecute">Метод, определяющий, может ли команда выполняться в данный момент (необязательно).</param>
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Определяет, может ли команда выполняться.
        /// </summary>
        /// <param name="parameter">Параметр команды.</param>
        /// <returns>True, если команда может быть выполнена; иначе false.</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// <summary>
        /// Выполняет логику команды.
        /// </summary>
        /// <param name="parameter">Параметр команды.</param>
        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// Событие, возникающее при изменении условий, влияющих на то, может ли выполняться команда.
        /// Привязывается к CommandManager.RequerySuggested для автоматического обновления состояния кнопок в UI.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
