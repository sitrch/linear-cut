# ViewModelBase

## Назначение
Базовый класс для всех ViewModel в приложении. Реализует паттерн MVVM через интерфейс `INotifyPropertyChanged`, предоставляя механизм оповещения UI (представления) об изменении состояния данных.

## Основные компоненты и методы

- **`INotifyPropertyChanged`**:
  Реализация интерфейса позволяет связывать (binding) свойства ViewModel с элементами UI в WPF.

- **`PropertyChanged`**:
  Событие, сигнализирующее об изменении свойства.

- **`OnPropertyChanged(string propertyName)`**:
  Защищенный метод для генерации события `PropertyChanged`. Использует атрибут `[CallerMemberName]`, что позволяет опускать имя свойства при вызове из setter'а.

- **`SetProperty<T>(ref T storage, T value, string propertyName)`**:
  Удобный метод для обновления значений свойств. Проверяет, изменилось ли значение (чтобы избежать лишних обновлений UI), обновляет поле и автоматически вызывает `OnPropertyChanged`. Возвращает `true`, если значение было обновлено.

## Зависимости
- `System.ComponentModel`: для `INotifyPropertyChanged`.
- `System.Runtime.CompilerServices`: для `[CallerMemberName]`.

## Особенности
Значительно упрощает код конкретных ViewModel, так как избавляет от необходимости дублировать логику проверки изменений и вызова события для каждого свойства.
