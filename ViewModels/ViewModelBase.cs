using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LinearCutWpf.ViewModels
{
    /// <summary>
    /// Базовый класс для ViewModel, реализующий интерфейс INotifyPropertyChanged.
    /// Обеспечивает оповещение UI об изменениях свойств.
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Событие, возникающее при изменении значения свойства.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Вызывает событие PropertyChanged для указанного свойства.
        /// </summary>
        /// <param name="propertyName">Имя изменившегося свойства (подставляется автоматически).</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Устанавливает новое значение свойства, если оно отличается от текущего, и вызывает событие PropertyChanged.
        /// </summary>
        /// <typeparam name="T">Тип свойства.</typeparam>
        /// <param name="storage">Ссылка на поле, хранящее значение свойства.</param>
        /// <param name="value">Новое значение.</param>
        /// <param name="propertyName">Имя свойства (подставляется автоматически).</param>
        /// <returns>True, если значение было изменено; иначе false.</returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
